namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Coherent read-only snapshot returned by a platform machine adapter.
/// </summary>
public sealed record DeviceSnapshot(
    DeviceIdentity Identity,
    DeviceSupportInfo Support,
    IReadOnlyList<DeviceCapability> Capabilities,
    IReadOnlyList<SensorReading> SensorReadings,
    string? PowerStatus,
    string Source);
