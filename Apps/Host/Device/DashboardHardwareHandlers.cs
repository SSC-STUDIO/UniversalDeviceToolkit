using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Bridges dashboard controls that are not represented by IFeature&lt;T&gt;.
/// </summary>
public static class DashboardHardwareHandlers
{
    private const int NotSupported = BridgeErrorCodes.FeatureNotSupported;

    private static readonly object GpuStatusLock = new();
    private static GPUController? _subscribedGpuController;
    private static GPUStatus? _lastGpuStatus;
    private static int _gpuMonitorCount;

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("dashboardHardware.getState", (_, ct) => HandleGetStateAsync(ct));
        rpc.RegisterHandler("dashboardHardware.setMonitoring", (request, ct) => HandleSetMonitoringAsync(request, ct));
        rpc.RegisterHandler("dashboardHardware.killGpuProcesses", (_, ct) => HandleKillGpuProcessesAsync(ct));
        rpc.RegisterHandler("dashboardHardware.restartGpu", (_, ct) => HandleRestartGpuAsync(ct));
        rpc.RegisterHandler("dashboardHardware.setOverclockEnabled", (request, ct) => HandleSetOverclockEnabledAsync(request, ct));
        rpc.RegisterHandler("dashboardHardware.setOverclock", (request, ct) => HandleSetOverclockAsync(request, ct));
        rpc.RegisterHandler("dashboardHardware.turnOffMonitors", (_, ct) => HandleTurnOffMonitorsAsync(ct));
    }

    private static async Task<BridgeResult> HandleGetStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gpuController = IoCContainer.Resolve<GPUController>();
            var discreteGpuSupported = await gpuController.IsSupportedAsync().ConfigureAwait(false);
            GPUStatus? gpuStatus = null;

            if (discreteGpuSupported)
            {
                EnsureGpuStatusSubscription(gpuController);
                var startedForProbe = false;
                if (!gpuController.IsStarted)
                {
                    await gpuController.StartAsync(delay: 0, interval: 5_000).ConfigureAwait(false);
                    startedForProbe = true;
                }

                try
                {
                    gpuStatus = await WaitForInitialGpuStatusAsync(cancellationToken).ConfigureAwait(false);
                    lock (GpuStatusLock)
                        gpuStatus ??= _lastGpuStatus;
                }
                finally
                {
                    if (startedForProbe)
                    {
                        var shouldStop = false;
                        lock (GpuStatusLock)
                            shouldStop = _gpuMonitorCount <= 0;
                        if (shouldStop)
                            await gpuController.StopAsync(waitForFinish: false).ConfigureAwait(false);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var overclockController = IoCContainer.Resolve<GPUOverclockController>();
            var overclockSupported = await overclockController.IsSupportedAsync().ConfigureAwait(false);
            var (overclockEnabled, overclockInfo) = overclockSupported
                ? overclockController.GetState()
                : (false, GPUOverclockInfo.Zero);

            return BridgeResult.Ok(new
            {
                discreteGpu = new
                {
                    supported = discreteGpuSupported,
                    state = gpuStatus?.State.ToString() ?? GPUState.Unknown.ToString(),
                    performanceState = gpuStatus?.PerformanceState,
                    processes = gpuStatus is { } status
                        ? GetProcessNames(status.Processes)
                        : Array.Empty<string>(),
                },
                overclockDiscreteGpu = new
                {
                    supported = overclockSupported,
                    enabled = overclockEnabled,
                    coreDeltaMhz = overclockInfo.CoreDeltaMhz,
                    memoryDeltaMhz = overclockInfo.MemoryDeltaMhz,
                    maxCoreDeltaMhz = GPUOverclockController.GetMaxCoreDeltaMhz(),
                    maxMemoryDeltaMhz = overclockSupported
                        ? GPUOverclockController.GetMaxMemoryDeltaMhz()
                        : 0,
                },
                turnOffMonitors = new { supported = OperatingSystem.IsWindows() },
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleSetMonitoringAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enabled = GetRequiredBoolean(request, "enabled");
            var gpuController = IoCContainer.Resolve<GPUController>();
            var supported = await gpuController.IsSupportedAsync().ConfigureAwait(false);

            if (!supported)
                throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");

            int count;
            lock (GpuStatusLock)
            {
                if (enabled)
                    _gpuMonitorCount++;
                else
                    _gpuMonitorCount = Math.Max(0, _gpuMonitorCount - 1);
                count = _gpuMonitorCount;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (count > 0)
            {
                EnsureGpuStatusSubscription(gpuController);
                await gpuController.StartAsync(delay: 0, interval: 5_000).ConfigureAwait(false);
            }
            else
            {
                await gpuController.StopAsync(waitForFinish: false).ConfigureAwait(false);
            }

            return BridgeResult.Ok(new { ok = true, monitoring = count > 0 });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleKillGpuProcessesAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gpuController = IoCContainer.Resolve<GPUController>();
            if (!await gpuController.IsSupportedAsync().ConfigureAwait(false))
                throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");

            var status = await gpuController.RefreshNowAsync().ConfigureAwait(false);
            if (status.State != GPUState.Active)
                throw new BridgeErrorException(NotSupported, "GPU is not active.");
            if (status.Processes.Count == 0)
                throw new BridgeErrorException(-32602, "No GPU processes to terminate.");

            cancellationToken.ThrowIfCancellationRequested();
            await gpuController.KillGPUProcessesAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleRestartGpuAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gpuController = IoCContainer.Resolve<GPUController>();
            if (!await gpuController.IsSupportedAsync().ConfigureAwait(false))
                throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");

            var status = await gpuController.RefreshNowAsync().ConfigureAwait(false);
            if (status.State is not GPUState.Active and not GPUState.Inactive)
                throw new BridgeErrorException(NotSupported, "GPU cannot be restarted in the current state.");

            cancellationToken.ThrowIfCancellationRequested();
            await gpuController.RestartGPUAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleSetOverclockEnabledAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enabled = GetRequiredBoolean(request, "enabled");
            var controller = IoCContainer.Resolve<GPUOverclockController>();
            await EnsureOverclockCanApplyAsync(controller, cancellationToken).ConfigureAwait(false);

            var (_, info) = controller.GetState();
            controller.SaveState(enabled, info);
            await controller.ApplyStateAsync(force: true).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true, enabled });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleSetOverclockAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var coreDeltaMhz = GetRequiredInt32(request, "coreDeltaMhz");
            var memoryDeltaMhz = GetRequiredInt32(request, "memoryDeltaMhz");
            var controller = IoCContainer.Resolve<GPUOverclockController>();
            if (!await controller.IsSupportedAsync().ConfigureAwait(false))
                throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");

            var maxCoreDeltaMhz = GPUOverclockController.GetMaxCoreDeltaMhz();
            var maxMemoryDeltaMhz = GPUOverclockController.GetMaxMemoryDeltaMhz();

            if (coreDeltaMhz < 0 || coreDeltaMhz > maxCoreDeltaMhz ||
                memoryDeltaMhz < 0 || memoryDeltaMhz > maxMemoryDeltaMhz)
            {
                throw new BridgeErrorException(-32602, "GPU overclock offsets are outside the supported range.");
            }

            var (enabled, _) = controller.GetState();
            controller.SaveState(enabled, new GPUOverclockInfo(coreDeltaMhz, memoryDeltaMhz));
            if (enabled)
            {
                await EnsureOverclockCanApplyAsync(controller, cancellationToken).ConfigureAwait(false);
                await controller.ApplyStateAsync().ConfigureAwait(false);
            }

            return BridgeResult.Ok(new { ok = true });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleTurnOffMonitorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows())
                throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");

            await IoCContainer.Resolve<NativeWindowsMessageListener>().TurnOffMonitorAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task EnsureOverclockCanApplyAsync(GPUOverclockController controller, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await controller.IsSupportedAsync().ConfigureAwait(false))
            throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");

        var vantage = await IoCContainer.Resolve<VantageDisabler>().GetStatusAsync().ConfigureAwait(false);
        if (vantage == SoftwareStatus.Enabled)
            throw new BridgeErrorException(-32603, "VANTAGE_RUNNING");

        var legionZone = await IoCContainer.Resolve<LegionZoneDisabler>().GetStatusAsync().ConfigureAwait(false);
        if (legionZone == SoftwareStatus.Enabled)
            throw new BridgeErrorException(-32603, "LEGION_ZONE_RUNNING");
    }

    private static void EnsureGpuStatusSubscription(GPUController controller)
    {
        lock (GpuStatusLock)
        {
            if (ReferenceEquals(_subscribedGpuController, controller))
                return;

            if (_subscribedGpuController is not null)
                _subscribedGpuController.Refreshed -= GpuControllerOnRefreshed;

            _subscribedGpuController = controller;
            _subscribedGpuController.Refreshed += GpuControllerOnRefreshed;
        }
    }

    private static void GpuControllerOnRefreshed(object? sender, GPUStatus status)
    {
        lock (GpuStatusLock)
            _lastGpuStatus = status;
    }

    private static async Task<GPUStatus?> WaitForInitialGpuStatusAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (GpuStatusLock)
            {
                if (_lastGpuStatus is { } status)
                    return status;
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static string[] GetProcessNames(IReadOnlyList<Process> processes) => processes
        .Select(TryGetProcessName)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name!)
        .GroupBy(name => name, StringComparer.CurrentCultureIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
        .Select(group => group.Count() > 1 ? $"{group.Key} x {group.Count()}" : group.Key)
        .ToArray();

    private static string? TryGetProcessName(Process process)
    {
        try
        {
            return process.HasExited ? null : process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool GetRequiredBoolean(BridgeRequest request, string name)
    {
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new BridgeErrorException(-32602, $"Missing boolean '{name}' parameter.");
        }

        return property.GetBoolean();
    }

    private static int GetRequiredInt32(BridgeRequest request, string name)
    {
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            throw new BridgeErrorException(-32602, $"Missing integer '{name}' parameter.");
        }

        return value;
    }
}
