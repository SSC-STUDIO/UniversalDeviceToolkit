using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.Lib;
#if WINDOWS
using Autofac;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Automation.CLI;
using UniversalDeviceToolkit.Lib.Automation.Optimization;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Integrations;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Macro;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Overclocking.Amd;
using UniversalDeviceToolkit.Lib.Services;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
#endif
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Shared.Logging;

namespace UniversalDeviceToolkit.Host;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
#if WINDOWS
        // Route an elevated optimization-worker invocation (--udt-elevated-optimization)
        // before any host startup: the worker only speaks the named-pipe protocol and
        // must not run the bridge/RPC lifecycle.
        var elevatedWorkerExitCode = await WindowsOptimizationElevationBridge
            .TryRunWorkerAsync(args).ConfigureAwait(false);
        if (elevatedWorkerExitCode.HasValue)
            return elevatedWorkerExitCode.Value;
#endif

        var flags = HostFlags.Parse(args);

        Log.Instance.IsTraceEnabled = flags.Trace;
        Environment.SetEnvironmentVariable("UDT_LOG_PATH", Log.Instance.LogPath);
        Environment.SetEnvironmentVariable("LLT_LOG_PATH", Log.Instance.LogPath);
        SharedLog.SetSink(new SerilogSharedLogSink());

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Host flags: {flags}");

        try
        {
            // Restore the shared language before hardware services or RPC
            // handlers are initialized so Host-owned resources start in that
            // culture. Startup failures remain inside the normal fatal handler.
            LocalizationRuntime.Initialize();

#if WINDOWS
            var settings = new ApplicationSettings();

            IoCContainer.Initialize(
                cb => cb.RegisterInstance(settings).As<ApplicationSettings>().AsSelf().SingleInstance(),
                new UniversalDeviceToolkit.Lib.IoCModule(),
                new UniversalDeviceToolkit.Lib.Automation.IoCModule(),
                new UniversalDeviceToolkit.Lib.Macro.IoCModule(),
                new BridgeModule());
#else
            IoCContainer.Initialize(
                preBuild: null,
                new UniversalDeviceToolkit.Lib.IoCModule(),
                new UniversalDeviceToolkit.Lib.Automation.IoCModule(),
                new UniversalDeviceToolkit.Lib.Macro.IoCModule(),
                new BridgeModule());
#endif

            IoCContainer.Resolve<HttpClientFactory>().SetProxy(
                flags.ProxyUrl is null ? null : new Uri(flags.ProxyUrl),
                flags.ProxyUsername, flags.ProxyPassword, flags.ProxyAllowAllCerts);

#if WINDOWS
            ApplyExperimentalGpuWorkingMode(flags);
#endif

            using var rpc = new BridgeRpcServer();
            Log.BridgeLineSink = line =>
            {
                try
                {
                    rpc.Publish("host.log", line);
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to publish host.log event: {ex.Message}", ex);
                }
            };
            RegisterBridgeHandlers(rpc, flags);

            var initializer = new HardwareInitializer(flags, rpc);
            await initializer.RunAsync().ConfigureAwait(false);

            rpc.Publish("host.ready", new
            {
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
                safeStart = initializer.ShouldEnterSafeMode,
                pid = Environment.ProcessId,
            });

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Bridge host ready; awaiting stdin requests.");

            await rpc.RunAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Bridge client disconnected; shutting down.");

            await ShutdownAsync(initializer).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Host startup critical failure: {ex.Message}", ex);
            try { Log.Instance.Flush(); } catch { /* ignore */ }
            return 1;
        }
    }

#if WINDOWS
    private static void ApplyExperimentalGpuWorkingMode(HostFlags flags)
    {
        var enabled = flags.ExperimentalGpuWorkingMode;
        IoCContainer.Resolve<IGPUModeFeature>().ExperimentalGPUWorkingMode = enabled;
        IoCContainer.Resolve<DGPUNotify>().ExperimentalGPUWorkingMode = enabled;
        if (enabled && Log.Instance.IsTraceEnabled)
            Log.Instance.Trace("Experimental GPU working mode enabled (LegionZone capability/flags backends).");
    }
