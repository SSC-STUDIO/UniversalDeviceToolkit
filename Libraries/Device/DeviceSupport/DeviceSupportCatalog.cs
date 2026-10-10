using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.DeviceSupport;

public sealed record DeviceSupportCatalog
{
    public int SchemaVersion { get; init; } = 1;
    public string AppVersion { get; init; } = "0.0.0";
    public IReadOnlyCollection<DevicePack> DevicePacks { get; init; } = [];
}
