using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalDeviceToolkit.Lib;

public sealed record HardwareInventory
{
    public static readonly HardwareInventory Empty = new();

    public ComputerSystemHardware ComputerSystem { get; init; } = ComputerSystemHardware.Empty;
    public BaseBoardHardware BaseBoard { get; init; } = BaseBoardHardware.Empty;
    public ChassisHardware Chassis { get; init; } = ChassisHardware.Empty;
    public IReadOnlyCollection<ProcessorHardware> Processors { get; init; } = [];
    public IReadOnlyCollection<VideoControllerHardware> VideoControllers { get; init; } = [];
    public MemoryHardware Memory { get; init; } = MemoryHardware.Empty;
    public IReadOnlyCollection<BatteryHardware> Batteries { get; init; } = [];

    public bool HasAnySignal =>
        ComputerSystem.HasAnySignal ||
        BaseBoard.HasAnySignal ||
        Chassis.HasAnySignal ||
        Processors.Count > 0 ||
        VideoControllers.Count > 0 ||
        Memory.HasAnySignal ||
        Batteries.Count > 0;

    public string PrimaryProcessorName => Processors
        .Select(processor => processor.Name)
        .FirstOrDefault(IsPresent) ?? string.Empty;

    public string PrimaryVideoControllerName => VideoControllers
        .Select(videoController => videoController.Name)
        .FirstOrDefault(IsPresent) ?? string.Empty;

    public IEnumerable<string> MatchSignals
    {
        get
        {
            yield return ComputerSystem.Manufacturer;
            yield return ComputerSystem.Model;
            yield return ComputerSystem.SystemFamily;
            yield return ComputerSystem.SystemType;
            yield return ComputerSystem.ChassisSkuNumber;
            yield return BaseBoard.Manufacturer;
            yield return BaseBoard.Product;
            yield return BaseBoard.Version;
            yield return Chassis.Manufacturer;

            foreach (var chassisTypeName in Chassis.ChassisTypeNames)
                yield return chassisTypeName;
        }
    }

    private static bool IsPresent(string value) => !string.IsNullOrWhiteSpace(value);
}

public sealed record ComputerSystemHardware
{
    public static readonly ComputerSystemHardware Empty = new();

    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string SystemFamily { get; init; } = string.Empty;
    public string SystemType { get; init; } = string.Empty;
    public string ChassisSkuNumber { get; init; } = string.Empty;
    public int? PcSystemType { get; init; }
    public int? PcSystemTypeEx { get; init; }

    public bool HasAnySignal =>
        !string.IsNullOrWhiteSpace(Manufacturer) ||
        !string.IsNullOrWhiteSpace(Model) ||
        !string.IsNullOrWhiteSpace(SystemFamily) ||
        !string.IsNullOrWhiteSpace(SystemType) ||
        !string.IsNullOrWhiteSpace(ChassisSkuNumber) ||
        PcSystemType.HasValue ||
        PcSystemTypeEx.HasValue;
}

public sealed record BaseBoardHardware
{
    public static readonly BaseBoardHardware Empty = new();

    public string Manufacturer { get; init; } = string.Empty;
    public string Product { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;

    public bool HasAnySignal =>
        !string.IsNullOrWhiteSpace(Manufacturer) ||
        !string.IsNullOrWhiteSpace(Product) ||
        !string.IsNullOrWhiteSpace(Version);
}

public sealed record ChassisHardware
{
    public static readonly ChassisHardware Empty = new();

    public string Manufacturer { get; init; } = string.Empty;
    public IReadOnlyCollection<ushort> ChassisTypes { get; init; } = [];

    public bool HasAnySignal =>
        !string.IsNullOrWhiteSpace(Manufacturer) ||
        ChassisTypes.Count > 0;

    public IEnumerable<string> ChassisTypeNames =>
        ChassisTypes
            .Select(GetChassisTypeName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static string GetChassisTypeName(ushort chassisType) => chassisType switch
    {
        3 => "Desktop",
        4 => "Low Profile Desktop",
        5 => "Pizza Box",
        6 => "Mini Tower",
        7 => "Tower",
        8 => "Portable",
        9 => "Laptop",
        10 => "Notebook",
        13 => "All-in-One",
        14 => "Sub Notebook",
        17 => "Main System Chassis",
        23 => "Rack Mount",
        30 => "Tablet",
        31 => "Convertible",
        32 => "Detachable",
        _ => string.Empty
    };
}

public sealed record ProcessorHardware
{
    public string Name { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public int? NumberOfCores { get; init; }
    public int? NumberOfLogicalProcessors { get; init; }
    public int? MaxClockSpeedMHz { get; init; }
    public int? AddressWidth { get; init; }
    public int? Architecture { get; init; }
}

public sealed record VideoControllerHardware
{
    public string Name { get; init; } = string.Empty;
    public string AdapterCompatibility { get; init; } = string.Empty;
    public string VideoProcessor { get; init; } = string.Empty;
    public ulong? AdapterRamBytes { get; init; }
}

public sealed record MemoryHardware
{
    public static readonly MemoryHardware Empty = new();

    public ulong TotalCapacityBytes { get; init; }
    public int ModuleCount { get; init; }
    public int? SpeedMHz { get; init; }
    public int? ConfiguredClockSpeedMHz { get; init; }

    public bool HasAnySignal =>
        TotalCapacityBytes > 0 ||
        ModuleCount > 0 ||
        SpeedMHz.HasValue ||
        ConfiguredClockSpeedMHz.HasValue;
}

public sealed record MemoryModuleHardware
{
    public ulong CapacityBytes { get; init; }
    public string Manufacturer { get; init; } = string.Empty;
    public int? SpeedMHz { get; init; }
    public int? ConfiguredClockSpeedMHz { get; init; }
    public string PartNumber { get; init; } = string.Empty;
}

public sealed record BatteryHardware
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? Chemistry { get; init; }
    public int? DesignCapacity { get; init; }
    public int? FullChargeCapacity { get; init; }
}