#endif

    private static void RegisterBridgeHandlers(BridgeRpcServer rpc, HostFlags flags)
    {
        rpc.RegisterHandler("ping", async _ =>
        {
            await Task.CompletedTask;
            return BridgeResult.Ok(new
            {
                pong = true,
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
            });
        });

        rpc.RegisterHandler("app.getStatus", async _ =>
        {
            await Task.CompletedTask;
            return BridgeResult.Ok(new
            {
                pid = Environment.ProcessId,
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
                logPath = Log.Instance.LogPath,
                culture = LocalizationRuntime.CurrentCulture.Name,
            });
        });

        rpc.RegisterHandler("app.getLogPath", async _ =>
        {
            await Task.CompletedTask;
            return BridgeResult.Ok(new { path = Log.Instance.LogPath });
        });

        rpc.RegisterHandler("app.quit", async (_, _) =>
        {
            rpc.RequestShutdown();
            return BridgeResult.Ok(new { quitting = true });
        });

        LocalizationHandlers.Register(rpc);
        PortableCapabilityHandlers.Register(rpc);
        UiActivityHandlers.Register(rpc);

#if WINDOWS
        SystemHandlers.Register(rpc);
        WmiCapabilityHandlers.Register(rpc);
        SettingsHandlers.Register(rpc);
        SensorsHandlers.Register(rpc);
        BootLogoHandlers.Register(rpc);
        FeatureHandlers.Register(rpc);
        DashboardHandlers.Register(rpc);
        DashboardHardwareHandlers.Register(rpc);
        AutomationHandlers.Register(rpc);
        KeyboardBacklightHandlers.Register(rpc);
        OptimizationHandlers.Register(rpc);
        AiHandlers.Register(rpc);
        DriverDownloadHandlers.Register(rpc);
        NetworkAccelerationHandlers.Register(rpc);
        CleanupRulesHandlers.Register(rpc);
        AppIntegrationHandlers.Register(rpc);
        SoftwareDisablerHandlers.Register(rpc);
        StartupHandlers.Register(rpc);
        GodModeHandlers.Register(rpc);
        GameBoostHandlers.Register(rpc);
        MouseHandlers.Register(rpc);
#else
        PortableCoreHandlers.Register(rpc);
        PortableStoreHandlers.Register(rpc);
        RegisterPlatformUnsupportedHandlers(rpc);
#endif

        // MacroHandlers compiles on every platform: real hooks on Windows,
        // configuration-backed sequence storage (and honest -32099 for
        // playback/recording) on portable hosts.
        MacroHandlers.Register(rpc);

        VerifyRpcSurface(rpc);

        _ = flags;
    }

    /// <summary>
    /// Startup guard for the RpcMethodNames registry: every listed method must
    /// have a handler. On Windows that is the concrete implementation; on
    /// portable builds PortableCapable is implemented and WindowsOnly is stubbed
    /// from the same lists, so a rename shows up here instead of as -32601.
    /// </summary>
    private static void VerifyRpcSurface(BridgeRpcServer rpc)
    {
        foreach (var method in RpcMethodNames.WindowsOnly)
        {
            if (!rpc.HasHandler(method))
                Log.Instance.Warning($"RPC registry drift: '{method}' is listed in RpcMethodNames.WindowsOnly but no handler registered it.");
        }

        foreach (var method in RpcMethodNames.PortableCapable)
        {
            if (!rpc.HasHandler(method))
                Log.Instance.Warning($"RPC registry drift: '{method}' is listed in RpcMethodNames.PortableCapable but no handler registered it.");
        }

        foreach (var method in RpcMethodNames.AlwaysOn)
        {
            if (!rpc.HasHandler(method))
                Log.Instance.Warning($"RPC registry drift: '{method}' is listed in RpcMethodNames.AlwaysOn but no handler registered it.");
        }

        var listed = new HashSet<string>(RpcMethodNames.WindowsOnly, StringComparer.Ordinal);
        listed.UnionWith(RpcMethodNames.PortableCapable);
        listed.UnionWith(RpcMethodNames.AlwaysOn);
        foreach (var method in rpc.RegisteredMethods)
        {
            if (listed.Contains(method))
                continue;
            if (method.StartsWith("macro.", StringComparison.Ordinal))
                continue;
            Log.Instance.Warning($"RPC registry drift: '{method}' is registered but missing from RpcMethodNames (portable hosts would answer -32601).");
        }
    }

#if !WINDOWS
    /// <summary>
    /// Registers remaining Windows-only RPC domains as explicit "not supported"
    /// responses. Core Electron methods in RpcMethodNames.PortableCapable are
    /// registered by PortableCoreHandlers instead of these stubs.
    /// </summary>
    private static void RegisterPlatformUnsupportedHandlers(BridgeRpcServer rpc)
    {
        Task<BridgeResult> NotSupportedAsync(BridgeRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(BridgeResult.Error(
                BridgeErrorCodes.PlatformNotSupported, "Not supported on this platform."));
        }

        foreach (var method in RpcMethodNames.WindowsOnly)
        {
            if (rpc.HasHandler(method))
                continue;
            rpc.RegisterHandler(method, NotSupportedAsync);
        }
    }
