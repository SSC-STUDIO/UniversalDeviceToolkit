using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Platform.Linux;
using UniversalDeviceToolkit.Platform.MacOS;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class PlatformCapabilityTests
{
    [Fact]
    public void LinuxCapabilities_ShouldUseInjectedProbeInsteadOfWindowsHostFilesystem()
    {
        var probe = new FakePlatformProbe(
            files:
            new HashSet<string>
            {
                "/usr/bin/nvidia-smi",
                "/usr/bin/powerprofilesctl",
                "/usr/bin/xrandr",
                "/sys/class/hwmon/hwmon0/pwm1"
            },
            directories:
            new HashSet<string>
            {
                "/sys/class/hwmon",
                "/sys/class/leds",
                "/sys/class/leds/thinkpad::kbd_backlight",
                "/sys/class/power_supply",
                "/sys/class/power_supply/BAT0"
            });

        var services = new LinuxPlatformServices(probe);

        Assert.True(services.SupportsGpuManagement);
        Assert.True(services.SupportsFanControl);
        Assert.True(services.SupportsKeyboardBacklight);
        Assert.True(services.SupportsBatteryManagement);
        Assert.True(services.SupportsDisplayControl);
        Assert.True(services.SupportsPowerProfile);
        Assert.True(services.SupportsSystemTelemetry);
    }

    [Fact]
    public void LinuxCapabilities_ShouldDetectSysfsDrmGpuWithoutVendorCli()
    {
        var probe = new FakePlatformProbe(
            files: new HashSet<string> { "/sys/class/drm/card0/device/vendor" },
            directories: new HashSet<string>
            {
                "/sys/class/drm",
                "/sys/class/drm/card0"
            });

        Assert.True(new LinuxPlatformServices(probe).SupportsGpuManagement);
    }

    [Fact]
    public void LinuxCapabilities_ShouldReturnUnavailableWhenProbeHasNoRequiredEntries()
    {
        var services = new LinuxPlatformServices(new FakePlatformProbe(
            new HashSet<string>(),
            new HashSet<string>()));

        Assert.False(services.SupportsGpuManagement);
        Assert.False(services.SupportsFanControl);
        Assert.False(services.SupportsKeyboardBacklight);
        Assert.False(services.SupportsBatteryManagement);
        Assert.False(services.SupportsDisplayControl);
        Assert.False(services.SupportsPowerProfile);
        Assert.True(services.SupportsSystemTelemetry);
    }

    [Fact]
    public void MacOsCapabilities_ShouldUseInjectedCommandAvailability()
    {
        var probe = new FakePlatformProbe(
            files: new HashSet<string> { "/usr/bin/pmset", "/usr/bin/osascript", "/usr/sbin/sysctl" },
            directories: new HashSet<string>());

        var services = new MacOSPlatformServices(probe);

        Assert.True(services.SupportsBatteryManagement);
        Assert.True(services.SupportsDisplayControl);
        Assert.True(services.SupportsPowerProfile);
        Assert.True(services.SupportsSystemTelemetry);
        Assert.False(services.SupportsGpuManagement);
        Assert.False(services.SupportsFanControl);
        Assert.False(services.SupportsKeyboardBacklight);
    }

    [Fact]
    public void MacOsCapabilities_ShouldReturnUnavailableWhenToolsAreMissing()
    {
        var services = new MacOSPlatformServices(new FakePlatformProbe(
            new HashSet<string>(),
            new HashSet<string>()));

        Assert.False(services.SupportsBatteryManagement);
        Assert.False(services.SupportsDisplayControl);
        Assert.False(services.SupportsPowerProfile);
        Assert.False(services.SupportsSystemTelemetry);
    }

    private sealed class FakePlatformProbe(
        IReadOnlySet<string> files,
        IReadOnlySet<string> directories) : IPlatformProbe
    {
        public bool FileExists(string path) => files.Contains(path);

        public bool DirectoryExists(string path) => directories.Contains(path) ||
            directories.Any(candidate => candidate.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal));

        public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern, bool recursive = false) =>
            files.Where(file => file.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal))
                .Where(file => recursive || file[(path.TrimEnd('/').Length + 1)..].IndexOf('/') < 0)
                .Where(file => MatchesPattern(Path.GetFileName(file), searchPattern))
                .ToArray();

        public IReadOnlyList<string> EnumerateDirectories(string path) =>
            directories.Where(directory =>
                    directory.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal) &&
                    directory[(path.TrimEnd('/').Length + 1)..].IndexOf('/') < 0)
                .ToArray();

        private static bool MatchesPattern(string value, string pattern) =>
            pattern.EndsWith('*')
                ? value.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
                : value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
