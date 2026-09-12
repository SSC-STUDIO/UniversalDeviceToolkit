using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;
#if WINDOWS
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.CLI;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.GameDetection;
using UniversalDeviceToolkit.Lib.Integrations;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Overclocking.Amd;
using UniversalDeviceToolkit.Lib.Services;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
#endif

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Headless equivalent of the WPF startup background initialization:
/// safe-start detection, network recovery and the same
/// serial hardware steps + service start steps used by the desktop app.
/// Progress is surfaced to the Electron front end via bridge events.
/// </summary>
public sealed class HardwareInitializer
{
    private const int ServiceStartConcurrency = 2;

    private readonly HostFlags _flags;
    private readonly BridgeRpcServer _rpc;
    private readonly CancellationTokenSource _cts = new();
    private StartupHealthGuard? _guard;
    private bool _shouldEnterSafeMode;
    private IReadOnlyList<string> _skippedSteps = Array.Empty<string>();
    private Task? _backgroundTask;

    public HardwareInitializer(HostFlags flags, BridgeRpcServer rpc)
    {
        _flags = flags ?? throw new ArgumentNullException(nameof(flags));
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
    }

    public bool ShouldEnterSafeMode => _shouldEnterSafeMode;

    public IReadOnlyList<string> SkippedSteps => _skippedSteps;

    /// <summary>
    /// Runs the synchronous pre-flight work (safe-start decision,
    /// network recovery) and kicks off the hardware background initialization.
    /// The host is responsive to bridge requests immediately after this returns.
    /// </summary>
    public async Task RunAsync()
    {
        DetermineAndApplySafeStartMode();
#if WINDOWS
        await RunNetworkStartupRecoveryAsync().ConfigureAwait(false);
#endif
        _backgroundTask = Task.Run(() => RunBackgroundInitializationAsync(_cts.Token), _cts.Token);
    }

    /// <summary>
    /// Cancels and awaits the background initialization.
    /// </summary>
    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_backgroundTask is not null)
        {
            try
            {
                await _backgroundTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort stop.
            }
        }
        _cts.Dispose();
    }

    private void DetermineAndApplySafeStartMode()
    {
        _guard ??= new StartupHealthGuard();
        var persistedFailureCount = StartupHealthGuard.ReadPersistedConsecutiveFailureCount();
        var persistedShouldEnterSafeMode = persistedFailureCount >= StartupHealthGuard.DefaultConsecutiveFailureThreshold;
        var interruptedHardwareInit = StartupHealthGuard.IsHardwareInitInProgressMarkerPresent();

        _shouldEnterSafeMode = _flags.SafeStart || persistedShouldEnterSafeMode || interruptedHardwareInit;

        if (interruptedHardwareInit)
        {
            StartupHealthGuard.ClearHardwareInitInProgress();
            Log.Instance.Info("SafeStart mode auto-engaged: previous hardware initialization was interrupted.");
        }

        if (_flags.SafeStart)
        {
            Log.Instance.Info("SafeStart mode requested: skipping non-critical initialization steps.");
        }
        else if (persistedShouldEnterSafeMode)
        {
            Log.Instance.Info(
                $"SafeStart mode auto-engaged: previous run reported {persistedFailureCount} consecutive failures.");
        }

        try
        {
            _guard.ResetFailureCount();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Resetting health guard on startup failed: {ex.Message}", ex);
        }
    }

#if WINDOWS
    private static async Task RunNetworkStartupRecoveryAsync()
    {
        try
        {
            var network = IoCContainer.Resolve<INetworkAccelerationService>();
            await network.EnsureCleanSystemStateOnStartupAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Network startup recovery failed: {ex.Message}", ex);
        }
    }
