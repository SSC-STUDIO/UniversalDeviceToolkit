using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Centralized catalog of sensor name preferences and matching patterns.
/// Extracted from SensorsGroupController to improve maintainability.
/// </summary>
internal static class SensorPreferenceCatalog
{
    internal static readonly string[] CPU_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "CPU Package",
        "CPU Package Temperature",
        "Processor Package",
        "Processor Package Temperature",
        "Package",
        "Tctl/Tdie",
        "Tctl",
        "Tdie",
        "CPU Die",
        "CPU CCD",
        "CCD",
        "Tjunction",
        "Core Max",
        "Core Average",
        "Average",
        "CPU",
        "Core",
    ];
    internal static readonly string[] CPU_VOLTAGE_SENSOR_PREFERENCES =
    [
        "CPU Core Voltage",
        "CPU VCore",
        "Core Voltage",
        "Vcore",
        "VCore",
        "Core VID",
        "CPU VID",
        "VDDCR CPU",
        "CPU VDD",
        "VDD CPU",
        "VCC Core",
        "IA Voltage",
        "IA VR Voltage",
        "Core VIDs",
        "CPU Core VID",
        "CPU Input Voltage",
        "VCCIN",
        "VDDCR_VDD",
        "SVI2 TFN CPU",
        "SVI3 TFN CPU",
        "SVI2 TFN",
        "SVI3 TFN",
        "VID",
        "Core",
        "CPU",
        "Voltage",
    ];
    internal static readonly string[] CPU_USAGE_SENSOR_PREFERENCES =
    [
        "CPU Total",
        "Total",
        "CPU Usage",
        "CPU Utilization",
        "CPU Utility",
        "Package",
    ];
    internal static readonly string[] CPU_PACKAGE_POWER_SENSOR_PREFERENCES =
    [
        "Core+SoC Power",
        "Core + SoC Power",
        "Core and SoC Power",
        "CPU Core+SoC",
        "CPU Core + SoC",
        "APU STAPM",
        "STAPM",
        "APU PPT",
        "APU sPPT",
        "APU Package",
        "APU Power",
        "CPU sPPT",
        "CPU Socket Power",
        "Socket Power",
        "CPU Package",
        "CPU Package Power",
        "Package Power",
        "CPU PPT Power",
        "CPU PPT",
        "sPPT",
        "PPT Limit",
        "Processor Package Power",
        "PPT",
        "Processor Power",
        "Processor Power Draw",
        "CPU Total",
        "Total CPU",
        "CPU Power",
        "CPU Power Draw",
        "Package Power Draw",
        "Package",
    ];
    internal static readonly string[] CPU_CORE_POWER_SENSOR_PREFERENCES =
    [
        "IA Cores",
        "IA Power",
        "IA Limit",
        "VDDCR CPU Power",
        "CPU VDD Power",
        "VDD CPU Power",
        "CPU Core Power",
        "Core Power Draw",
        "CPU Cores",
        "CPU Core",
        "Core Power",
        "Cores",
    ];
    internal static readonly string[] CPU_MEMORY_POWER_SENSOR_PREFERENCES =
    [
        "CPU Memory",
        "Memory Controller",
        "DRAM",
        "MCH",
    ];
    internal static readonly string[] CPU_PLATFORM_POWER_SENSOR_PREFERENCES =
    [
        "CPU Platform",
        "CPU Graphics",
        "GT Cores",
        "GT Power",
        "VDDCR SOC Power",
        "VDDCR SoC Power",
        "VDDCR_SOC Power",
        "CPU SoC",
        "SoC",
        "SOC",
        "SoC Power",
        "System Agent",
        "PCH",
        "CPU Uncore",
        "Uncore",
        "Uncore Power",
        "Ring",
        "EDC",
        "TDC",
    ];
    internal static readonly string[] CPU_P_CORE_CLOCK_SENSOR_PREFERENCES =
    [
        "CPU P-Core",
        "P-Core",
        "P Core",
        "Performance Core",
        "Performance-Core",
        "CPU Performance",
    ];
    internal static readonly string[] CPU_E_CORE_CLOCK_SENSOR_PREFERENCES =
    [
        "CPU E-Core",
        "E-Core",
        "E Core",
        "Efficient Core",
        "Efficiency Core",
        "Efficient-Core",
        "CPU Efficient",
        "CPU Efficiency",
    ];
    internal static readonly string[] GPU_VRAM_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "GPU Memory Junction",
        "Memory Junction",
        "VRAM Junction",
        "VRAM Temperature",
        "Memory Temperature",
        "VRAM",
        "Memory",
    ];
    internal static readonly string[] GPU_HOTSPOT_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "GPU Hot Spot",
        "Hot Spot",
        "Hotspot",
    ];
    internal static readonly string[] MEMORY_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "DIMM Temperature",
        "DIMM Thermal Sensor",
        "DIMM Thermal",
        "Memory Temperature",
        "Memory Module Temperature",
        "Module Temperature",
        "RAM Temperature",
        "DRAM Temperature",
        "DIMM Module",
        "DIMM #",
        "Memory Slot",
        "DDR Module",
        "DDR5 SPD Hub",
        "DDR4 TSOD",
        "SPD Hub Temperature",
        "SPD Hub",
        "TSOD Temperature",
        "PMIC Temperature",
        "Thermal Sensor on DIMM",
        "DIMM",
        "DRAM",
        "DDR",
        "SPD",
        "TSOD",
        "PMIC",
        "Memory",
        "RAM",
    ];
    internal static readonly string[] MOTHERBOARD_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "PCH Temperature",
        "PCH",
        "Chipset Temperature",
        "Chipset",
        "Platform Controller Hub Temperature",
        "Platform Controller Hub",
        "Motherboard Temperature",
        "Motherboard",
        "Mainboard Temperature",
        "Mainboard",
        "Board Temperature",
        "Board",
        "VRM MOS Temperature",
        "VRM Temperature",
        "VRM MOS",
        "VRM",
        "MOSFET",
        "MOS Temperature",
        "MOS",
        "Super I/O",
        "Super IO",
        "System Temperature",
        "Sys Temp",
        "System",
        "T_Sensor",
        "TSensor",
        "SYSTIN",
        "AUXTIN",
        "TMPIN",
        "Temp1",
        "Temp 1",
        "Temperature #1",
        "Temp2",
        "Temp 2",
        "Temperature #2",
        "ACPI Thermal Zone",
        "Thermal Zone",
        "TZ00",
        "TZ01",
        "TZS0",
        "TZS1",
        "EC Temp",
        "EC",
        "Embedded Controller",
    ];
    internal static readonly string[] BOARD_SENSOR_HARDWARE_NAME_EXCLUSIONS =
    [
        "Battery",
        "Network",
        "Ethernet",
        "Wi-Fi",
        "WiFi",
        "Wireless",
    ];
    internal static readonly string[] GPU_VRAM_USED_SENSOR_PREFERENCES =
    [
        "GPU Memory Used",
        "GPU Dedicated Memory Used",
        "Dedicated Memory Used",
        "Dedicated Video Memory Used",
        "D3D Dedicated Memory Used",
        "D3D Shared Memory Used",
        "Shared Memory Used",
        "VRAM Used",
        "Memory Used",
    ];
    internal static readonly string[] GPU_VRAM_TOTAL_SENSOR_PREFERENCES =
    [
        "GPU Memory Total",
        "GPU Dedicated Memory Total",
        "Dedicated Memory Total",
        "Dedicated Video Memory Total",
        "VRAM Total",
        "Memory Total",
        "GPU Memory",
        // Shared-memory totals rank last: they describe system RAM loaned to the GPU,
        // not dedicated VRAM, and mismatch the dedicated "used" sensor picked above.
        "D3D Shared Memory Total",
        "Shared Memory Total",
    ];
    internal static readonly string[] GPU_VRAM_FREE_SENSOR_PREFERENCES =
    [
        "GPU Memory Free",
        "GPU Dedicated Memory Free",
        "Dedicated Memory Free",
        "Dedicated Video Memory Free",
        "D3D Dedicated Memory Free",
        "D3D Shared Memory Free",
        "Shared Memory Free",
        "VRAM Free",
        "Memory Free",
    ];
    internal static readonly string[] GPU_PCIE_RX_THROUGHPUT_SENSOR_PREFERENCES =
    [
        "GPU PCIe Rx",
        "GPU PCIe Read",
        "PCIe Read",
        "PCIe Rx",
        "PCIe RX",
        "Bus Read",
        "Bus Rx",
    ];
    internal static readonly string[] GPU_PCIE_TX_THROUGHPUT_SENSOR_PREFERENCES =
    [
        "GPU PCIe Tx",
        "GPU PCIe Write",
        "PCIe Write",
        "PCIe Tx",
        "PCIe TX",
        "Bus Write",
        "Bus Tx",
    ];
    internal static readonly string[] GPU_POWER_SENSOR_PREFERENCES =
    [
        "GPU Package",
        "GPU PPT",
        "GPU Power",
        "GPU Power Draw",
        "GPU Power Consumption",
        "GPU Instantaneous Power",
        "Board Power Draw",
        "Board Power",
        "GPU Board Power",
        "GPU Total Board Power",
        "Total Board Power",
        "GPU Total Power",
        "Total Graphics Power",
        "Average GPU Power",
        "Current GPU Power",
        "Graphics Power",
        "GPU Graphics Power",
        "GPU Core Power",
        "GPU ASIC Power",
        "ASIC Power",
        "GPU Chip Power",
        "Chip Power",
        "Core Power",
        "Power Consumption",
        "Instantaneous Power",
        "TGP",
        "PPT",
        "Power Draw",
        "Package Power",
        "Power",
    ];
    internal static readonly string[] GPU_VOLTAGE_SENSOR_PREFERENCES =
    [
        "GPU Core Voltage",
        "GPU VDDC",
        "GPU VDD",
        "GPU VCore",
        "Core Voltage",
        "VDDC",
        "VDDCI",
        "VDD",
        "MVDD",
        "NVVDD",
        "GPU Core",
        "GPU Voltage",
        "Voltage",
        "Core",
        "GPU",
    ];
    internal static readonly string[] GPU_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "GPU Core",
        "Core Temperature",
        "Core",
        "GPU Temperature",
        "GPU",
        "Temperature",
    ];
    internal static readonly string[] GPU_CORE_CLOCK_SENSOR_PREFERENCES =
    [
        "GPU Core",
        "Core Clock",
        "Graphics Clock",
        "Graphics",
        "SM Clock",
        "Shader Clock",
        "GPU Clock",
        "Core",
        "Clock",
    ];
    internal static readonly string[] GPU_MEMORY_CLOCK_SENSOR_PREFERENCES =
    [
        "GPU Memory",
        "Memory Clock",
        "FB Clock",
        "VRAM Memory Clock",
        "VRAM Clock",
        "VRAM",
        "Memory",
        "Clock",
    ];
    internal static readonly string[] MEMORY_USED_SENSOR_PREFERENCES =
    [
        "Memory Used",
        "Used Memory",
    ];
    internal static readonly string[] MEMORY_AVAILABLE_SENSOR_PREFERENCES =
    [
        "Memory Available",
        "Available Memory",
        "Memory Free",
        "Free Memory",
    ];
    internal static readonly string[] MEMORY_LOAD_SENSOR_PREFERENCES =
    [
        "Memory",
        "Memory Load",
        "System Memory",
    ];
    internal static readonly string[] STORAGE_TEMPERATURE_SENSOR_PREFERENCES =
    [
        "NVMe Composite Temperature",
        "Composite Temperature",
        "Drive Composite Temperature",
        "Composite",
        "NVMe Composite",
        "Drive Temperature 1",
        "Drive Temperature 2",
        "Drive Temperature",
        "SSD Temperature",
        "Disk Temperature",
        "HDD Temperature",
        "Controller Temperature",
        "ASIC Controller Temperature",
        "ASIC Controller",
        "ASIC Temperature",
        "NAND Temperature 1",
        "NAND Temperature 2",
        "NAND Temperature",
        "NAND 1",
        "NAND 2",
        "Temperature 1",
        "Temperature 2",
        "Temperature #1",
        "Temperature #2",
        "Temperature",
    ];
    internal static readonly string[] GPU_USAGE_SENSOR_PREFERENCES =
    [
        "D3D 3D",
        "GPU Core",
        "Core Utilization",
        "GPU Utilization",
        "Utilization",
        "3D",
    ];

    internal static readonly Regex RegexAmdGpuIntegrated = new(@"AMD Radeon\(TM\)\s+\d+M", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex RegexStripAmd = new(@"\s+with\s+Radeon\s+Graphics$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex RegexStripIntel = new(@"\s*\d+(?:th|st|nd|rd)?\s+Gen\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal static readonly Regex RegexStripNvidia = new(@"(?i)\b(?:Nvidia\s+)?(GeForce\s+(?:RTX|GTX)\s+\d{3,4}(?:\s+(Ti|SUPER|Ti\s+SUPER|M))?)\b(?:\s+Laptop\s+GPU)?(?!\S)", RegexOptions.Compiled);
    internal static readonly Regex RegexCleanSpaces = new(@"\s+", RegexOptions.Compiled);
    internal static readonly Regex RegexSocBoundary = new(@"\bSoC\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string? SelectPreferredSensorName(IEnumerable<string> sensorNames, IEnumerable<string> preferredNames)
    {
        var names = sensorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
            return null;

        foreach (var preferredName in preferredNames)
        {
            var preferred = names.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
                return preferred;
        }

        return null;
    }
}
