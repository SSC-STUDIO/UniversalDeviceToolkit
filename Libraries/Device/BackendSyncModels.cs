using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Devices.Lights;

namespace UniversalDeviceToolkit.Lib;

public readonly struct AmdWmiCommand
{
    public string Name { get; init; }
    public uint Id { get; init; }
    public bool IsSet { get; init; }

    public override string ToString() => $"{Name} (0x{Id:X8})";
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct CIntelligentCooling
{
    private long _value;
}

public readonly struct FanSpeedTable(int cpuFanSpeed, int gpuFanSpeed, int pchFanSpeed)
{
    public int CpuFanSpeed { get; } = cpuFanSpeed;
    public int GpuFanSpeed { get; } = gpuFanSpeed;
    public int PchFanSpeed { get; } = pchFanSpeed;
}

public readonly struct HidDeviceConfig(
    ushort vendorId,
    ushort productId,
    ushort usagePage,
    ushort usage,
    string displayName)
{
    public ushort VendorId { get; } = vendorId;
    public ushort ProductId { get; } = productId;
    public ushort UsagePage { get; } = usagePage;
    public ushort Usage { get; } = usage;
    public string DisplayName { get; } = displayName;
}

public readonly struct KeyMap(int width, int height, ushort[,] keyCodes, ushort[] additionalKeyCodes)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public ushort[,] KeyCodes { get; } = keyCodes;
    public ushort[] AdditionalKeyCodes { get; } = additionalKeyCodes;
}

public readonly struct LampArrayInfo(string id, string displayName, LampArray lampArray)
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
    public LampArray LampArray { get; } = lampArray;
}

public readonly struct OverclockingProfile
{
    public uint? FMax { get; init; }
    public List<double?> CoreValues { get; init; }
}

public readonly struct ShutdownInfo
{
    public string Status { get; init; }
    public int AbnormalCount { get; init; }
}
