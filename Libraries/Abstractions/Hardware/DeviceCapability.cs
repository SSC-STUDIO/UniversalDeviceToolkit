namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Describes one capability exposed by a machine adapter.
/// </summary>
public sealed record DeviceCapability(
    string Id,
    bool IsAvailable,
    bool CanWrite,
    string Source,
    string Reason)
{
    public bool IsReadOnly => IsAvailable && !CanWrite;

    public static DeviceCapability Unavailable(string id, string reason, string source = "probe") =>
        new(id, false, false, source, reason);
}
