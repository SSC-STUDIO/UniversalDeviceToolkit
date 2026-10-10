using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Platform.Linux;

/// <summary>
/// Linux implementation of <see cref="IPlatformServices"/>.
/// Detects feature availability by probing well-known sysfs / procfs paths and CLI tools.
/// </summary>
public sealed class LinuxPlatformServices : IPlatformServices
{
    private readonly IPlatformProbe _probe;

    public LinuxPlatformServices(IPlatformProbe? probe = null)
    {
        _probe = probe ?? new PhysicalPlatformProbe();
    }

    /// <inheritdoc />
    public string PlatformName => "linux";

    /// <inheritdoc />
    public bool SupportsGpuManagement =>
        _probe.FileExists("/usr/bin/nvidia-smi") ||
        _probe.FileExists("/usr/bin/rocm-smi") ||
        HasDrmGpu();

    /// <inheritdoc />
    public bool SupportsFanControl =>
        _probe.DirectoryExists("/sys/class/hwmon") &&
        _probe.EnumerateFiles("/sys/class/hwmon", "pwm*", recursive: true).Count > 0;

    /// <inheritdoc />
    public bool SupportsKeyboardBacklight =>
        _probe.DirectoryExists("/sys/class/leds") &&
        _probe.EnumerateDirectories("/sys/class/leds").Any(d =>
            Path.GetFileName(d).Contains("kbd_backlight", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public bool SupportsBatteryManagement =>
        _probe.DirectoryExists("/sys/class/power_supply") &&
        _probe.EnumerateDirectories("/sys/class/power_supply").Any(d =>
            Path.GetFileName(d).StartsWith("BAT", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public bool SupportsDisplayControl =>
        _probe.DirectoryExists("/sys/class/backlight") ||
        _probe.FileExists("/usr/bin/xrandr");

    /// <inheritdoc />
    public bool SupportsPowerProfile =>
        _probe.FileExists("/usr/bin/powerprofilesctl") || _probe.FileExists("/usr/sbin/tuned-adm");

    /// <inheritdoc />
    public bool SupportsSystemTelemetry => true;

    private bool HasDrmGpu()
    {
        foreach (var cardDir in _probe.EnumerateDirectories("/sys/class/drm"))
        {
            var name = Path.GetFileName(cardDir.TrimEnd('/'));
            if (name is null ||
                !name.StartsWith("card", StringComparison.Ordinal) ||
                name.Contains('-', StringComparison.Ordinal))
            {
                continue;
            }

            if (_probe.FileExists($"{cardDir.TrimEnd('/')}/device/vendor"))
                return true;
        }

        return false;
    }
}
