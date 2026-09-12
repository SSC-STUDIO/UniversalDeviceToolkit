using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Platform.MacOS;

/// <summary>
/// macOS implementation of <see cref="IPlatformServices"/>.
/// Detects feature availability using macOS-specific tools and APIs.
/// </summary>
public sealed class MacOSPlatformServices : IPlatformServices
{
    private readonly IPlatformProbe _probe;

    public MacOSPlatformServices(IPlatformProbe? probe = null)
    {
        _probe = probe ?? new PhysicalPlatformProbe();
    }

    /// <inheritdoc />
    public string PlatformName => "macos";

    /// <inheritdoc />
    public bool SupportsGpuManagement => false; // Limited NVIDIA support on macOS

    /// <inheritdoc />
    public bool SupportsFanControl => false; // Requires root access via SMC

    /// <inheritdoc />
    public bool SupportsKeyboardBacklight => false;

    /// <inheritdoc />
    public bool SupportsBatteryManagement => _probe.FileExists("/usr/bin/pmset");

    /// <inheritdoc />
    public bool SupportsDisplayControl => _probe.FileExists("/usr/bin/osascript");

    /// <inheritdoc />
    public bool SupportsPowerProfile => _probe.FileExists("/usr/bin/pmset");

    /// <inheritdoc />
    public bool SupportsSystemTelemetry => _probe.FileExists("/usr/sbin/sysctl");
}
