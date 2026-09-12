namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Stable, platform-neutral identity for the machine being adapted.
/// </summary>
public sealed record DeviceIdentity(
    string Platform,
    string Architecture,
    string Vendor,
    string Model,
    string ProductName,
    string BiosVersion,
    string SerialNumber,
    string Source)
{
    public string MachineType { get; init; } = string.Empty;

    public static DeviceIdentity Unknown(string platform, string source) =>
        new(platform, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, source);
}