#endif

    private async Task RunBackgroundInitializationAsync(CancellationToken cancellationToken)
    {
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var completedCleanly = false;
        StartupHealthGuard.MarkHardwareInitInProgress();

        var (initializationSteps, serviceStartSteps) = GetBackgroundInitializationSteps();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_flags.NoHardware)
            {
#if WINDOWS
                var runner = new StartupInitializationRunner(_guard ?? new StartupHealthGuard(), safeStart: _shouldEnterSafeMode);
                for (var i = 0; i < initializationSteps.Length; i++)
                {
                    var step = initializationSteps[i];
                    runner.RegisterStep($"bg-hw-{i}", TimeSpan.FromSeconds(45), step, isCritical: false);
                }

                var hwResult = await runner.RunAsync(cancellationToken).ConfigureAwait(false);
                if (Log.Instance.IsTraceEnabled)
                {
                    Log.Instance.Trace(
                        $"Background hardware init via StartupInitializationRunner: success={hwResult.Success}, " +
                        $"failed=[{string.Join(", ", hwResult.FailedSteps)}], elapsed={totalSw.ElapsedMilliseconds}ms.");
                }

                await RunWithLimitedConcurrencyAsync(serviceStartSteps, ServiceStartConcurrency, cancellationToken).ConfigureAwait(false);
#else
                // Non-Windows: no hardware initialization steps are available.
                // The CLI IpcServer (named-pipe bridge) is intentionally skipped
                // here; the host talks to the front end over stdio (BridgeRpcServer).
                _skippedSteps = NonWindowsSkippedSteps;
#endif
            }

            completedCleanly = true;
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Background initialization completed in {totalSw.ElapsedMilliseconds}ms.");

            _rpc.Publish("host.initialized", new
            {
                success = true,
                skippedSteps = _skippedSteps,
                elapsedMs = totalSw.ElapsedMilliseconds,
            });
        }
        catch (OperationCanceledException)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Background initialization was cancelled after {totalSw.ElapsedMilliseconds}ms.");

            // Host shutdown cancels this work. Leaving the in-progress marker
            // would make the next launch auto-enter safe-start as if we crashed.
            if (_cts.IsCancellationRequested)
                completedCleanly = true;
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Background initialization failed after {totalSw.ElapsedMilliseconds}ms.", ex);

            _rpc.Publish("host.initialized", new
            {
                success = false,
                error = $"{ex.GetType().Name}: {ex.Message}",
                elapsedMs = totalSw.ElapsedMilliseconds,
            });
        }
        finally
        {
            if (completedCleanly)
                StartupHealthGuard.ClearHardwareInitInProgress();
        }
    }

