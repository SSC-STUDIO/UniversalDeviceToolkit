// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) RAMSPDToolkit and Contributors.
// Partial Copyright (C) Michael Moeller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using LibreHardwareMonitor.Hardware;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public class SensorsGroupController : IDisposable
{
    #region Constants

    private const float INVALID_VALUE_FLOAT = -1f;
    private const string UNKNOWN_NAME = "UNKNOWN";

    #endregion

    #region State

    private bool _initialized;
    public LibreHardwareMonitorInitialState InitialState { get; private set; }
    public bool IsHybrid => _hardware.IsHybrid;

    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private readonly Lock _dataLock = new();

    #endregion

    #region Services

    private readonly SensorSnapshotStore _snapshotStore = new();
    private readonly HardwareDiscoveryService _hardware = new();

    #endregion

    #region Dependencies

    private readonly GPUController _gpuController;
    private readonly IDelayProvider _delayProvider;

    #endregion

    #region Producer / Subscribers

    private readonly Dictionary<object, TimeSpan> _subscribers = [];
    private CancellationTokenSource? _producerCts;
    private bool _producerLoopErrorLogged;
    private int _resetInFlight;
    private int _disposed;
    public event EventHandler? SensorsUpdated;

    #endregion

    public SensorsGroupController(IDelayProvider delayProvider)
        : this(delayProvider, IoCContainer.Resolve<GPUController>())
    {
    }

    internal SensorsGroupController(IDelayProvider delayProvider, GPUController gpuController)
    {
        _delayProvider = delayProvider;
        _gpuController = gpuController;
    }

    internal int SubscriberCount
    {
        get
        {
            lock (_subscribers)
                return _subscribers.Count;
        }
    }

    #region Configuration Properties

    private bool _selectedGpuIsIgpu;
    public bool SelectedGpuIsIgpu
    {
        get => _selectedGpuIsIgpu;
        set
        {
            lock (_dataLock)
            {
                if (_selectedGpuIsIgpu != value)
                {
                    _selectedGpuIsIgpu = value;
                    _cachedGpuName = string.Empty;
                }
            }
        }
    }

    private bool _showAverageCpuFrequency;
    public bool ShowAverageCpuFrequency
    {
        get => _showAverageCpuFrequency;
        set
        {
            lock (_dataLock)
            {
                _showAverageCpuFrequency = value;
            }
        }
    }

    private bool _isDgpuConnected = true;
    public bool IsDgpuConnected
    {
        get => _isDgpuConnected;
        set
        {
            lock (_dataLock)
            {
                if (_isDgpuConnected != value)
                {
                    _isDgpuConnected = value;
                    _cachedGpuName = string.Empty;
                    if (!_isDgpuConnected)
                        _hardware.ClearDiscreteGpuHardware();
                }
            }
        }
    }

    #endregion

    #region Cached Names

    private string _cachedCpuName = string.Empty;
    private string _cachedGpuName = string.Empty;

    #endregion

    #region Public Async Getters (delegated to SensorSnapshotStore)

    public Task<float> GetCpuTemperatureAsync() => _snapshotStore.GetCpuTemperatureAsync();
    public Task<float> GetCpuUsageAsync() => _snapshotStore.GetCpuUsageAsync();
    public Task<float> GetCpuFanSpeedAsync() => _snapshotStore.GetCpuFanSpeedAsync();
    public Task<float> GetGpuFanSpeedAsync() => _snapshotStore.GetGpuFanSpeedAsync();
    public Task<float> GetGpuUsageAsync() => _snapshotStore.GetGpuUsageAsync();
    public Task<float> GetGpuTemperatureAsync() => _snapshotStore.GetGpuTemperatureAsync();
    public Task<float> GetGpuCoreClockAsync() => _snapshotStore.GetGpuCoreClockAsync();
    public Task<float> GetGpuMemoryClockAsync() => _snapshotStore.GetGpuMemoryClockAsync();
    public Task<float> GetCpuPowerAsync() => _snapshotStore.GetCpuPowerAsync();
    public Task<(float cores, float memory, float platform)> GetCpuComponentPowersAsync() => _snapshotStore.GetCpuComponentPowersAsync();
    public Task<float> GetCpuVoltageAsync() => _snapshotStore.GetCpuVoltageAsync();
    public Task<float> GetCpuCoreClockAsync() => _snapshotStore.GetCpuCoreClockAsync(_showAverageCpuFrequency);
    public Task<float> GetCpuPCoreClockAsync() => _snapshotStore.GetCpuPCoreClockAsync(_showAverageCpuFrequency);
    public Task<float> GetCpuECoreClockAsync() => _snapshotStore.GetCpuECoreClockAsync(_showAverageCpuFrequency);
    public Task<float> GetGpuPowerAsync() => _snapshotStore.GetGpuPowerAsync();
    public Task<float> GetGpuVoltageAsync() => _snapshotStore.GetGpuVoltageAsync();
    public Task<float> GetGpuVramTemperatureAsync() => _snapshotStore.GetGpuVramTemperatureAsync();
    public Task<float> GetGpuHotSpotTemperatureAsync() => _snapshotStore.GetGpuHotSpotTemperatureAsync();
    public Task<float> GetGpuVramUtilizationAsync() => _snapshotStore.GetGpuVramUtilizationAsync();
    public Task<float> GetGpuVramUsedAsync() => _snapshotStore.GetGpuVramUsedAsync();
    public Task<float> GetGpuPcieRxThroughputAsync() => _snapshotStore.GetGpuPcieRxThroughputAsync();
    public Task<float> GetGpuPcieTxThroughputAsync() => _snapshotStore.GetGpuPcieTxThroughputAsync();
    public Task<(float, float)> GetSsdTemperaturesAsync() => _snapshotStore.GetSsdTemperaturesAsync();
    public Task<float> GetMemoryUsageAsync() => _snapshotStore.GetMemoryUsageAsync();
    public Task<float> GetMemoryUsedAsync() => _snapshotStore.GetMemoryUsedAsync();
    public Task<float> GetMemoryTotalAsync() => _snapshotStore.GetMemoryTotalAsync();
    public Task<double> GetHighestMemoryTemperatureAsync() => _snapshotStore.GetHighestMemoryTemperatureAsync();
    public Task<double> GetHighestMotherboardTemperatureAsync() => _snapshotStore.GetHighestMotherboardTemperatureAsync();

    #endregion

    #region Name & GPU Info (cross-service coordination)

    public Task<string> GetCpuNameAsync()
    {
        lock (_dataLock)
        {
            if (_hardware.IsResetting || !IsLibreHardwareMonitorInitialized() || _hardware.CpuHardware == null)
                return Task.FromResult(UNKNOWN_NAME);

            if (!string.IsNullOrEmpty(_cachedCpuName))
                return Task.FromResult(_cachedCpuName);

            _cachedCpuName = HardwareDiscoveryService.StripName(_hardware.CpuHardware.Name);
            return Task.FromResult(_cachedCpuName);
        }
    }

    public Task<string> GetGpuNameAsync()
    {
        lock (_dataLock)
        {
            if (_hardware.IsResetting || !IsLibreHardwareMonitorInitialized())
                return Task.FromResult(UNKNOWN_NAME);

            if (!string.IsNullOrEmpty(_cachedGpuName) && !_hardware.NeedRefreshGpuHardware)
                return Task.FromResult(_cachedGpuName);

            var dGpu = _hardware.GpuHardware ?? _hardware.AmdGpuHardware;
            var gpu = ShouldUseIntegratedGpuSnapshot(dGpu) ? _hardware.IGpuHardware : dGpu;
            _cachedGpuName = gpu != null ? HardwareDiscoveryService.StripName(gpu.Name) : UNKNOWN_NAME;
            _hardware.NeedRefreshGpuHardware = false;
            return Task.FromResult(_cachedGpuName);
        }
    }

    public Task<bool> IsCurrentGpuIntegratedAsync()
    {
        lock (_dataLock)
        {
            if (_hardware.IsResetting || !IsLibreHardwareMonitorInitialized())
                return Task.FromResult(false);

            return Task.FromResult(ShouldUseIntegratedGpuSnapshot(_hardware.GpuHardware ?? _hardware.AmdGpuHardware));
        }
    }

    public Task<float> GetGpuVramTotalAsync()
    {
        lock (_dataLock)
        {
            var dGpu = _hardware.GpuHardware ?? _hardware.AmdGpuHardware;
            float total = ShouldUseIntegratedGpuSnapshot(dGpu) ? _hardware.CachedIGpuVramTotal : _hardware.CachedGpuVramTotal;
            return Task.FromResult(total > 0 ? total : INVALID_VALUE_FLOAT);
        }
    }

    private bool ShouldUseIntegratedGpuSnapshot(IHardware? dGpu) =>
        SelectedGpuIsIgpu || dGpu == null || !_isDgpuConnected;

    #endregion

    #region Initialization

    public async Task<LibreHardwareMonitorInitialState> IsSupportedAsync()
    {
        LibreHardwareMonitorInitialState result = await InitializeAsync(HardwareDiscoveryService.InitializationMode.Full).ConfigureAwait(false);
        try
        {
            bool haveHardware;
            lock (_dataLock) { haveHardware = _hardware.HardwareCount != 0; }
            if (haveHardware && result is LibreHardwareMonitorInitialState.Initialized or LibreHardwareMonitorInitialState.Success) return result;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Sensor group check failed: {ex}");
            return result;
        }
        return LibreHardwareMonitorInitialState.Fail;
    }

    internal async Task<bool> EnsureFanSensorsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var state = await InitializeAsync(HardwareDiscoveryService.InitializationMode.FanOnly, cancellationToken).ConfigureAwait(false);
        return state is LibreHardwareMonitorInitialState.Initialized or LibreHardwareMonitorInitialState.Success;
    }

    private async Task<LibreHardwareMonitorInitialState> InitializeAsync(
        HardwareDiscoveryService.InitializationMode requestedMode,
        CancellationToken cancellationToken = default,
        bool persistFailureToSettings = true)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return LibreHardwareMonitorInitialState.Fail;

        if (_initialized && _hardware.Mode >= requestedMode)
        {
            InitialState = _hardware.HardwareCount == 0
                ? LibreHardwareMonitorInitialState.Fail
                : LibreHardwareMonitorInitialState.Initialized;
            return InitialState;
        }

        await _initSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                return LibreHardwareMonitorInitialState.Fail;

            if (_initialized && _hardware.Mode >= requestedMode)
            {
                InitialState = _hardware.HardwareCount == 0
                    ? LibreHardwareMonitorInitialState.Fail
                    : LibreHardwareMonitorInitialState.Initialized;
                return InitialState;
            }

            if (_initialized && requestedMode == HardwareDiscoveryService.InitializationMode.Full)
                _hardware.CloseHardwareForUpgrade();

            await Task.Run(() => _hardware.GetHardware(requestedMode), cancellationToken).ConfigureAwait(false);
            _initialized = true;
            InitialState = _hardware.HardwareCount == 0
                ? LibreHardwareMonitorInitialState.Fail
                : LibreHardwareMonitorInitialState.Success;
            return InitialState;
        }
        catch (DllNotFoundException)
        {
            HandleInitException("DLL Not Found", mutateSettings: persistFailureToSettings && requestedMode == HardwareDiscoveryService.InitializationMode.Full);
            InitialState = LibreHardwareMonitorInitialState.PawnIONotInstalled;
            return InitialState;
        }
        catch (Exception ex)
        {
            HandleInitException(ex.Message, mutateSettings: persistFailureToSettings && requestedMode == HardwareDiscoveryService.InitializationMode.Full);
            if (requestedMode == HardwareDiscoveryService.InitializationMode.FanOnly)
                return LibreHardwareMonitorInitialState.Fail;
            throw;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private void HandleInitException(string reason, bool mutateSettings)
    {
        Log.Instance.Trace($"LibreHardwareMonitor initialization failed: {reason}");
        if (mutateSettings)
        {
            var settings = IoCContainer.Resolve<ApplicationSettings>();
            settings.Store.EnableHardwareSensors = false;
            settings.SynchronizeStore();
        }
        InitialState = LibreHardwareMonitorInitialState.Fail;
    }

    #endregion

    #region Update

    public async Task UpdateAsync()
    {
        if (_hardware.IsResetting || !IsLibreHardwareMonitorInitialized()) return;

        var gpuState = await _gpuController.GetLastKnownStateAsync().ConfigureAwait(false);
        bool gpuInactive = IsGpuInactive(gpuState);

        var needReset = await Task.Run(() =>
        {
            var dGpu = _hardware.GpuHardware ?? _hardware.AmdGpuHardware;
            bool useIntegrated = ShouldUseIntegratedGpuSnapshot(dGpu);

            return _hardware.UpdateHardwareAndSnapshots(
                _snapshotStore, gpuInactive, useIntegrated, IsHybrid, _showAverageCpuFrequency);
        }).ConfigureAwait(false);

        if (needReset && Interlocked.CompareExchange(ref _resetInFlight, 1, 0) == 0)
            ResetSensorsAsync().Forget("reset LibreHardwareMonitor sensors");
    }

    private async Task ResetSensorsAsync()
    {
        try
        {
            await Task.Run(_hardware.ResetSensors).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _resetInFlight, 0);
        }
    }

    #endregion

    #region Hardware Refresh

    public void NeedRefreshHardware(string hardwareId)
    {
        _hardware.NeedRefreshHardware(hardwareId);
    }

    #endregion

    #region Status Helpers

    public bool IsGpuInactive(GPUState state) =>
        state is GPUState.Inactive or GPUState.PoweredOff or GPUState.Unknown or GPUState.NvidiaGpuNotFound;

    public bool IsLibreHardwareMonitorInitialized() =>
        InitialState is LibreHardwareMonitorInitialState.Initialized or LibreHardwareMonitorInitialState.Success;

    /// <summary>
    /// Close LibreHardwareMonitor while the UI is tray-only. Chart polling is
    /// already stopped; this releases the native Computer graph. Re-open via
    /// <see cref="EnsureHardwareAfterBackgroundAsync"/> before UI resume.
    /// </summary>
    public void ReleaseHardwareForBackground()
    {
        lock (_subscribers)
            CancelProducerLoop();

        _hardware.CloseHardwareForUpgrade();
        _initialized = false;
        InitialState = LibreHardwareMonitorInitialState.Fail;
        lock (_dataLock)
        {
            _cachedCpuName = string.Empty;
            _cachedGpuName = string.Empty;
        }
    }

    public Task<LibreHardwareMonitorInitialState> EnsureHardwareAfterBackgroundAsync(
        CancellationToken cancellationToken = default) =>
        InitializeAsync(
            HardwareDiscoveryService.InitializationMode.Full,
            cancellationToken,
            persistFailureToSettings: false);

    #endregion

    #region Producer / Subscriber

    public void Start(object subscriber, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        lock (_subscribers)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            _subscribers[subscriber] = interval;
            EnsureProducerLoop();
        }
    }

    public void Stop(object subscriber)
    {
        if (subscriber is null)
            return;

        lock (_subscribers)
        {
            if (_subscribers.Remove(subscriber))
                EnsureProducerLoop();
        }
    }

    private void EnsureProducerLoop()
    {
        if (Volatile.Read(ref _disposed) != 0 || _subscribers.Count == 0)
        {
            CancelProducerLoop();
            return;
        }

        // The loop re-reads the minimum interval each tick. Restarting on every
        // Start/Stop races the old loop against a disposed CancellationTokenSource.
        if (_producerCts is { IsCancellationRequested: false })
            return;

        CancelProducerLoop();
        _producerCts = new CancellationTokenSource();
        var token = _producerCts.Token;
        _ = Task.Run(() => ProducerLoop(token), token);
    }

    private void CancelProducerLoop()
    {
        var cts = _producerCts;
        _producerCts = null;
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ProducerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TimeSpan minInterval;
            lock (_subscribers)
            {
                if (_subscribers.Count == 0) return;
                minInterval = _subscribers.Values.Min();
            }

            try
            {
                await UpdateAsync().ConfigureAwait(false);
                _producerLoopErrorLogged = false;
                SensorsUpdated?.Invoke(this, EventArgs.Empty);

                await _delayProvider.Delay(minInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled && !_producerLoopErrorLogged)
                {
                    Log.Instance.Trace($"ProducerLoop error: {ex}");
                    _producerLoopErrorLogged = true;
                }

                await _delayProvider.Delay(TimeSpan.FromMilliseconds(1000), token).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region Static Forwarding (backward compatibility for tests)

    internal static IEnumerable<IHardware> EnumerateHardwareTree(IEnumerable<IHardware> hardware) =>
        HardwareDiscoveryService.EnumerateHardwareTree(hardware);

    internal static float ResolveCpuPower(float packagePower, IEnumerable<float> componentPowers) =>
        SensorSnapshotStore.ResolveCpuPower(packagePower, componentPowers);

    internal static (float cores, float memory, float platform) ResolveCpuComponentPowers(
        IEnumerable<(string name, float value)> components) =>
        SensorSnapshotStore.ResolveCpuComponentPowers(components);

    internal static float ResolveGpuPower(float currentPower, float previousPower) =>
        SensorSnapshotStore.ResolveGpuPower(currentPower, previousPower);

    internal static (float used, float total, float utilization) ResolveGpuVramMetrics(
        float used, float total, float free) =>
        SensorSnapshotStore.ResolveGpuVramMetrics(used, total, free);

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        lock (_subscribers)
        {
            _subscribers.Clear();
            CancelProducerLoop();
        }

        SensorsUpdated = null;
        _hardware.Close();
        _initialized = false;
        _initSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
