// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) RAMSPDToolkit and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Thread-safe store for sensor snapshot data.
/// Manages volatile snapshot fields and provides async access methods.
/// </summary>
internal sealed class SensorSnapshotStore
{
    private const float INVALID_VALUE_FLOAT = -1f;
    private const double INVALID_VALUE_DOUBLE = 0.0;
    private const float MAX_VALID_CPU_POWER = 400f;
    private const float MIN_VALID_POWER_READING = 0f;
    private const int MAX_CPU_POWER_STUCK_RETRIES = 10;

    private readonly Lock _dataLock = new();

    #region Snapshot Fields

    private float _snapshotCpuTemp = INVALID_VALUE_FLOAT;
    private float _snapshotCpuUsage = INVALID_VALUE_FLOAT;
    private float _snapshotCpuFan = INVALID_VALUE_FLOAT;
    private float _snapshotGpuFan = INVALID_VALUE_FLOAT;
    private float _snapshotCpuPower = INVALID_VALUE_FLOAT;
    private float _snapshotCpuCoresPower = INVALID_VALUE_FLOAT;
    private float _snapshotCpuMemoryPower = INVALID_VALUE_FLOAT;
    private float _snapshotCpuPlatformPower = INVALID_VALUE_FLOAT;
    private float _snapshotCpuVoltage = INVALID_VALUE_FLOAT;
    private float _snapshotCpuMaxClock = INVALID_VALUE_FLOAT;
    private float _snapshotCpuAvgClock = INVALID_VALUE_FLOAT;
    private float _snapshotCpuPClock = INVALID_VALUE_FLOAT;
    private float _snapshotCpuEClock = INVALID_VALUE_FLOAT;
    private float _snapshotCpuPAvgClock = INVALID_VALUE_FLOAT;
    private float _snapshotCpuEAvgClock = INVALID_VALUE_FLOAT;
    private float _snapshotGpuUsage = INVALID_VALUE_FLOAT;
    private float _snapshotGpuTemp = INVALID_VALUE_FLOAT;
    private float _snapshotGpuClock = INVALID_VALUE_FLOAT;
    private float _snapshotGpuMemoryClock = INVALID_VALUE_FLOAT;
    private float _snapshotGpuPower = INVALID_VALUE_FLOAT;
    private float _snapshotGpuVoltage = INVALID_VALUE_FLOAT;
    private float _snapshotGpuVramTemp = INVALID_VALUE_FLOAT;
    private float _snapshotGpuHotSpotTemp = INVALID_VALUE_FLOAT;
    private float _snapshotGpuVramUsage = INVALID_VALUE_FLOAT;
    private float _snapshotGpuVramUtilization = INVALID_VALUE_FLOAT;
    private float _snapshotGpuPcieRxThroughput = INVALID_VALUE_FLOAT;
    private float _snapshotGpuPcieTxThroughput = INVALID_VALUE_FLOAT;
    private float _snapshotMemUsage = INVALID_VALUE_FLOAT;
    private float _snapshotMemUsed = INVALID_VALUE_FLOAT;
    private float _snapshotMemTotal = INVALID_VALUE_FLOAT;
    private double _snapshotMemMaxTemp = INVALID_VALUE_DOUBLE;
    private double _snapshotMotherboardMaxTemp = INVALID_VALUE_DOUBLE;
    private (float, float) _snapshotSsdTemps = (INVALID_VALUE_FLOAT, INVALID_VALUE_FLOAT);

    #endregion

    #region Snapshot Processing State

    private float _cachedCpuPower;
    private int _cachedCpuPowerTime;
    private float _lastGpuPower;

    #endregion

    #region Async Getters