#if WINDOWS
    public (Func<Task>[] initializationSteps, Func<Task>[] serviceStartSteps) GetBackgroundInitializationSteps()
    {
        var vantageDisabler = IoCContainer.Resolve<VantageDisabler>();
        var legionZoneDisabler = IoCContainer.Resolve<LegionZoneDisabler>();
        var fnKeysDisabler = IoCContainer.Resolve<FnKeysDisabler>();
        var lampArrayController = IoCContainer.Resolve<LampArrayController>();
        var lampArraySettings = IoCContainer.Resolve<LampArraySettings>();
        var powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
        var itsModeFeature = IoCContainer.Resolve<ITSModeFeature>();
        var batteryFeature = IoCContainer.Resolve<BatteryFeature>();
        var rgbKeyboardController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
        var spectrumKeyboardController = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
        var gpuOverclockController = IoCContainer.Resolve<GPUOverclockController>();
        var hybridModeFeature = IoCContainer.Resolve<HybridModeFeature>();
        var fanCurveManager = IoCContainer.Resolve<FanCurveManager>();
        var fanCurveSettings = IoCContainer.Resolve<FanCurveSettings>();
        var amdOverclockingController = IoCContainer.Resolve<AmdOverclockingController>();
        var automationProcessor = IoCContainer.Resolve<AutomationProcessor>();

        if (_shouldEnterSafeMode || _flags.NoHardware)
        {
            if (_shouldEnterSafeMode && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Safe-start: skipping hardware re-apply and third-party integrations.");

            Func<Task>[] safeBg =
            [
                () => LogSoftwareStatusAsync(vantageDisabler, legionZoneDisabler, fnKeysDisabler),
            ];
            Func<Task>[] safePost =
            [
                () => IoCContainer.Resolve<IpcServer>().StartStopIfNeededAsync(),
            ];
            _skippedSteps =
            [
                "lamp-array", "power-mode", "its-mode", "battery-feature", "rgb-keyboard",
                "spectrum-keyboard", "gpu-overclock", "hybrid-mode", "fan-manager",
                "amd-overclock", "automation-processor", "ai-controller", "hwinfo", "battery-monitor",
            ];
            return (safeBg, safePost);
        }

        Func<Task>[] bgSteps =
        [
            () => LogSoftwareStatusAsync(vantageDisabler, legionZoneDisabler, fnKeysDisabler),
            () => InitControllerAsync(lampArrayController, lampArraySettings),
            () => InitPowerModeFeatureAsync(powerModeFeature),
            () => InitItsModeFeatureAsync(itsModeFeature),
            () => InitBatteryFeatureAsync(batteryFeature),
            () => InitRgbKeyboardControllerAsync(rgbKeyboardController),
            () => InitSpectrumKeyboardControllerAsync(spectrumKeyboardController),
            () => InitGpuOverclockControllerAsync(gpuOverclockController),
            () => InitHybridModeAsync(hybridModeFeature),
            () => InitFanManagerAsync(fanCurveManager, powerModeFeature, fanCurveSettings),
            () => InitAmdOverclockingAsync(amdOverclockingController),
            () => InitAutomationProcessorAsync(automationProcessor),
        ];
        Func<Task>[] postSteps =
        [
            () => IoCContainer.Resolve<AIController>().StartIfNeededAsync(),
            () => IoCContainer.Resolve<HWiNFOIntegration>().StartStopIfNeededAsync(),
            () => IoCContainer.Resolve<IpcServer>().StartStopIfNeededAsync(),
            () => IoCContainer.Resolve<BatteryDischargeRateMonitorService>().StartStopIfNeededAsync(),
            () => IoCContainer.Resolve<GameBoostService>().StartAsync(),
        ];
        return (bgSteps, postSteps);
    }
#else
    /// <summary>
    /// Windows hardware/service step names reported as skipped on portable
    /// hosts (host.initialized event, consumed by the Electron startup gates).
    /// </summary>
    private static readonly string[] NonWindowsSkippedSteps =
    [
        "lamp-array", "power-mode", "its-mode", "battery-feature", "rgb-keyboard",
        "spectrum-keyboard", "gpu-overclock", "hybrid-mode", "fan-manager",
        "amd-overclock", "automation-processor", "ai-controller", "hwinfo", "battery-monitor",
    ];

    public (Func<Task>[] initializationSteps, Func<Task>[] serviceStartSteps) GetBackgroundInitializationSteps()
    {
        _skippedSteps = NonWindowsSkippedSteps;
        return ([], []);
    }
#endif

    private void PersistStartupHealthOutcome(Exception? failure = null)
    {
        try
        {
            if (_guard is null)
                return;

            if (failure is not null)
            {
                StartupHealthGuard.WritePersistedState(StartupHealthGuard.DefaultConsecutiveFailureThreshold, shouldEnterSafeMode: true);
                return;
            }

            StartupHealthGuard.WritePersistedState(0, shouldEnterSafeMode: false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to persist startup-health outcome: {ex.Message}", ex);
        }
    }

    public async Task PersistOutcomeAndFinalizeAsync(bool success, Exception? failure = null)
    {
        PersistStartupHealthOutcome(failure);
        await StopAsync().ConfigureAwait(false);
    }

#if WINDOWS
    private static async Task RunWithLimitedConcurrencyAsync(
        IReadOnlyList<Func<Task>> steps,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        if (steps.Count == 0)
            return;

        maxConcurrency = Math.Max(1, maxConcurrency);
        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = new Task[steps.Count];

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            tasks[i] = Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await step().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Instance.Warning("Background service start step failed.", ex);
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private static Task RunWithErrorHandlingAsync(Func<Task> action, string operationName, bool logOnSuccess = true)
        => AppHostHelpers.RunInitStepAsync(action, operationName, logOnSuccess);

    private static async Task LogSoftwareStatusAsync(VantageDisabler vantageDisabler, LegionZoneDisabler legionZoneDisabler, FnKeysDisabler fnKeysDisabler)
    {
        if (!Log.Instance.IsTraceEnabled)
            return;

        var statuses = await Task.WhenAll(
            vantageDisabler.GetStatusAsync(),
            legionZoneDisabler.GetStatusAsync(),
            fnKeysDisabler.GetStatusAsync()
        );

        Log.Instance.Trace($"Vantage status: {statuses[0]}");
        Log.Instance.Trace($"LegionZone status: {statuses[1]}");
        Log.Instance.Trace($"FnKeys status: {statuses[2]}");
    }

    private static async Task InitControllerAsync(LampArrayController controller, LampArraySettings settings)
    {
        await RunWithErrorHandlingAsync(
            () => controller.InitializeAsync(settings),
            "lamp array controller",
            false
        );
    }

    private static async Task InitPowerModeFeatureAsync(PowerModeFeature feature)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (await feature.IsSupportedAsync())
                {
                    var godModeSettings = IoCContainer.TryResolve<GodModeSettings>();
                    var recovery = new HardwareStateRecoveryService();
                    var godModeValid = HardwareConfigRangeValidator.TryValidateOrBackupGodMode(
                        godModeSettings,
                        filename => recovery.TryBackupFile(filename, out _),
                        msg =>
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace(msg);
                            else
                                Log.Instance.Warning(msg);
                        });

                    if (godModeValid)
                    {
                        await feature.EnsureGodModeStateIsAppliedAsync();
                    }
                    else if (Log.Instance.IsTraceEnabled)
                    {
                        Log.Instance.Trace("Skipping EnsureGodModeStateIsAppliedAsync due to invalid God Mode range.");
                    }

                    await feature.EnsureCorrectWindowsPowerSettingsAreSetAsync();
                }
            },
            "power mode feature",
            false
        );
    }

    private static async Task InitItsModeFeatureAsync(ITSModeFeature feature)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (await feature.IsSupportedAsync())
                    await feature.SetStateAsync(await feature.GetStateAsync());
            },
            "ITS mode feature",
            false
        );
    }

    private static async Task InitBatteryFeatureAsync(BatteryFeature feature)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (await feature.IsSupportedAsync())
                    await feature.EnsureCorrectBatteryModeIsSetAsync();
            },
            "battery feature",
            false
        );
    }

    private static async Task InitRgbKeyboardControllerAsync(RGBKeyboardBacklightController controller)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (await controller.IsSupportedAsync())
                    await controller.SetLightControlOwnerAsync(true, true);
            },
            "RGB keyboard controller",
            false
        );
    }

    private static async Task InitSpectrumKeyboardControllerAsync(SpectrumKeyboardBacklightController controller)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (await controller.IsSupportedAsync())
                    await controller.StartAuroraIfNeededAsync();
            },
            "Spectrum keyboard controller",
            false
        );
    }

    private static async Task InitGpuOverclockControllerAsync(GPUOverclockController controller)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (await controller.IsSupportedAsync())
                {
                    var gpuSettings = IoCContainer.TryResolve<GPUOverclockSettings>();
                    var recovery = new HardwareStateRecoveryService();
                    var gpuValid = HardwareConfigRangeValidator.TryValidateOrBackupGpuOverclock(
                        controller,
                        gpuSettings,
                        maxCoreDeltaMhz: GPUOverclockController.GetMaxCoreDeltaMhz(),
                        maxMemoryDeltaMhz: HardwareConfigRangeValidator.DefaultMaxGpuMemoryDeltaMhz,
                        tryBackupFile: filename => recovery.TryBackupFile(filename, out _),
                        log: msg =>
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace(msg);
                            else
                                Log.Instance.Warning(msg);
                        });

                    if (!gpuValid)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace("Skipping EnsureOverclockIsAppliedAsync due to invalid GPU OC range.");
                        return;
                    }

                    await controller.EnsureOverclockIsAppliedAsync();
                }
            },
            "GPU overclock controller",
            false
        );
    }

    private static async Task InitHybridModeAsync(HybridModeFeature feature)
    {
        await RunWithErrorHandlingAsync(
            async () => await feature.EnsureDGPUEjectedIfNeededAsync(),
            "hybrid mode"
        );
    }

    private static async Task InitFanManagerAsync(FanCurveManager fanManager, PowerModeFeature powerMode, FanCurveSettings fanSettings)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (!await fanManager.IsSupportedAsync())
                    return;

                await fanManager.InitializeAsync();

                var mi = await Compatibility.GetMachineInformationAsync();
                if (mi.LegionSeries <= LegionSeries.Legion_Legacy)
                {
                    if (await powerMode.GetStateAsync() != PowerModeState.GodMode)
                        return;
                }

                var recovery = new HardwareStateRecoveryService();
                var fansValid = HardwareConfigRangeValidator.TryValidateOrBackupFanCurves(
                    fanSettings,
                    filename => recovery.TryBackupFile(filename, out _),
                    msg =>
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace(msg);
                        else
                            Log.Instance.Warning(msg);
                    });

                if (!fansValid)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("Skipping fan curve apply due to invalid range.");
                    return;
                }

                if (fanSettings.Store.Entries.Count == 0)
                    await fanSettings.SynchronizeStoreAsync();

                await fanManager.LoadAndApply(fanSettings.Store.Entries);
            },
            "fan manager",
            false
        );
    }

    private static async Task InitAmdOverclockingAsync(AmdOverclockingController controller)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                if (!controller.IsActive())
                    return;

                await controller.InitializeAsync();

                if (!controller.DoNotApply)
                    await controller.ApplyInternalProfileAsync();
            },
            "AMD overclocking",
            false
        );
    }

    private static async Task InitAutomationProcessorAsync(AutomationProcessor automationProcessor)
    {
        await RunWithErrorHandlingAsync(
            async () =>
            {
                await automationProcessor.InitializeAsync();
                automationProcessor.RunOnStartup();
            },
            "automation processor"
        );
    }
#endif
}
