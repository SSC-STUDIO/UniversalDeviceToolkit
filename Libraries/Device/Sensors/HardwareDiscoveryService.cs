// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) RAMSPDToolkit and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using LibreHardwareMonitor.Hardware;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Hardware discovery and sensor cache management.
/// Responsible for enumerating hardware, selecting sensors, and managing hardware lifecycle.
/// </summary>
internal sealed class HardwareDiscoveryService
{
    internal enum InitializationMode
    {
        None,
        FanOnly,
        Full
    }

    private const float INVALID_VALUE_FLOAT = -1f;
    private const string UNKNOWN_NAME = "UNKNOWN";
    private const string HARDWARE_ID_NVIDIA_GPU = "NvidiaGPU";

    private readonly Lock _hardwareLock = new();
    internal volatile bool IsResetting;
    private bool _hardwareInitialized;

    private Computer? _computer;
    private InitializationMode _initializationMode;
    internal bool IsInitialized => _hardwareInitialized;
    internal InitializationMode Mode => _initializationMode;

    private readonly List<IHardware> _hardware = [];
    internal int HardwareCount
    {
        get { lock (_hardwareLock) return _hardware.Count; }
    }

    #region Hardware References

    internal IHardware? CpuHardware;
    internal IHardware? AmdGpuHardware;
    internal IHardware? GpuHardware;
    internal IHardware? IGpuHardware;
    private IHardware? _memoryHardware;

    #endregion

    #region Sensor References

    internal ISensor? CpuTempSensor;
    internal ISensor? CpuUsageSensor;
    internal ISensor? CpuCoreVoltageSensor;
    internal ISensor? CpuFanSensor;
    internal ISensor? GpuFanSensor;
    internal List<ISensor> AllFanSensors = [];

    internal ISensor? GpuUsageSensor;
    internal ISensor? GpuTempSensor;
    internal ISensor? GpuClockSensor;
    internal ISensor? GpuMemoryClockSensor;
    internal ISensor? GpuCoreVoltageSensor;
    internal ISensor? IGpuCoreVoltageSensor;

    internal ISensor? IGpuUsageSensor;
    internal ISensor? IGpuTempSensor;
    internal ISensor? IGpuClockSensor;
    internal ISensor? IGpuMemoryClockSensor;
    internal ISensor? IGpuPowerSensor;

    internal ISensor? GpuD3DVramUsedSensor;
    internal ISensor? GpuVramTotalSensor;
    internal ISensor? GpuVramFreeSensor;
    internal ISensor? GpuPcieRxSensor;
    internal ISensor? GpuPcieTxSensor;
    internal float CachedGpuVramTotal = INVALID_VALUE_FLOAT;

    internal ISensor? IGpuD3DVramUsedSensor;
    internal ISensor? IGpuVramTotalSensor;
    internal ISensor? IGpuVramFreeSensor;
    internal ISensor? IGpuPcieRxSensor;
    internal ISensor? IGpuPcieTxSensor;
    internal float CachedIGpuVramTotal = INVALID_VALUE_FLOAT;

    internal readonly List<ISensor> PClockSensors = [];
    internal readonly List<ISensor> EClockSensors = [];
    internal ISensor? CpuPackagePowerSensor;
    internal readonly List<ISensor> CpuComponentPowerSensors = [];
    internal readonly List<ISensor> CpuCoreClockSensors = [];

    internal ISensor? GpuPowerSensor;
    internal ISensor? GpuVramTemperatureSensor;
    internal ISensor? GpuHotSpotSensor;

    internal ISensor? MemoryLoadSensor;
    internal ISensor? MemoryUsedSensor;
    internal ISensor? MemoryAvailableSensor;
    internal float CachedMemoryTotal = INVALID_VALUE_FLOAT;
    internal readonly List<ISensor> MemoryTempSensors = [];
    internal readonly List<ISensor> MotherboardTempSensors = [];
    internal readonly List<ISensor> StorageTempSensors = [];

    #endregion

    #region Hardware Lifecycle

