using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using NeoSmart.AsyncLock;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers;

/// <summary>
/// GPU controller for monitoring and managing NVIDIA discrete GPU state.
/// </summary>
/// <remarks>
/// <para>
/// This controller provides the following features:
/// </para>
/// <list type="bullet">
///   <item><description>GPU state monitoring (active, inactive, powered off, etc.)</description></item>
///   <item><description>GPU process management</description></item>
///   <item><description>GPU restart and process termination</description></item>
///   <item><description>Adaptive refresh interval (2s when active, 10s when idle)</description></item>
/// </list>
/// <para>
/// Uses NVAPI for NVIDIA driver communication. Requires NVIDIA GPU support.
/// </para>
/// </remarks>
public class GPUController : IDisposable
{
    private readonly AsyncLock _lock = new();
    private readonly IGPUProcessManager _processManager;
    private readonly IGPUHardwareManager _hardwareManager;
    private readonly IDelayProvider _delayProvider;
    private int _disposed = 0;

    private Task? _refreshTask;
    private Task? _stoppingTask;
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private readonly object _startStopLock = new();

    private GPUState _state = GPUState.Unknown;
    private IReadOnlyList<Process> _processes = Array.Empty<Process>();
    private string? _gpuInstanceId;
    private string? _performanceState;
    private int _currentInterval;
    private DateTime _lastStateChangeTime = DateTime.MinValue;
    private const int ActiveInterval = 2000;
    private const int InactiveInterval = 10000;
    private const int StabilizationDelay = 5000;

    /// <summary>
    /// Event raised when GPU state is refreshed.
    public event EventHandler<GPUStatus>? Refreshed;

    /// <summary>
    /// Gets whether the GPU monitoring service is started.
    public bool IsStarted
    {
        get
        {
            lock (_startStopLock)
                return _refreshTask is { IsCompleted: false };
        }
    }

    /// <summary>
    /// Creates a new instance of GPUController.
    /// <param name="processManager">GPU process manager.</param>
    /// <param name="hardwareManager">GPU hardware manager.</param>
    public GPUController(IGPUProcessManager processManager, IGPUHardwareManager hardwareManager, IDelayProvider delayProvider)
    {
        _processManager = processManager;
        _hardwareManager = hardwareManager;
        _delayProvider = delayProvider;
    }

    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

            if (!Compatibility.IsSupportedLegionMachine(mi))
                return false;

