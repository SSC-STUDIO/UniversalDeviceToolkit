using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Platform.Windows;

/// <summary>
/// Conservative capability projection for generic Windows machines.
/// Vendor-specific writes must be exposed by a verified backend, not by OS detection alone.
/// </summary>
public sealed class WindowsPlatformServices : IPlatformServices
{
    public string PlatformName => "windows";
    public bool SupportsGpuManagement => false;
    public bool SupportsFanControl => false;
    public bool SupportsKeyboardBacklight => false;
    public bool SupportsBatteryManagement => false;
    public bool SupportsDisplayControl => false;
    public bool SupportsPowerProfile => false;
    public bool SupportsSystemTelemetry => true;
}