    internal void GetHardware(InitializationMode mode)
    {
        lock (_hardwareLock)
        {
            if (_hardwareInitialized) return;

            var pawnIoInstalled = PawnIOHelper.IsPawnIOInstalled();
            if (!pawnIoInstalled && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("PawnIO not detected; attempting LibreHardwareMonitor initialization without it.");

            try
            {
                var fanOnly = mode == InitializationMode.FanOnly;
                _computer = new Computer
                {
                    IsCpuEnabled = !fanOnly,
                    IsGpuEnabled = !fanOnly,
                    IsMemoryEnabled = !fanOnly,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = !fanOnly
                };

                _computer.Open();
                _computer.Accept(new UpdateVisitor());
                _hardware.AddRange(EnumerateHardwareTree(_computer.Hardware));
                RefreshSensorCache();
                _initializationMode = mode;
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"GetHardware failed: {ex}");
                _computer?.Close();
                _computer = null;
                _hardware.Clear();
                throw;
            }
            finally { _hardwareInitialized = _computer is not null && _hardware.Count > 0; }
        }
    }

    internal void CloseHardwareForUpgrade()
    {
        lock (_hardwareLock)
        {
            _computer?.Close();
            _computer = null;
            _hardware.Clear();
            _hardwareInitialized = false;
            _initializationMode = InitializationMode.None;
            RefreshSensorCache();
        }
    }

    internal void ResetSensors()
    {
        IsResetting = true;
        try
        {
            lock (_hardwareLock)
            {
                if (_computer == null) return;

                _computer.Close();
                _hardware.Clear();
                _computer.Open();
                _computer.Accept(new UpdateVisitor());
                _computer.Reset();
                _hardware.AddRange(EnumerateHardwareTree(_computer.Hardware));
                RefreshSensorCache();
            }
        }
        finally { IsResetting = false; }
    }