            NVAPI.Initialize();
            return NVAPI.GetGPU() is not null;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("gpu-is-supported", "GPUController.IsSupportedAsync failed (NVAPI/WMI probe).", ex);
            return false;
        }
        finally
        {
            try
            {
                NVAPI.Unload();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"NVAPI unload failed: {ex.Message}", ex);
            }
        }
    }

    public async Task<GPUState> GetLastKnownStateAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
            return _state;
    }

    public async Task<GPUStatus> RefreshNowAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            await RefreshStateAsync().ConfigureAwait(false);
            return new GPUStatus(_state, _performanceState, _processes);
        }
    }

    public Task StartAsync(int delay = 1_000, int interval = 5_000)
    {
        Task? leftover;
        lock (_startStopLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            if (_refreshTask is { IsCompleted: false })
                return Task.CompletedTask;

            leftover = _stoppingTask;
            _stoppingTask = null;
        }

        if (leftover is { IsCompleted: false })
            return StartAfterLeftoverAsync(leftover, delay, interval);

        return StartCore(delay, interval);
    }

    private async Task StartAfterLeftoverAsync(Task leftover, int delay, int interval)
    {
        try
        {
            await leftover.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Previous GPU refresh loop did not stop in time; starting a new loop.");
        }
        catch (OperationCanceledException)
        {
        }

        await StartCore(delay, interval).ConfigureAwait(false);
    }

    private Task StartCore(int delay, int interval)
    {
        lock (_startStopLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            if (_refreshTask is { IsCompleted: false })
                return Task.CompletedTask;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting GPU service [interval={interval}ms]");

            Log.Instance.Info($"GPU monitoring started [controller={nameof(GPUController)}]");

            _currentInterval = interval;
            _refreshCancellationTokenSource?.Dispose();
            _refreshCancellationTokenSource = new CancellationTokenSource();
            var token = _refreshCancellationTokenSource.Token;
            _refreshTask = Task.Run(() => RefreshLoopAsync(delay, interval, token), token);
            return Task.CompletedTask;
        }
    }

    public async Task StopAsync(bool waitForFinish = false)
    {
        CancellationTokenSource? ctsToDispose = null;
        Task? taskToWait = null;

        lock (_startStopLock)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Stopping GPU service");

            Log.Instance.Info($"GPU monitoring stopped [controller={nameof(GPUController)}]");

            ctsToDispose = _refreshCancellationTokenSource;
            _refreshCancellationTokenSource = null;
            taskToWait = _refreshTask;
            _refreshTask = null;
            if (taskToWait is { IsCompleted: false })
                _stoppingTask = taskToWait;

            try
            {
                ctsToDispose?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (waitForFinish && taskToWait is not null)
        {
            try
            {
                await taskToWait.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when GPU service is stopped, no action needed
            }

            lock (_startStopLock)
            {
                if (ReferenceEquals(_stoppingTask, taskToWait))
                    _stoppingTask = null;
            }
        }

        ctsToDispose?.Dispose();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"GPU service stopped");
    }

    public async Task RestartGPUAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_state is not GPUState.Active and not GPUState.Inactive)
                return;

            if (string.IsNullOrWhiteSpace(_gpuInstanceId))
                return;

            await _hardwareManager.RestartGPUAsync(_gpuInstanceId).ConfigureAwait(false);
        }
    }

    public async Task KillGPUProcessesAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_state is not GPUState.Active)
                return;

            if (_processes.Count == 0)
                return;

            await _processManager.KillGPUProcessesAsync(_processes).ConfigureAwait(false);
        }
    }

    private async Task RefreshLoopAsync(int delay, int interval, CancellationToken token)
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Initializing NVAPI");

            NVAPI.Initialize();

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"NVAPI initialized");

            await _delayProvider.Delay(TimeSpan.FromMilliseconds(delay), token).ConfigureAwait(false);

            while (true)
            {
                token.ThrowIfCancellationRequested();

                using (await _lock.LockAsync(token).ConfigureAwait(false))
                {
                    await RefreshStateAsync().ConfigureAwait(false);
                    Refreshed?.Invoke(this, new GPUStatus(_state, _performanceState, _processes));
                }

                var adjustedInterval = AdjustRefreshInterval();
                if (adjustedInterval > 0)
                    await _delayProvider.Delay(TimeSpan.FromMilliseconds(adjustedInterval), token).ConfigureAwait(false);
                else
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Warning($"GPU controller exception", ex);
        }
        finally
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unloading NVAPI");

            NVAPI.Unload();

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"NVAPI unloaded");
        }
    }

    private int AdjustRefreshInterval()
    {
        var now = DateTime.UtcNow;
        var timeSinceStateChange = (now - _lastStateChangeTime).TotalMilliseconds;

        if (_state == GPUState.Active)
        {
            if (timeSinceStateChange > StabilizationDelay)
            {
                if (_currentInterval != ActiveInterval)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Refresh interval: {ActiveInterval}ms (active)");
                    _currentInterval = ActiveInterval;
                }
                return ActiveInterval;
            }
        }
        else if (_state == GPUState.Inactive || _state == GPUState.MonitorConnected)
        {
            if (timeSinceStateChange > StabilizationDelay)
            {
                if (_currentInterval != InactiveInterval)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Refresh interval: {InactiveInterval}ms (inactive)");
                    _currentInterval = InactiveInterval;
                }
                return InactiveInterval;
            }
        }

        return _currentInterval;
    }

    private async Task RefreshStateAsync()
    {
        var previousState = _state;
        ResetState();

        if (NVAPI.GetGPU() is not { } gpu)
        {
            HandleGpuNotFound(previousState);
            return;
        }

        // If NVAPI reports the GPU is powered off, do not let DetermineGpuState overwrite it.
        if (TryGetPerformanceState(gpu))
        {
            CheckStateChange(previousState);
            return;
        }

        var pnpDeviceIdPart = NVAPI.GetGPUId(gpu);
        var gpuInstanceId = await TryGetGpuInstanceIdAsync(pnpDeviceIdPart).ConfigureAwait(false);
        var processNames = NVAPIExtensions.GetActiveProcesses(gpu);

        DetermineGpuState(gpu, gpuInstanceId, processNames, previousState);
    }

    private static async Task<string?> TryGetGpuInstanceIdAsync(string? pnpDeviceIdPart)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceIdPart))
            return null;

        try
        {
            var gpuInstanceId = await WMI.Win32.PnpEntity.GetDeviceIDAsync(pnpDeviceIdPart).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(gpuInstanceId) ? null : gpuInstanceId;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "gpu-instance-id",
                $"Failed to resolve GPU PnP instance id for '{pnpDeviceIdPart}'.",
                ex);
            return null;
        }
    }

    private void ResetState()
    {
        _state = GPUState.Unknown;
        _processes = Array.Empty<Process>();
        _gpuInstanceId = null;
        _performanceState = null;
    }

    private void HandleGpuNotFound(GPUState previousState)
    {
        _state = GPUState.NvidiaGpuNotFound;
        CheckStateChange(previousState);
    }

    /// <summary>
    /// Reads NVAPI performance state. Returns <c>true</c> when the GPU is powered off
    /// and refresh should stop (do not run <see cref="DetermineGpuState"/>).
    /// </summary>
    private bool TryGetPerformanceState(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var pstateText = NVAPI.GetCurrentPstate(gpu).ToString();
            var stateId = pstateText.Contains('_')
                ? pstateText.GetUntilOrEmpty("_")
                : pstateText; // fallback: use full text for values without underscore (e.g. "Undefined")
            _performanceState = Resource.GPUController_PoweredOn;
            if (!string.IsNullOrWhiteSpace(stateId))
                _performanceState += $", {stateId}";
            return false;
        }
        catch (Exception ex) when (ex.Message.Contains("GpuNotPowered"))
        {
            _state = GPUState.PoweredOff;
            _performanceState = Resource.GPUController_PoweredOff;
            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "gpu-perf-state",
                "Failed to read NVAPI performance state; reporting Unknown.",
                ex);
            _performanceState = "Unknown";
            return false;
        }
    }

    private void DetermineGpuState(NvPhysicalGpuHandle gpu, string? gpuInstanceId, List<Process> processNames, GPUState previousState)
    {
        if (NVAPI.IsDisplayConnected(gpu))
        {
            HandleMonitorConnected(processNames, previousState);
        }
        else if (processNames.Count != 0)
        {
            HandleActive(gpuInstanceId, processNames, previousState);
        }
        else
        {
            HandleInactive(gpuInstanceId, previousState);
        }
    }

    private void HandleMonitorConnected(List<Process> processNames, GPUState previousState)
    {
        _processes = processNames;
        _state = GPUState.MonitorConnected;
        CheckStateChange(previousState);
    }

    private void HandleActive(string? gpuInstanceId, List<Process> processNames, GPUState previousState)
    {
        _processes = processNames;
        _state = GPUState.Active;
        _gpuInstanceId = gpuInstanceId;
        CheckStateChange(previousState);
    }

    private void HandleInactive(string? gpuInstanceId, GPUState previousState)
    {
        _state = GPUState.Inactive;
        _gpuInstanceId = gpuInstanceId;
        CheckStateChange(previousState);
    }

    private void CheckStateChange(GPUState previousState)
    {
        if (_state != previousState)
        {
            _lastStateChangeTime = DateTime.UtcNow;
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU state changed from {previousState} to {_state}");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Atomic dispose guard (Pillar A): volatile bool is NOT an atomic check-then-set,
        // so two concurrent Dispose()/finalize calls could both pass the guard and double-dispose
        // the CTS and process list. Use Interlocked.CompareExchange to make teardown idempotent
        // under any concurrent Dispose interleaving (BUG-2026-07-09-007).
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        if (disposing)
        {
            try
            {
                CancellationTokenSource? ctsToDispose;
                lock (_startStopLock)
                {
                    ctsToDispose = _refreshCancellationTokenSource;
                    _refreshCancellationTokenSource = null;
                    _refreshTask = null;
                    _stoppingTask = null;
                }

                try
                {
                    ctsToDispose?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                ctsToDispose?.Dispose();

                if (_processes != null)
                {
                    foreach (var process in _processes)
                    {
                        try { process.Dispose(); } catch
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace("Failed to dispose process");
                        }
                    }
                    _processes = Array.Empty<Process>();
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"GPUController disposal error", ex);
            }
        }
    }
}
