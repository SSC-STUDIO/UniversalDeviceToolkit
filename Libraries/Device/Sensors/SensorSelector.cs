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
using UniversalDeviceToolkit.Lib.Settings;
using LibreHardwareMonitor.Hardware;
using static UniversalDeviceToolkit.Lib.Controllers.Sensors.SensorPreferenceCatalog;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensor selection logic for identifying the correct sensor from hardware enumeration.
/// Extracted from SensorsGroupController to improve maintainability.
/// </summary>
internal static class SensorSelector
{
    private const string SENSOR_NAME_PACKAGE = "Package";
    private const string SENSOR_NAME_TOTAL_MEMORY = "Total Memory";
    private const string SENSOR_NAME_MEMORY_USED = "Memory Used";
    private const string SENSOR_NAME_MEMORY_AVAILABLE = "Memory Available";

    internal static ISensor? SelectCpuTemperatureSensor(IEnumerable<ISensor> sensors)
    {
        var temperatureSensors = sensors
            .Where(s => s.SensorType == SensorType.Temperature)
            .ToArray();

        if (temperatureSensors.Length == 0)
            return null;

        var preferredName = SelectCpuTemperatureSensorName(temperatureSensors.Select(sensor => sensor.Name));
        if (preferredName is not null)
            return temperatureSensors.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));

        return temperatureSensors
            .OrderByDescending(sensor => sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(sensor => sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .ThenBy(sensor => sensor.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    internal static IHardware? SelectMemoryHardware(IEnumerable<IHardware> hardware)
    {
        var memoryHardware = hardware
            .Where(h => h.HardwareType == HardwareType.Memory)
            .ToArray();

        if (memoryHardware.Length == 0)
            return null;

        var preferredName = SelectMemoryHardwareName(memoryHardware.Select(h => h.Name));
        if (preferredName is not null)
            return memoryHardware.FirstOrDefault(h => string.Equals(h.Name, preferredName, StringComparison.OrdinalIgnoreCase));

        return memoryHardware
            .FirstOrDefault();
    }

    internal static string? SelectMemoryHardwareName(IEnumerable<string> hardwareNames)
    {
        var names = hardwareNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        return names
            .OrderByDescending(name => string.Equals(name, SENSOR_NAME_TOTAL_MEMORY, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(name => name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    internal static string? SelectCpuTemperatureSensorName(IEnumerable<string> sensorNames)
    {
        var names = sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        foreach (var preferredName in SensorPreferenceCatalog.CPU_TEMPERATURE_SENSOR_PREFERENCES)
        {
            var preferred = names.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return preferred;
        }

        return names
            .OrderByDescending(name => name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(name => name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    internal static ISensor? SelectCpuUsageSensor(IEnumerable<ISensor> sensors)
    {
        var loadSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Load)
            .ToArray();

        if (loadSensors.Length == 0)
            return null;

        var preferredName = SelectCpuUsageSensorName(loadSensors.Select(sensor => sensor.Name));
        if (preferredName is not null)
            return loadSensors.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));

        return loadSensors.FirstOrDefault();
    }

    internal static string? SelectCpuUsageSensorName(IEnumerable<string> sensorNames)
    {
        var names = sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        foreach (var preferredName in SensorPreferenceCatalog.CPU_USAGE_SENSOR_PREFERENCES)
        {
            var preferred = names.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Core Max", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Thread", StringComparison.OrdinalIgnoreCase)
                && !name.Contains('#'));
            if (preferred is not null)
                return preferred;
        }

        return names
            .Where(name =>
                !name.Contains("Core Max", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Thread", StringComparison.OrdinalIgnoreCase)
                && !name.Contains('#'))
            .OrderByDescending(name => name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(name => name.Contains("Total", StringComparison.OrdinalIgnoreCase))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    internal static ISensor? SelectCpuVoltageSensor(IEnumerable<ISensor> sensors)
    {
        var voltageSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Voltage)
            .ToArray();

        if (voltageSensors.Length == 0)
            return null;

        var preferredName = SelectCpuVoltageSensorName(voltageSensors.Select(sensor => sensor.Name));
        if (preferredName is not null)
            return voltageSensors.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));

        return voltageSensors.FirstOrDefault();
    }

    internal static string? SelectCpuVoltageSensorName(IEnumerable<string> sensorNames)
    {
        var names = sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        foreach (var preferredName in SensorPreferenceCatalog.CPU_VOLTAGE_SENSOR_PREFERENCES)
        {
            var preferred = names.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase)
                && !name.Contains("System Agent", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("SA", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Cache", StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return preferred;
        }

        return names
            .Where(name =>
                !name.Contains("System Agent", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("SA", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Cache", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(name => name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(name => name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    internal static ISensor? SelectCpuPackagePowerSensor(IEnumerable<ISensor> sensors)
    {
        var powerSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Power)
            .ToArray();

        if (powerSensors.Length == 0)
            return null;

        var preferredName = SelectCpuPackagePowerSensorName(powerSensors.Select(sensor => sensor.Name));
        if (preferredName is not null)
            return powerSensors.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));

        return null;
    }

    internal static string? SelectCpuPackagePowerSensorName(IEnumerable<string> sensorNames) =>
        SensorPreferenceCatalog.SelectPreferredSensorName(
            sensorNames.Where(IsLikelyCpuPackagePowerCandidateName),
            SensorPreferenceCatalog.CPU_PACKAGE_POWER_SENSOR_PREFERENCES);

    internal static ISensor? SelectGpuVramTemperatureSensor(IEnumerable<ISensor> sensors)
    {
        var temperatureSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .ToArray();

        if (temperatureSensors.Length == 0)
            return null;

        var preferredName = SelectGpuVramTemperatureSensorName(temperatureSensors.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return temperatureSensors.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? SelectGpuVramTemperatureSensorName(IEnumerable<string> sensorNames)
    {
        var names = sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        foreach (var preferredName in SensorPreferenceCatalog.GPU_VRAM_TEMPERATURE_SENSOR_PREFERENCES)
        {
            var preferred = names.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return preferred;
        }

        return null;
    }

    internal static ISensor? SelectGpuHotSpotTemperatureSensor(IEnumerable<ISensor> sensors)
    {
        var temperatureSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .ToArray();

        if (temperatureSensors.Length == 0)
            return null;

        var preferredName = SelectGpuHotSpotTemperatureSensorName(temperatureSensors.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return temperatureSensors.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? SelectGpuHotSpotTemperatureSensorName(IEnumerable<string> sensorNames)
    {
        var names = sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        foreach (var preferredName in SensorPreferenceCatalog.GPU_HOTSPOT_TEMPERATURE_SENSOR_PREFERENCES)
        {
            var preferred = names.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return preferred;
        }

        return null;
    }

    internal static bool IsLikelyGpuVramTemperatureSensorName(string sensorName) =>
        SelectGpuVramTemperatureSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuHotSpotTemperatureSensorName(string sensorName) =>
        SelectGpuHotSpotTemperatureSensorName([sensorName]) is not null;

    internal static IEnumerable<ISensor> SelectMemoryTemperatureSensors(IEnumerable<ISensor> sensors, bool requireMemoryKeywords)
    {
        var temperatureSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .Where(sensor => !requireMemoryKeywords || IsLikelyMemoryTemperatureSensorName(sensor.Name))
            .OrderByDescending(sensor => SensorPreferenceCatalog.MEMORY_TEMPERATURE_SENSOR_PREFERENCES.Any(keyword =>
                sensor.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ThenByDescending(sensor => sensor.Name.Contains("DIMM", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(sensor => sensor.Name.Contains("DRAM", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(sensor => sensor.Name.Contains("SPD", StringComparison.OrdinalIgnoreCase))
            .ThenBy(sensor => sensor.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return temperatureSensors;
    }

    internal static bool IsLikelyMemoryTemperatureSensorName(string sensorName) =>
        SensorPreferenceCatalog.MEMORY_TEMPERATURE_SENSOR_PREFERENCES.Any(keyword =>
            sensorName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    internal static bool IsBoardTemperatureHardware(IHardware hardware)
    {
        if (hardware.HardwareType == HardwareType.Motherboard)
            return true;

        if (IsDedicatedMetricHardwareType(hardware.HardwareType.ToString()))
            return false;

        if (SensorPreferenceCatalog.BOARD_SENSOR_HARDWARE_NAME_EXCLUSIONS.Any(keyword =>
            hardware.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            return false;

        var sensors = hardware.Sensors;
        return sensors is not null && sensors.Any(sensor =>
            sensor.SensorType == SensorType.Temperature &&
            (IsLikelyMemoryTemperatureSensorName(sensor.Name) ||
             SelectMotherboardTemperatureSensorName([sensor.Name]) is not null));
    }

    private static bool IsDedicatedMetricHardwareType(string hardwareTypeName) =>
        hardwareTypeName.Contains("Cpu", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Gpu", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Network", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Battery", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Psu", StringComparison.OrdinalIgnoreCase) ||
        hardwareTypeName.Contains("Cooler", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<ISensor> SelectMotherboardTemperatureSensors(IEnumerable<ISensor> sensors)
    {
        var temperatureSensors = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .ToArray();

        if (temperatureSensors.Length == 0)
            return [];

        var preferredName = SelectMotherboardTemperatureSensorName(temperatureSensors.Select(sensor => sensor.Name));
        if (preferredName is not null)
            return temperatureSensors
                .Where(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        return temperatureSensors
            .Where(sensor => !IsLikelyMemoryTemperatureSensorName(sensor.Name))
            .Where(sensor => !sensor.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
            .Where(sensor => !sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            .OrderBy(sensor => sensor.Name, StringComparer.OrdinalIgnoreCase)
            .Take(1)
            .ToArray();
    }

    internal static string? SelectMotherboardTemperatureSensorName(IEnumerable<string> sensorNames) =>
        SensorPreferenceCatalog.SelectPreferredSensorName(
            sensorNames.Where(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !IsLikelyMemoryTemperatureSensorName(name) &&
                !name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("GPU", StringComparison.OrdinalIgnoreCase)),
            SensorPreferenceCatalog.MOTHERBOARD_TEMPERATURE_SENSOR_PREFERENCES);

    internal static ISensor? SelectGpuVramUsedSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType is SensorType.SmallData or SensorType.Data)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuVramUsedSensorName(candidates.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuVramTotalSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType is SensorType.SmallData or SensorType.Data)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuVramTotalSensorName(candidates.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuVramFreeSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType is SensorType.SmallData or SensorType.Data)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuVramFreeSensorName(candidates.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? SelectGpuVramUsedSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_VRAM_USED_SENSOR_PREFERENCES);

    internal static string? SelectGpuVramTotalSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_VRAM_TOTAL_SENSOR_PREFERENCES);

    internal static string? SelectGpuVramFreeSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_VRAM_FREE_SENSOR_PREFERENCES);

    internal static bool IsLikelyGpuVramUsedSensorName(string sensorName) =>
        SelectGpuVramUsedSensorName([sensorName]) is not null;

    internal static bool IsLikelyCpuVoltageSensorName(string sensorName) =>
        SelectCpuVoltageSensorName([sensorName]) is not null;

    internal static bool IsLikelyCpuUsageSensorName(string sensorName) =>
        SelectCpuUsageSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuVramTotalSensorName(string sensorName) =>
        SelectGpuVramTotalSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuVramFreeSensorName(string sensorName) =>
        SelectGpuVramFreeSensorName([sensorName]) is not null;

    internal static ISensor? SelectGpuPcieRxThroughputSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Throughput)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuPcieRxThroughputSensorName(candidates.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuPcieTxThroughputSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Throughput)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuPcieTxThroughputSensorName(candidates.Select(sensor => sensor.Name));
        if (preferredName is null)
            return null;

        return candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? SelectGpuPcieRxThroughputSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_PCIE_RX_THROUGHPUT_SENSOR_PREFERENCES);

    internal static string? SelectGpuPcieTxThroughputSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_PCIE_TX_THROUGHPUT_SENSOR_PREFERENCES);

    internal static ISensor? SelectCpuFanSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Fan)
            .ToArray();
        if (candidates.Length == 0)
            return null;

        return candidates
            .OrderByDescending(sensor => ScoreCpuFanName(sensor.Name))
            .ThenByDescending(sensor => sensor.Value ?? 0f)
            .FirstOrDefault(sensor => ScoreCpuFanName(sensor.Name) > 0);
    }

    internal static ISensor? SelectGpuFanSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Fan)
            .ToArray();
        if (candidates.Length == 0)
            return null;

        return candidates
            .OrderByDescending(sensor => ScoreGpuFanName(sensor.Name))
            .ThenByDescending(sensor => sensor.Value ?? 0f)
            .FirstOrDefault(sensor => ScoreGpuFanName(sensor.Name) > 0);
    }

    internal static int ScoreCpuFanName(string name)
    {
        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Processor", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("PCH", StringComparison.OrdinalIgnoreCase))
            return 300;
        if (name.Contains("Fan #1", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fan 1", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fan#1", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("SYS", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("System", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Chassis", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Left", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Right", StringComparison.OrdinalIgnoreCase))
            return 200;
        if (name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Graphics", StringComparison.OrdinalIgnoreCase))
            return 0;
        // Any remaining fan sensor is still a candidate (Legion OEM names vary by generation).
        return 100;
    }

    internal static int ScoreGpuFanName(string name)
    {
        if (name.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Graphics", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("dGPU", StringComparison.OrdinalIgnoreCase))
            return 300;
        if (name.Contains("Fan #2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fan 2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fan#2", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fan2", StringComparison.OrdinalIgnoreCase))
            return 150;
        // Generic "Fan" / chassis names still usable when no dedicated GPU sensor exists.
        if (name.Contains("Fan", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Processor", StringComparison.OrdinalIgnoreCase))
            return 50;
        return 0;
    }

    internal static ISensor? SelectGpuUsageSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Load)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuUsageSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectMemoryUsedSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Data)
            .Where(sensor => IsLikelySystemMemorySensorName(sensor.Name))
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectMemoryUsedSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault(sensor => sensor.Name.Contains(SENSOR_NAME_MEMORY_USED, StringComparison.OrdinalIgnoreCase))
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectMemoryAvailableSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Data)
            .Where(sensor => IsLikelySystemMemorySensorName(sensor.Name))
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectMemoryAvailableSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault(sensor => sensor.Name.Contains(SENSOR_NAME_MEMORY_AVAILABLE, StringComparison.OrdinalIgnoreCase))
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectMemoryLoadSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Load)
            .Where(sensor => IsLikelySystemMemorySensorName(sensor.Name))
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectMemoryLoadSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectStorageTemperatureSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectStorageTemperatureSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuPowerSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Power)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuPowerSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuTemperatureSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuTemperatureSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuCoreClockSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Clock)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuCoreClockSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuMemoryClockSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Clock)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuMemoryClockSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static ISensor? SelectGpuVoltageSensor(IEnumerable<ISensor> sensors)
    {
        var candidates = sensors
            .Where(sensor => sensor.SensorType == SensorType.Voltage)
            .ToArray();

        if (candidates.Length == 0)
            return null;

        var preferredName = SelectGpuVoltageSensorName(candidates.Select(sensor => sensor.Name));
        return preferredName is null
            ? candidates.FirstOrDefault()
            : candidates.FirstOrDefault(sensor => string.Equals(sensor.Name, preferredName, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? SelectGpuPowerSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_POWER_SENSOR_PREFERENCES);

    internal static string? SelectGpuTemperatureSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_TEMPERATURE_SENSOR_PREFERENCES);

    internal static string? SelectGpuCoreClockSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_CORE_CLOCK_SENSOR_PREFERENCES);

    internal static string? SelectGpuMemoryClockSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_MEMORY_CLOCK_SENSOR_PREFERENCES);

    internal static string? SelectGpuVoltageSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_VOLTAGE_SENSOR_PREFERENCES);

    internal static string? SelectMemoryUsedSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames.Where(IsLikelySystemMemorySensorName), SensorPreferenceCatalog.MEMORY_USED_SENSOR_PREFERENCES);

    internal static string? SelectMemoryAvailableSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames.Where(IsLikelySystemMemorySensorName), SensorPreferenceCatalog.MEMORY_AVAILABLE_SENSOR_PREFERENCES);

    internal static string? SelectMemoryLoadSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames.Where(IsLikelySystemMemorySensorName), SensorPreferenceCatalog.MEMORY_LOAD_SENSOR_PREFERENCES);

    internal static string? SelectStorageTemperatureSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.STORAGE_TEMPERATURE_SENSOR_PREFERENCES);

    private static bool IsLikelySystemMemorySensorName(string sensorName) =>
        !sensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase)
        && !sensorName.Contains("VRAM", StringComparison.OrdinalIgnoreCase)
        && !sensorName.Contains("D3D", StringComparison.OrdinalIgnoreCase)
        && !sensorName.Contains("Shared", StringComparison.OrdinalIgnoreCase);

    internal static string? SelectGpuUsageSensorName(IEnumerable<string> sensorNames) =>
        SelectPreferredSensorName(sensorNames, SensorPreferenceCatalog.GPU_USAGE_SENSOR_PREFERENCES)
        ?? sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(name => name.Trim().StartsWith("D3D", StringComparison.OrdinalIgnoreCase));

    internal static bool IsLikelyGpuPcieRxThroughputSensorName(string sensorName) =>
        SelectGpuPcieRxThroughputSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuPcieTxThroughputSensorName(string sensorName) =>
        SelectGpuPcieTxThroughputSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuUsageSensorName(string sensorName) =>
        SelectGpuUsageSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuPowerSensorName(string sensorName) =>
        SelectGpuPowerSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuTemperatureSensorName(string sensorName) =>
        SelectGpuTemperatureSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuCoreClockSensorName(string sensorName) =>
        SelectGpuCoreClockSensorName([sensorName]) is not null;

    internal static bool IsLikelyCpuPCoreClockSensorName(string sensorName) =>
        IsLikelyCpuCoreClockSensorName(sensorName, SensorPreferenceCatalog.CPU_P_CORE_CLOCK_SENSOR_PREFERENCES);

    internal static bool IsLikelyCpuECoreClockSensorName(string sensorName) =>
        IsLikelyCpuCoreClockSensorName(sensorName, SensorPreferenceCatalog.CPU_E_CORE_CLOCK_SENSOR_PREFERENCES);

    private static bool IsLikelyCpuCoreClockSensorName(string? sensorName, IEnumerable<string> preferences)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
            return false;

        if (sensorName.Contains("Average", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Effective", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SelectPreferredSensorName([sensorName], preferences) is not null;
    }

    internal static bool IsLikelyGpuMemoryClockSensorName(string sensorName) =>
        SelectGpuMemoryClockSensorName([sensorName]) is not null;

    internal static bool IsLikelyGpuVoltageSensorName(string sensorName) =>
        SelectGpuVoltageSensorName([sensorName]) is not null;

    internal static bool IsLikelyCpuPackagePowerSensorName(string sensorName) =>
        SelectCpuPackagePowerSensorName([sensorName]) is not null;

    private static bool IsLikelyCpuPackagePowerCandidateName(string? sensorName)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
            return false;

        if (sensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("DRAM", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Platform", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Uncore", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Ring", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasCore = sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                      sensorName.Contains("Cores", StringComparison.OrdinalIgnoreCase);
        var hasSoc = SensorPreferenceCatalog.RegexSocBoundary.IsMatch(sensorName);

        if (hasCore && hasSoc)
            return true;

        if (hasCore)
        {
            return sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                   sensorName.Contains("PPT", StringComparison.OrdinalIgnoreCase) ||
                   sensorName.Contains("STAPM", StringComparison.OrdinalIgnoreCase) ||
                   sensorName.Contains("APU", StringComparison.OrdinalIgnoreCase) ||
                   sensorName.Contains("Socket", StringComparison.OrdinalIgnoreCase);
        }

        return !hasSoc;
    }

    internal static bool IsLikelyCpuComponentPowerSensorName(string sensorName) =>
        IsLikelyCpuCorePowerSensorName(sensorName)
        || IsLikelyCpuMemoryPowerSensorName(sensorName)
        || IsLikelyCpuPlatformPowerSensorName(sensorName);

    internal static bool IsLikelyCpuCorePowerSensorName(string sensorName) =>
        !sensorName.Contains("GT", StringComparison.OrdinalIgnoreCase) &&
        !sensorName.Contains("Graphics", StringComparison.OrdinalIgnoreCase) &&
        !sensorName.Contains("Uncore", StringComparison.OrdinalIgnoreCase) &&
        !sensorName.Contains("Ring", StringComparison.OrdinalIgnoreCase) &&
        SensorPreferenceCatalog.CPU_CORE_POWER_SENSOR_PREFERENCES.Any(keyword =>
            sensorName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    internal static bool IsLikelyCpuMemoryPowerSensorName(string sensorName) =>
        SensorPreferenceCatalog.CPU_MEMORY_POWER_SENSOR_PREFERENCES.Any(keyword =>
            sensorName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    internal static bool IsLikelyCpuPlatformPowerSensorName(string sensorName) =>
        SensorPreferenceCatalog.CPU_PLATFORM_POWER_SENSOR_PREFERENCES.Any(keyword =>
            sensorName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