    internal void NeedRefreshHardware(string hardwareId)
    {
        if (!_hardwareInitialized || _computer == null || hardwareId != HARDWARE_ID_NVIDIA_GPU) return;

        // ResetSensors acquires _hardwareLock. System.Threading.Lock is not reentrant,
        // so calling it while already holding that lock deadlocks the sensor pipeline.
        ResetSensors();

        lock (_hardwareLock)
        {
            try { NVAPI.Initialize(); }
            catch
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Failed to initialize NVAPI");
            }

            _needRefreshGpuHardware = true;
        }
    }

    internal void Close()
    {
        lock (_hardwareLock)
        {
            _computer?.Close();
            _computer = null;
            _hardwareInitialized = false;
            _initializationMode = InitializationMode.None;
        }
    }

    internal void ClearDiscreteGpuHardware()
    {
        lock (_hardwareLock)
        {
            GpuHardware = null;
            AmdGpuHardware = null;
        }
    }

    #endregion

    #region Hardware Update

    /// <summary>
    /// Updates all hardware, then processes sensor readings into the snapshot store.
    /// Acquires hardwareLock internally; the snapshot store acquires dataLock inside (correct nesting).
    /// </summary>
    /// <returns>True if ResetSensors should be triggered.</returns>
    internal bool UpdateHardwareAndSnapshots(
        SensorSnapshotStore snapshotStore,
        bool gpuInactive,
        bool shouldUseIntegratedGpu,
        bool isHybrid,
        bool showAverageCpuFrequency)
    {
        lock (_hardwareLock)
        {
            if (IsResetting || _computer == null || !_hardwareInitialized) return false;
            try
            {
                foreach (var h in _hardware)
                {
                    if (h == null) continue;
                    // Hybrid laptops idle the discrete GPU in Optimus mode - keep sampling
                    // it anyway: temperature / VRAM / clocks remain valid readings that the
                    // console should keep showing instead of blanking the whole GPU panel.
                    h.Update();
                }

                // Resolve fan sensors (modifies CpuFanSensor/GpuFanSensor references)
                float cpuFan = ResolveFanSnapshot(CpuFanSensor, preferGpu: false);
                float gpuFan = ResolveFanSnapshot(GpuFanSensor, preferGpu: true);

                // Collect component power readings
                var cpuComponentReadings = CpuComponentPowerSensors
                    .Select(sensor => (sensor.Name, value: sensor.Value ?? INVALID_VALUE_FLOAT))
                    .ToList();

                float cachedGpuVram = CachedGpuVramTotal;
                float cachedIGpuVram = CachedIGpuVramTotal;

                bool needReset = snapshotStore.UpdateSnapshots(
                    // CPU
                    CpuTempSensor?.Value, CpuUsageSensor?.Value, CpuCoreVoltageSensor?.Value,
                    cpuFan, gpuFan,
                    CpuCoreClockSensors, isHybrid,
                    PClockSensors, EClockSensors,
                    CpuPackagePowerSensor?.Value, cpuComponentReadings,
                    // GPU selection
                    shouldUseIntegratedGpu, gpuInactive,
                    // iGPU
                    ToGigabytes(IGpuVramTotalSensor), ToGigabytes(IGpuD3DVramUsedSensor), ToGigabytes(IGpuVramFreeSensor),
                    IGpuPowerSensor?.Value, IGpuUsageSensor?.Value, IGpuTempSensor?.Value,
                    IGpuCoreVoltageSensor?.Value,
                    IGpuClockSensor?.Value, IGpuMemoryClockSensor?.Value,
                    IGpuPcieRxSensor?.Value, IGpuPcieTxSensor?.Value,
                    // dGPU
                    ToGigabytes(GpuVramTotalSensor), ToGigabytes(GpuD3DVramUsedSensor), ToGigabytes(GpuVramFreeSensor),
                    GpuPowerSensor?.Value, GpuCoreVoltageSensor?.Value,
                    GpuVramTemperatureSensor?.Value, GpuHotSpotSensor?.Value,
                    GpuUsageSensor?.Value, GpuTempSensor?.Value,
                    GpuClockSensor?.Value, GpuMemoryClockSensor?.Value,
                    GpuPcieRxSensor?.Value, GpuPcieTxSensor?.Value,
                    // Memory
                    MemoryLoadSensor?.Value, MemoryUsedSensor?.Value, MemoryAvailableSensor?.Value,
                    MemoryTempSensors, MotherboardTempSensors, StorageTempSensors,
                    ref cachedGpuVram, ref cachedIGpuVram,
                    CachedMemoryTotal);

                CachedGpuVramTotal = cachedGpuVram;
                CachedIGpuVramTotal = cachedIGpuVram;

                return needReset;
            }
            catch (Exception ex)
            {
                if (ex is IndexOutOfRangeException) return true;
                return false;
            }
        }
    }

    #endregion

    #region Sensor Cache

    private bool _needRefreshGpuHardware;
    internal bool NeedRefreshGpuHardware
    {
        get => _needRefreshGpuHardware;
        set => _needRefreshGpuHardware = value;
    }

    internal bool IsHybrid { get; private set; }

    private void RefreshSensorCache()
    {
        CpuHardware = null;
        AmdGpuHardware = null;
        GpuHardware = null;
        _memoryHardware = null;
        CpuTempSensor = null;
        CpuUsageSensor = null;
        CpuCoreVoltageSensor = null;
        CpuFanSensor = null;
        GpuFanSensor = null;
        GpuUsageSensor = null;
        GpuTempSensor = null;
        GpuClockSensor = null;
        GpuMemoryClockSensor = null;
        GpuCoreVoltageSensor = null;
        IGpuCoreVoltageSensor = null;

        IGpuUsageSensor = null;
        IGpuTempSensor = null;
        IGpuClockSensor = null;
        IGpuMemoryClockSensor = null;
        IGpuPowerSensor = null;

        GpuD3DVramUsedSensor = null;
        GpuVramTotalSensor = null;
        GpuVramFreeSensor = null;
        GpuPcieRxSensor = null;
        GpuPcieTxSensor = null;
        CachedGpuVramTotal = INVALID_VALUE_FLOAT;

        IGpuD3DVramUsedSensor = null;
        IGpuVramTotalSensor = null;
        IGpuVramFreeSensor = null;
        IGpuPcieRxSensor = null;
        IGpuPcieTxSensor = null;
        CachedIGpuVramTotal = INVALID_VALUE_FLOAT;

        PClockSensors.Clear();
        EClockSensors.Clear();
        CpuCoreClockSensors.Clear();
        CpuComponentPowerSensors.Clear();
        MemoryTempSensors.Clear();
        MotherboardTempSensors.Clear();
        StorageTempSensors.Clear();

        CpuPackagePowerSensor = null;
        GpuPowerSensor = null;
        GpuVramTemperatureSensor = null;
        GpuHotSpotSensor = null;
        MemoryLoadSensor = null;
        MemoryUsedSensor = null;
        MemoryAvailableSensor = null;
        CachedMemoryTotal = INVALID_VALUE_FLOAT;

        IsHybrid = false;

        CpuHardware = _hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        AmdGpuHardware = _hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuAmd && !SensorPreferenceCatalog.RegexAmdGpuIntegrated.IsMatch(h.Name));
        IGpuHardware = _hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel || (h.HardwareType == HardwareType.GpuAmd && SensorPreferenceCatalog.RegexAmdGpuIntegrated.IsMatch(h.Name)));
        GpuHardware = _hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia);
        _memoryHardware = SensorSelector.SelectMemoryHardware(_hardware);

        if (CpuHardware?.Sensors != null)
        {
            foreach (var s in CpuHardware.Sensors)
            {
                switch (s.SensorType)
                {
                    case SensorType.Temperature when s.Name.Contains("Package"):
                        CpuTempSensor = s;
                        break;
                    case SensorType.Load when SensorSelector.IsLikelyCpuUsageSensorName(s.Name):
                        CpuUsageSensor = s;
                        break;
                    case SensorType.Voltage when SensorSelector.IsLikelyCpuVoltageSensorName(s.Name):
                        CpuCoreVoltageSensor ??= s;
                        break;
                    case SensorType.Clock when SensorSelector.IsLikelyCpuPCoreClockSensorName(s.Name):
                        PClockSensors.Add(s);
                        CpuCoreClockSensors.Add(s);
                        break;
                    case SensorType.Clock when SensorSelector.IsLikelyCpuECoreClockSensorName(s.Name):
                        EClockSensors.Add(s);
                        CpuCoreClockSensors.Add(s);
                        break;
                    case SensorType.Clock when s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && !s.Name.Contains("Average") && !s.Name.Contains("Effective"):
                        CpuCoreClockSensors.Add(s);
                        break;
                    case SensorType.Power when SensorSelector.IsLikelyCpuPackagePowerSensorName(s.Name):
                        CpuPackagePowerSensor ??= s;
                        break;
                    case SensorType.Power when SensorSelector.IsLikelyCpuComponentPowerSensorName(s.Name):
                        CpuComponentPowerSensors.Add(s);
                        break;
                }
            }
            IsHybrid = PClockSensors.Count > 0 || EClockSensors.Count > 0;
            CpuTempSensor ??= SensorSelector.SelectCpuTemperatureSensor(CpuHardware.Sensors);
            CpuUsageSensor ??= SensorSelector.SelectCpuUsageSensor(CpuHardware.Sensors);
            CpuCoreVoltageSensor ??= SensorSelector.SelectCpuVoltageSensor(CpuHardware.Sensors);
            CpuPackagePowerSensor ??= SensorSelector.SelectCpuPackagePowerSensor(CpuHardware.Sensors);
        }

        var mainGpu = GpuHardware ?? AmdGpuHardware;
        if (mainGpu?.Sensors != null)
        {
            foreach (var s in mainGpu.Sensors)
            {
                switch (s.SensorType)
                {
                    case SensorType.Load when SensorSelector.IsLikelyGpuUsageSensorName(s.Name):
                        GpuUsageSensor = s;
                        break;
                    case SensorType.Temperature when SensorSelector.IsLikelyGpuTemperatureSensorName(s.Name):
                        GpuTempSensor = s;
                        break;
                    case SensorType.Clock when SensorSelector.IsLikelyGpuCoreClockSensorName(s.Name):
                        GpuClockSensor = s;
                        break;
                    case SensorType.Clock when SensorSelector.IsLikelyGpuMemoryClockSensorName(s.Name):
                        GpuMemoryClockSensor ??= s;
                        break;
                    case SensorType.Power when SensorSelector.IsLikelyGpuPowerSensorName(s.Name):
                        GpuPowerSensor ??= s;
                        break;
                    case SensorType.Voltage when SensorSelector.IsLikelyGpuVoltageSensorName(s.Name):
                        GpuCoreVoltageSensor ??= s;
                        break;
                    case SensorType.Temperature when SensorSelector.IsLikelyGpuHotSpotTemperatureSensorName(s.Name):
                        GpuHotSpotSensor ??= s;
                        break;
                    case SensorType.Temperature when SensorSelector.IsLikelyGpuVramTemperatureSensorName(s.Name):
                        GpuVramTemperatureSensor ??= s;
                        break;
                    case SensorType.SmallData or SensorType.Data when SensorSelector.IsLikelyGpuVramUsedSensorName(s.Name):
                        GpuD3DVramUsedSensor = s;
                        break;
                    case SensorType.SmallData or SensorType.Data when SensorSelector.IsLikelyGpuVramTotalSensorName(s.Name):
                        GpuVramTotalSensor = s;
                        break;
                    case SensorType.SmallData or SensorType.Data when SensorSelector.IsLikelyGpuVramFreeSensorName(s.Name):
                        GpuVramFreeSensor = s;
                        break;
                    case SensorType.Throughput when SensorSelector.IsLikelyGpuPcieRxThroughputSensorName(s.Name):
                        GpuPcieRxSensor ??= s;
                        break;
                    case SensorType.Throughput when SensorSelector.IsLikelyGpuPcieTxThroughputSensorName(s.Name):
                        GpuPcieTxSensor ??= s;
                        break;
                }
            }
            GpuUsageSensor ??= SensorSelector.SelectGpuUsageSensor(mainGpu.Sensors);
            GpuTempSensor ??= SensorSelector.SelectGpuTemperatureSensor(mainGpu.Sensors);
            GpuClockSensor ??= SensorSelector.SelectGpuCoreClockSensor(mainGpu.Sensors);
            GpuMemoryClockSensor ??= SensorSelector.SelectGpuMemoryClockSensor(mainGpu.Sensors);
            GpuPowerSensor ??= SensorSelector.SelectGpuPowerSensor(mainGpu.Sensors);
            GpuCoreVoltageSensor ??= SensorSelector.SelectGpuVoltageSensor(mainGpu.Sensors);
            GpuHotSpotSensor ??= SensorSelector.SelectGpuHotSpotTemperatureSensor(mainGpu.Sensors);
            GpuVramTemperatureSensor ??= SensorSelector.SelectGpuVramTemperatureSensor(mainGpu.Sensors);
            GpuD3DVramUsedSensor ??= SensorSelector.SelectGpuVramUsedSensor(mainGpu.Sensors);
            GpuVramTotalSensor ??= SensorSelector.SelectGpuVramTotalSensor(mainGpu.Sensors);
            GpuVramFreeSensor ??= SensorSelector.SelectGpuVramFreeSensor(mainGpu.Sensors);
            GpuPcieRxSensor ??= SensorSelector.SelectGpuPcieRxThroughputSensor(mainGpu.Sensors);
            GpuPcieTxSensor ??= SensorSelector.SelectGpuPcieTxThroughputSensor(mainGpu.Sensors);
        }

        if (IGpuHardware?.Sensors != null)
        {
            foreach (var s in IGpuHardware.Sensors)
            {
                switch (s.SensorType)
                {
                    case SensorType.Load when SensorSelector.IsLikelyGpuUsageSensorName(s.Name):
                        IGpuUsageSensor = s;
                        break;
                    case SensorType.Temperature when SensorSelector.IsLikelyGpuTemperatureSensorName(s.Name):
                        IGpuTempSensor = s;
                        break;
                    case SensorType.Clock when SensorSelector.IsLikelyGpuCoreClockSensorName(s.Name):
                        IGpuClockSensor = s;
                        break;
                    case SensorType.Clock when SensorSelector.IsLikelyGpuMemoryClockSensorName(s.Name):
                        IGpuMemoryClockSensor ??= s;
                        break;
                    case SensorType.Power when SensorSelector.IsLikelyGpuPowerSensorName(s.Name):
                        IGpuPowerSensor ??= s;
                        break;
                    case SensorType.Voltage when SensorSelector.IsLikelyGpuVoltageSensorName(s.Name):
                        IGpuCoreVoltageSensor ??= s;
                        break;
                    case SensorType.SmallData or SensorType.Data when SensorSelector.IsLikelyGpuVramUsedSensorName(s.Name):
                        IGpuD3DVramUsedSensor = s;
                        break;
                    case SensorType.SmallData or SensorType.Data when SensorSelector.IsLikelyGpuVramTotalSensorName(s.Name):
                        IGpuVramTotalSensor = s;
                        break;
                    case SensorType.SmallData or SensorType.Data when SensorSelector.IsLikelyGpuVramFreeSensorName(s.Name):
                        IGpuVramFreeSensor = s;
                        break;
                    case SensorType.Throughput when SensorSelector.IsLikelyGpuPcieRxThroughputSensorName(s.Name):
                        IGpuPcieRxSensor ??= s;
                        break;
                    case SensorType.Throughput when SensorSelector.IsLikelyGpuPcieTxThroughputSensorName(s.Name):
                        IGpuPcieTxSensor ??= s;
                        break;
                }
            }
            IGpuUsageSensor ??= SensorSelector.SelectGpuUsageSensor(IGpuHardware.Sensors);
            IGpuTempSensor ??= SensorSelector.SelectGpuTemperatureSensor(IGpuHardware.Sensors);
            IGpuClockSensor ??= SensorSelector.SelectGpuCoreClockSensor(IGpuHardware.Sensors);
            IGpuMemoryClockSensor ??= SensorSelector.SelectGpuMemoryClockSensor(IGpuHardware.Sensors);
            IGpuPowerSensor ??= SensorSelector.SelectGpuPowerSensor(IGpuHardware.Sensors);
            IGpuCoreVoltageSensor ??= SensorSelector.SelectGpuVoltageSensor(IGpuHardware.Sensors);
            IGpuD3DVramUsedSensor ??= SensorSelector.SelectGpuVramUsedSensor(IGpuHardware.Sensors);
            IGpuVramTotalSensor ??= SensorSelector.SelectGpuVramTotalSensor(IGpuHardware.Sensors);
            IGpuVramFreeSensor ??= SensorSelector.SelectGpuVramFreeSensor(IGpuHardware.Sensors);
            IGpuPcieRxSensor ??= SensorSelector.SelectGpuPcieRxThroughputSensor(IGpuHardware.Sensors);
            IGpuPcieTxSensor ??= SensorSelector.SelectGpuPcieTxThroughputSensor(IGpuHardware.Sensors);
        }

        MemoryLoadSensor = _memoryHardware?.Sensors is null ? null : SensorSelector.SelectMemoryLoadSensor(_memoryHardware.Sensors);
        MemoryUsedSensor = _memoryHardware?.Sensors is null ? null : SensorSelector.SelectMemoryUsedSensor(_memoryHardware.Sensors);
        MemoryAvailableSensor = _memoryHardware?.Sensors is null ? null : SensorSelector.SelectMemoryAvailableSensor(_memoryHardware.Sensors);

        foreach (var hw in _hardware.Where(h => h.HardwareType == HardwareType.Memory))
        {
            if (hw.Sensors == null) continue;
            MemoryTempSensors.AddRange(SensorSelector.SelectMemoryTemperatureSensors(hw.Sensors, requireMemoryKeywords: false));
        }

        foreach (var hw in _hardware.Where(SensorSelector.IsBoardTemperatureHardware))
        {
            if (hw.Sensors == null) continue;
            MemoryTempSensors.AddRange(SensorSelector.SelectMemoryTemperatureSensors(hw.Sensors, requireMemoryKeywords: true));
            MotherboardTempSensors.AddRange(SensorSelector.SelectMotherboardTemperatureSensors(hw.Sensors));
        }

        foreach (var storage in _hardware.Where(h => h.HardwareType == HardwareType.Storage))
        {
            var temp = storage.Sensors is null ? null : SensorSelector.SelectStorageTemperatureSensor(storage.Sensors);
            if (temp != null) StorageTempSensors.Add(temp);
        }

        var fanSensors = _hardware
            .Where(h => h.Sensors is not null)
            .SelectMany(h => h.Sensors!)
            .Where(s => s.SensorType == SensorType.Fan)
            .ToList();
        AllFanSensors = fanSensors;

        if (mainGpu?.Sensors is not null)
        {
            GpuFanSensor = SensorSelector.SelectGpuFanSensor(mainGpu.Sensors);
        }

        CpuFanSensor = SensorSelector.SelectCpuFanSensor(fanSensors);
        GpuFanSensor ??= SensorSelector.SelectGpuFanSensor(fanSensors);
        CpuFanSensor ??= fanSensors
            .Where(s => s.Value is > 0)
            .OrderByDescending(s => s.Value)
            .FirstOrDefault();

        // Cache memory total for fallback calculations
        if (MemoryUsedSensor != null && MemoryAvailableSensor != null)
        {
            var used = MemoryUsedSensor.Value ?? INVALID_VALUE_FLOAT;
            var avail = MemoryAvailableSensor.Value ?? INVALID_VALUE_FLOAT;
            if (used >= 0 && avail >= 0)
                CachedMemoryTotal = used + avail;
        }

        if (Log.Instance.IsTraceEnabled)
        {
            var hardwareSummary = string.Join(", ", _hardware.Select(h => $"{h.HardwareType}:{h.Name}"));
            Log.Instance.Trace($"LibreHardwareMonitor hardware summary: [{hardwareSummary}]");
            Log.Instance.Trace($"LibreHardwareMonitor CPU temperature sensor: {(CpuTempSensor is null ? "not found" : CpuTempSensor.Name)}");
            Log.Instance.Trace($"LibreHardwareMonitor CPU package power sensor: {(CpuPackagePowerSensor is null ? "not found" : CpuPackagePowerSensor.Name)}");
            Log.Instance.Trace($"LibreHardwareMonitor CPU fan sensor: {(CpuFanSensor is null ? "not found" : CpuFanSensor.Name)}");
            Log.Instance.Trace($"LibreHardwareMonitor GPU fan sensor: {(GpuFanSensor is null ? "not found" : GpuFanSensor.Name)}");
        }
    }

    #endregion

    #region Fan Resolution

    private float ResolveFanSnapshot(ISensor? preferred, bool preferGpu)
    {
        var preferredValue = preferred?.Value;
        if (preferredValue is > 0)
        {
            if (preferGpu)
                GpuFanSensor = preferred;
            else
                CpuFanSensor = preferred;
            return preferredValue.Value;
        }

        Func<string, int> score = preferGpu ? SensorSelector.ScoreGpuFanName : SensorSelector.ScoreCpuFanName;
        var best = AllFanSensors
            .Where(sensor => sensor.SensorType == SensorType.Fan && sensor.Value is > 0)
            .OrderByDescending(sensor => score(sensor.Name))
            .ThenByDescending(sensor => sensor.Value ?? 0f)
            .FirstOrDefault(sensor => score(sensor.Name) > 0)
            ?? AllFanSensors
                .Where(sensor => sensor.SensorType == SensorType.Fan && sensor.Value is > 0)
                .OrderByDescending(sensor => sensor.Value ?? 0f)
                .FirstOrDefault();

        var bestValue = best?.Value;
        if (bestValue is > 0)
        {
            if (preferGpu)
                GpuFanSensor = best;
            else
                CpuFanSensor = best;
            return bestValue.Value;
        }

        if (preferredValue is >= 0)
            return preferredValue.Value;

        return INVALID_VALUE_FLOAT;
    }

    #endregion

    #region Static Helpers

    internal static IEnumerable<IHardware> EnumerateHardwareTree(IEnumerable<IHardware> hardware)
    {
        foreach (var item in hardware)
        {
            if (item is null) continue;
            yield return item;
            foreach (var child in EnumerateHardwareTree(item.SubHardware ?? []))
                yield return child;
        }
    }

    internal static float? ToGigabytes(ISensor? sensor)
    {
        if (sensor?.Value is not { } value)
            return null;

        return SensorReadingHelper.NormalizeLibreHardwareMonitorDataToGigabytes(
            value,
            sensor.SensorType == SensorType.SmallData);
    }

    internal static string StripName(string name)
    {
        if (string.IsNullOrEmpty(name)) return UNKNOWN_NAME;
        var cleaned = name.Trim();
        if (cleaned.Contains("AMD", StringComparison.OrdinalIgnoreCase))
            cleaned = SensorPreferenceCatalog.RegexStripAmd.Replace(cleaned, "");
        else if (cleaned.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            cleaned = SensorPreferenceCatalog.RegexStripIntel.Replace(cleaned, "");
        else if (cleaned.Contains("Nvidia", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("GeForce", StringComparison.OrdinalIgnoreCase))
        {
            var m = SensorPreferenceCatalog.RegexStripNvidia.Match(cleaned);
            if (m.Success) cleaned = m.Groups[1].Value;
        }
        return SensorPreferenceCatalog.RegexCleanSpaces.Replace(cleaned, " ").Trim();
    }

    #endregion
}