    internal Task<float> GetCpuTemperatureAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotCpuTemp);
    }

    internal Task<float> GetCpuUsageAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotCpuUsage);
    }

    internal Task<float> GetCpuFanSpeedAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotCpuFan);
    }

    internal Task<float> GetGpuFanSpeedAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuFan);
    }

    internal Task<float> GetGpuUsageAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuUsage);
    }

    internal Task<float> GetGpuTemperatureAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuTemp);
    }

    internal Task<float> GetGpuCoreClockAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuClock);
    }

    internal Task<float> GetGpuMemoryClockAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuMemoryClock);
    }

    internal Task<float> GetCpuPowerAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotCpuPower);
    }

    internal Task<(float cores, float memory, float platform)> GetCpuComponentPowersAsync()
    {
        lock (_dataLock) return Task.FromResult((_snapshotCpuCoresPower, _snapshotCpuMemoryPower, _snapshotCpuPlatformPower));
    }

    internal Task<float> GetCpuVoltageAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotCpuVoltage);
    }

    internal Task<float> GetCpuCoreClockAsync(bool showAverage)
    {
        lock (_dataLock) return Task.FromResult(showAverage ? _snapshotCpuAvgClock : _snapshotCpuMaxClock);
    }

    internal Task<float> GetCpuPCoreClockAsync(bool showAverage)
    {
        lock (_dataLock) return Task.FromResult(showAverage ? _snapshotCpuPAvgClock : _snapshotCpuPClock);
    }

    internal Task<float> GetCpuECoreClockAsync(bool showAverage)
    {
        lock (_dataLock) return Task.FromResult(showAverage ? _snapshotCpuEAvgClock : _snapshotCpuEClock);
    }

    internal Task<float> GetGpuPowerAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuPower);
    }

    internal Task<float> GetGpuVoltageAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuVoltage);
    }

    internal Task<float> GetGpuVramTemperatureAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuVramTemp);
    }

    internal Task<float> GetGpuHotSpotTemperatureAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuHotSpotTemp);
    }

    internal Task<float> GetGpuVramUtilizationAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuVramUtilization);
    }

    internal Task<float> GetGpuVramUsedAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuVramUsage);
    }

    internal Task<float> GetGpuPcieRxThroughputAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuPcieRxThroughput);
    }

    internal Task<float> GetGpuPcieTxThroughputAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotGpuPcieTxThroughput);
    }

    internal Task<(float, float)> GetSsdTemperaturesAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotSsdTemps);
    }

    internal Task<float> GetMemoryUsageAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotMemUsage);
    }

    internal Task<float> GetMemoryUsedAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotMemUsed);
    }

    internal Task<float> GetMemoryTotalAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotMemTotal);
    }

    internal Task<double> GetHighestMemoryTemperatureAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotMemMaxTemp);
    }

    internal Task<double> GetHighestMotherboardTemperatureAsync()
    {
        lock (_dataLock) return Task.FromResult(_snapshotMotherboardMaxTemp);
    }

    #endregion

    #region Snapshot Update

    /// <summary>
    /// Processes raw sensor readings and updates all snapshot fields.
    /// Must be called while the caller holds the hardware lock (to maintain lock ordering).
    /// </summary>
    /// <returns>True if ResetSensors should be triggered.</returns>
    internal bool UpdateSnapshots(
        // CPU sensor readings
        float? cpuTemp, float? cpuUsage, float? cpuVoltage,
        float cpuFan, float gpuFan,
        List<ISensor> cpuCoreClockSensors, bool isHybrid,
        List<ISensor> pCoreClockSensors, List<ISensor> eCoreClockSensors,
        float? cpuPackagePower, List<(string name, float value)> cpuComponentReadings,
        // GPU selection
        bool useIntegratedGpu, bool gpuInactive,
        // iGPU sensor readings
        float? iGpuVramTotal, float? iGpuVramUsed, float? iGpuVramFree,
        float? iGpuPower, float? iGpuUsage, float? iGpuTemp, float? iGpuVoltage,
        float? iGpuClock, float? iGpuMemoryClock,
        float? iGpuPcieRx, float? iGpuPcieTx,
        // dGPU sensor readings
        float? gpuVramTotal, float? gpuVramUsed, float? gpuVramFree,
        float? gpuPower, float? gpuVoltage, float? gpuVramTemp, float? gpuHotSpot,
        float? gpuUsage, float? gpuTemp, float? gpuClock, float? gpuMemoryClock,
        float? gpuPcieRx, float? gpuPcieTx,
        // Memory sensor readings
        float? memLoad, float? memUsed, float? memAvailable,
        List<ISensor> memoryTempSensors, List<ISensor> motherboardTempSensors,
        List<ISensor> storageTempSensors,
        // Cached totals from hardware service
        ref float cachedGpuVramTotal, ref float cachedIGpuVramTotal,
        float cachedMemoryTotal)
    {
        lock (_dataLock)
        {
            // CPU basic
            _snapshotCpuTemp = cpuTemp ?? INVALID_VALUE_FLOAT;
            _snapshotCpuUsage = cpuUsage ?? INVALID_VALUE_FLOAT;
            _snapshotCpuVoltage = cpuVoltage ?? INVALID_VALUE_FLOAT;
            _snapshotCpuFan = cpuFan;
            _snapshotGpuFan = gpuFan;

            // CPU clocks
            if (cpuCoreClockSensors.Count > 0)
            {
                var (max, avg) = ComputeMaxAndAverage(cpuCoreClockSensors);
                _snapshotCpuMaxClock = max;
                _snapshotCpuAvgClock = avg;
            }
            else
            {
                _snapshotCpuMaxClock = INVALID_VALUE_FLOAT;
                _snapshotCpuAvgClock = INVALID_VALUE_FLOAT;
            }

            if (isHybrid)
            {
                var (pMax, pAvg) = pCoreClockSensors.Count > 0
                    ? ComputeMaxAndAverage(pCoreClockSensors)
                    : (INVALID_VALUE_FLOAT, INVALID_VALUE_FLOAT);
                var (eMax, eAvg) = eCoreClockSensors.Count > 0
                    ? ComputeMaxAndAverage(eCoreClockSensors)
                    : (INVALID_VALUE_FLOAT, INVALID_VALUE_FLOAT);

                _snapshotCpuPClock = pMax > 0 ? (float)Math.Round(pMax) : pMax;
                _snapshotCpuEClock = eMax > 0 ? (float)Math.Round(eMax) : eMax;
                _snapshotCpuPAvgClock = pAvg > 0 ? (float)Math.Round(pAvg) : pAvg;
                _snapshotCpuEAvgClock = eAvg > 0 ? (float)Math.Round(eAvg) : eAvg;
            }

            // CPU power
            var packagePower = cpuPackagePower ?? INVALID_VALUE_FLOAT;
            var cpuComponentPower = cpuComponentReadings.Select(r => r.value);
            var resolvedCpuPower = ResolveCpuPower(packagePower, cpuComponentPower);
            (_snapshotCpuCoresPower, _snapshotCpuMemoryPower, _snapshotCpuPlatformPower) =
                ResolveCpuComponentPowers(cpuComponentReadings);

            if (resolvedCpuPower > MAX_VALID_CPU_POWER)
            {
                _snapshotCpuPower = INVALID_VALUE_FLOAT;
                return true; // signal reset
            }
            else if (resolvedCpuPower <= MIN_VALID_POWER_READING)
            {
                _snapshotCpuPower = INVALID_VALUE_FLOAT;
            }
            else
            {
                if (Math.Abs(resolvedCpuPower - _cachedCpuPower) < float.Epsilon)
                {
                    if (++_cachedCpuPowerTime >= MAX_CPU_POWER_STUCK_RETRIES)
                    {
                        _snapshotCpuPower = INVALID_VALUE_FLOAT;
                        return true; // signal reset
                    }
                    else _snapshotCpuPower = resolvedCpuPower;
                }
                else
                {
                    _cachedCpuPower = resolvedCpuPower;
                    _cachedCpuPowerTime = 0;
                    _snapshotCpuPower = resolvedCpuPower;
                }
            }

            // GPU
            if (useIntegratedGpu)
            {
                var iVramTotal = iGpuVramTotal ?? cachedIGpuVramTotal;
                var iVramUsed = iGpuVramUsed ?? INVALID_VALUE_FLOAT;
                var iVramFree = iGpuVramFree ?? INVALID_VALUE_FLOAT;
                var iVramMetrics = ResolveGpuVramMetrics(iVramUsed, iVramTotal, iVramFree);

                _snapshotGpuVramUsage = iVramMetrics.used;
                _snapshotGpuVramUtilization = iVramMetrics.utilization;
                cachedIGpuVramTotal = iVramMetrics.total > 0 ? iVramMetrics.total : cachedIGpuVramTotal;

                _snapshotGpuPower = iGpuPower ?? INVALID_VALUE_FLOAT;
                _snapshotGpuUsage = iGpuUsage ?? INVALID_VALUE_FLOAT;
                _snapshotGpuTemp = iGpuTemp ?? INVALID_VALUE_FLOAT;
                _snapshotGpuVoltage = iGpuVoltage ?? INVALID_VALUE_FLOAT;
                _snapshotGpuVramTemp = INVALID_VALUE_FLOAT;
                _snapshotGpuHotSpotTemp = INVALID_VALUE_FLOAT;
                _snapshotGpuClock = iGpuClock ?? INVALID_VALUE_FLOAT;
                _snapshotGpuMemoryClock = iGpuMemoryClock ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPcieRxThroughput = iGpuPcieRx ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPcieTxThroughput = iGpuPcieTx ?? INVALID_VALUE_FLOAT;
            }
            else if (gpuInactive)
            {
                // Hybrid laptop with an idle discrete GPU (Optimus): the dGPU
                // readings (temperature, VRAM, clocks) are still valid and should
                // stay on the console - do not blank the whole panel. Each field
                // prefers the discrete value and falls back to the integrated GPU
                // only when the discrete sensor is unavailable.
                var dVramTotal = gpuVramTotal ?? cachedGpuVramTotal;
                var dVramUsed = gpuVramUsed ?? INVALID_VALUE_FLOAT;
                var dVramFree = gpuVramFree ?? INVALID_VALUE_FLOAT;
                var dVramMetrics = ResolveGpuVramMetrics(dVramUsed, dVramTotal, dVramFree);

                var iVramTotal = iGpuVramTotal ?? cachedIGpuVramTotal;
                var iVramUsed = iGpuVramUsed ?? INVALID_VALUE_FLOAT;
                var iVramFree = iGpuVramFree ?? INVALID_VALUE_FLOAT;
                var iVramMetrics = ResolveGpuVramMetrics(iVramUsed, iVramTotal, iVramFree);

                if (dVramMetrics.used >= 0 || dVramMetrics.utilization >= 0)
                {
                    _snapshotGpuVramUsage = dVramMetrics.used;
                    _snapshotGpuVramUtilization = dVramMetrics.utilization;
                    cachedGpuVramTotal = dVramMetrics.total > 0 ? dVramMetrics.total : cachedGpuVramTotal;
                }
                else
                {
                    _snapshotGpuVramUsage = iVramMetrics.used;
                    _snapshotGpuVramUtilization = iVramMetrics.utilization;
                    cachedIGpuVramTotal = iVramMetrics.total > 0 ? iVramMetrics.total : cachedIGpuVramTotal;
                }

                float gPower = gpuPower ?? iGpuPower ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPower = ResolveGpuPower(gPower, _lastGpuPower);
                if (_snapshotGpuPower > MIN_VALID_POWER_READING)
                    _lastGpuPower = _snapshotGpuPower;

                _snapshotGpuVoltage = gpuVoltage ?? iGpuVoltage ?? INVALID_VALUE_FLOAT;
                _snapshotGpuVramTemp = gpuVramTemp ?? INVALID_VALUE_FLOAT;
                _snapshotGpuHotSpotTemp = gpuHotSpot ?? INVALID_VALUE_FLOAT;
                _snapshotGpuUsage = gpuUsage ?? iGpuUsage ?? 0f;
                _snapshotGpuTemp = gpuTemp ?? iGpuTemp ?? INVALID_VALUE_FLOAT;
                _snapshotGpuClock = gpuClock ?? iGpuClock ?? 0f;
                _snapshotGpuMemoryClock = gpuMemoryClock ?? iGpuMemoryClock ?? 0f;
                _snapshotGpuPcieRxThroughput = gpuPcieRx ?? iGpuPcieRx ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPcieTxThroughput = gpuPcieTx ?? iGpuPcieTx ?? INVALID_VALUE_FLOAT;
            }
            else
            {
                var dVramTotal = gpuVramTotal ?? cachedGpuVramTotal;
                var dVramUsed = gpuVramUsed ?? INVALID_VALUE_FLOAT;
                var dVramFree = gpuVramFree ?? INVALID_VALUE_FLOAT;
                var dVramMetrics = ResolveGpuVramMetrics(dVramUsed, dVramTotal, dVramFree);

                _snapshotGpuVramUsage = dVramMetrics.used;
                _snapshotGpuVramUtilization = dVramMetrics.utilization;
                cachedGpuVramTotal = dVramMetrics.total > 0 ? dVramMetrics.total : cachedGpuVramTotal;

                float gPower = gpuPower ?? iGpuPower ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPower = ResolveGpuPower(gPower, _lastGpuPower);
                if (_snapshotGpuPower > MIN_VALID_POWER_READING)
                    _lastGpuPower = _snapshotGpuPower;
                _snapshotGpuVoltage = gpuVoltage ?? iGpuVoltage ?? INVALID_VALUE_FLOAT;
                _snapshotGpuVramTemp = gpuVramTemp ?? INVALID_VALUE_FLOAT;
                _snapshotGpuHotSpotTemp = gpuHotSpot ?? INVALID_VALUE_FLOAT;
                _snapshotGpuUsage = gpuUsage ?? iGpuUsage ?? INVALID_VALUE_FLOAT;
                _snapshotGpuTemp = gpuTemp ?? iGpuTemp ?? INVALID_VALUE_FLOAT;
                _snapshotGpuClock = gpuClock ?? iGpuClock ?? INVALID_VALUE_FLOAT;
                _snapshotGpuMemoryClock = gpuMemoryClock ?? iGpuMemoryClock ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPcieRxThroughput = gpuPcieRx ?? iGpuPcieRx ?? INVALID_VALUE_FLOAT;
                _snapshotGpuPcieTxThroughput = gpuPcieTx ?? iGpuPcieTx ?? INVALID_VALUE_FLOAT;
            }

            // Memory
            _snapshotMemUsage = memLoad ?? INVALID_VALUE_FLOAT;

            float memoryUsed = memUsed ?? INVALID_VALUE_FLOAT;
            float memoryAvailable = memAvailable ?? INVALID_VALUE_FLOAT;

            if (memoryUsed >= 0 && memoryAvailable >= 0)
            {
                _snapshotMemUsed = memoryUsed;
                _snapshotMemTotal = memoryUsed + memoryAvailable;

                if (_snapshotMemUsage < 0 && _snapshotMemTotal > 0)
                {
                    _snapshotMemUsage = (_snapshotMemUsed / _snapshotMemTotal) * 100f;
                }
            }
            else if (cachedMemoryTotal > 0 && _snapshotMemUsage >= 0)
            {
                _snapshotMemTotal = cachedMemoryTotal;
                _snapshotMemUsed = (_snapshotMemUsage / 100f) * _snapshotMemTotal;
            }
            else
            {
                _snapshotMemUsed = INVALID_VALUE_FLOAT;
                _snapshotMemTotal = INVALID_VALUE_FLOAT;
            }

            // Temperatures
            _snapshotMemMaxTemp = memoryTempSensors.Count > 0
                ? (double)(memoryTempSensors.Max(s => s.Value) ?? 0)
                : INVALID_VALUE_DOUBLE;
            _snapshotMotherboardMaxTemp = motherboardTempSensors.Count > 0
                ? (double)(motherboardTempSensors.Max(s => s.Value) ?? 0)
                : INVALID_VALUE_DOUBLE;

            float t1 = storageTempSensors.Count > 0
                ? storageTempSensors[0].Value ?? INVALID_VALUE_FLOAT
                : INVALID_VALUE_FLOAT;
            float t2 = storageTempSensors.Count > 1
                ? storageTempSensors[1].Value ?? INVALID_VALUE_FLOAT
                : INVALID_VALUE_FLOAT;
            _snapshotSsdTemps = (t1, t2);

            return false;
        }
    }

    #endregion

    #region Static Helpers

    internal static (float max, float avg) ComputeMaxAndAverage(List<ISensor> sensors)
    {
        if (sensors.Count == 0) return (0, 0);
        float max = float.MinValue, sum = 0;
        foreach (var s in sensors)
        {
            var val = s.Value ?? INVALID_VALUE_FLOAT;
            if (val > max) max = val;
            sum += val;
        }
        return (max, sum / sensors.Count);
    }

    internal static float ResolveCpuPower(float packagePower, IEnumerable<float> componentPowers)
    {
        if (packagePower > MIN_VALID_POWER_READING)
            return packagePower;

        var validComponentPowers = componentPowers
            .Where(value => value > MIN_VALID_POWER_READING)
            .ToArray();

        if (validComponentPowers.Length == 0)
            return INVALID_VALUE_FLOAT;

        var total = validComponentPowers.Sum();
        return total > MIN_VALID_POWER_READING ? total : INVALID_VALUE_FLOAT;
    }

    internal static (float cores, float memory, float platform) ResolveCpuComponentPowers(
        IEnumerable<(string name, float value)> components)
    {
        float cores = INVALID_VALUE_FLOAT;
        float memory = INVALID_VALUE_FLOAT;
        float platform = INVALID_VALUE_FLOAT;

        foreach (var (name, value) in components)
        {
            if (value <= MIN_VALID_POWER_READING)
                continue;

            if (SensorSelector.IsLikelyCpuCorePowerSensorName(name))
                cores = AddComponentPower(cores, value);
            else if (SensorSelector.IsLikelyCpuMemoryPowerSensorName(name))
                memory = AddComponentPower(memory, value);
            else if (SensorSelector.IsLikelyCpuPlatformPowerSensorName(name))
                platform = AddComponentPower(platform, value);
        }

        return (cores, memory, platform);
    }

    internal static float ResolveGpuPower(float currentPower, float previousPower)
    {
        if (currentPower > MIN_VALID_POWER_READING)
            return currentPower;

        return previousPower > MIN_VALID_POWER_READING
            ? previousPower
            : INVALID_VALUE_FLOAT;
    }

    internal static (float used, float total, float utilization) ResolveGpuVramMetrics(
        float used, float total, float free)
    {
        if (total <= 0 && used >= 0 && free >= 0)
            total = used + free;

        if (used < 0 && total > 0 && free >= 0)
            used = Math.Max(0, total - free);

        var utilization = used >= 0 && total > 0
            ? (used / total) * 100f
            : INVALID_VALUE_FLOAT;

        return (used, total, utilization);
    }

    private static float AddComponentPower(float current, float value) =>
        current <= MIN_VALID_POWER_READING ? value : current + value;

    #endregion
}
