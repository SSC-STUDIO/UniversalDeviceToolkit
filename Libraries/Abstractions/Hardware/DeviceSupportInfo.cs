namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Data-only device-pack result shared by desktop and cross-platform clients.
/// </summary>
public sealed record DeviceSupportInfo(
    string SupportLevel,
    string DevicePackId,
    string DisplayName,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyList<string> HiddenFeatures,
    string Reason)
{
    public bool IsHardwareControlAvailable =>
        EnabledFeatures.Contains("lenovo-hardware-controls", StringComparer.OrdinalIgnoreCase) &&
        !HiddenFeatures.Contains("lenovo-hardware-controls", StringComparer.OrdinalIgnoreCase);
}