#endif

    private static async Task ShutdownAsync(HardwareInitializer initializer)
    {
        var totalStopwatch = Stopwatch.StartNew();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace("Host shutdown started.");

        try
        {
            await initializer.PersistOutcomeAndFinalizeAsync(success: true).ConfigureAwait(false);

#if WINDOWS
            // Stop network acceleration worker and restore system proxy/hosts first.
            try
            {
                if (IoCContainer.TryResolve<INetworkAccelerationService>() is { } networkAcceleration)
                    await networkAcceleration.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error stopping network acceleration during shutdown: {ex.Message}", ex);
            }

            var stopServicesTask = Task.WhenAll(
                StopServiceAsync<AIController>(controller => controller.StopAsync(), "AI controller"),
                StopServiceAsync<RGBKeyboardBacklightController>(controller => controller.SetLightControlOwnerAsync(false), "RGB keyboard controller"),
                StopServiceAsync<SessionLockUnlockListener>(listener => listener.StopAsync(), "session lock/unlock listener"),
                StopServiceAsync<HWiNFOIntegration>(integration => integration.StopAsync(), "HWiNFO integration"),
                StopServiceAsync<IpcServer>(server => server.StopAsync(), "IPC server"),
                StopServiceAsync<BatteryDischargeRateMonitorService>(monitor => monitor.StopAsync(), "battery monitor"),
                StopServiceAsync<LampArrayController>(controller => controller.StopAsync(), "lamp array controller"),
                StopServiceAsync<NativeWindowsMessageListener>(listener => listener.StopAsync(), "native Windows message listener")
            );

            try
            {
                if (IoCContainer.TryResolve<UserInactivityAutoListener>() is { } listener)
                    await Task.Run(() => ((IDisposable)listener).Dispose()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"UserInactivityAutoListener dispose failed: {ex.Message}", ex);
            }

            var completedTask = await Task.WhenAny(stopServicesTask, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
            if (completedTask != stopServicesTask && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Service stop timed out after 2 seconds.");

            await FinalizeRuntimeProfilesAsync().ConfigureAwait(false);

            // CRITICAL: release the global input hooks (recorder + playback)
            // before exiting.
            try
            {
                MacroHandlers.StopRecordingIfActive();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error stopping active macro recording: {ex.Message}", ex);
            }

            try
            {
                if (IoCContainer.TryResolve<MacroController>() is { } macroController)
                    macroController.Stop();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error stopping MacroController: {ex.Message}", ex);
            }
#endif

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Host shutdown completed in {totalStopwatch.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Host shutdown error: {ex.Message}", ex);
        }
        finally
        {
            try { Log.Instance.Flush(); } catch { /* ignore */ }
            try { await Log.Instance.ShutdownAsync().ConfigureAwait(false); } catch { /* ignore */ }

            try { IoCContainer.Dispose(); } catch { /* ignore */ }
        }
    }

    private static async Task StopServiceAsync<T>(Func<T, Task> stopAction, string serviceName) where T : class
    {
        try
        {
            if (IoCContainer.TryResolve<T>() is not { } service)
                return;

            await stopAction(service).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Service stop failed ({serviceName}): {ex.Message}", ex);
        }
    }

#if WINDOWS
    private static async Task FinalizeRuntimeProfilesAsync()
    {
        try
        {
            if (IoCContainer.TryResolve<AmdOverclockingController>() is { } amdController && amdController.IsActive())
            {
                amdController.SaveShutdownInfo(new ShutdownInfo
                {
                    Status = "Normal",
                    AbnormalCount = 0,
                });
            }

            if (IoCContainer.TryResolve<FanCurveManager>() is { } fanManager &&
                await fanManager.IsSupportedAsync().ConfigureAwait(false))
            {
                await fanManager.SetRegisterAsync(false).ConfigureAwait(false);
            }

            if (IoCContainer.TryResolve<LampArrayController>() is { } lampArrayController &&
                IoCContainer.TryResolve<LampArraySettings>() is { } lampArraySettings)
            {
                lampArrayController.SaveSettings(lampArraySettings);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Runtime profile finalization failed: {ex.Message}", ex);
        }
    }
#endif
}
