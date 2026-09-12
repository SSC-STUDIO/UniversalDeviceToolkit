namespace UniversalDeviceToolkit.Tests.Infrastructure;

public enum KeyboardBacklightType
{
    None,
    White,
    RGB,
    Spectrum,
    Zone
}

public record DeviceProfile(
    string Name,
    string DeviceFamily,
    bool HasDgpu,
    string? GpuModel,
    int FanCount,
    int SensorCount,
    KeyboardBacklightType BacklightType,
    int[] DisplayRefreshRates,
    bool HasOverclockSupport,
    int BatteryCapacityWh);
