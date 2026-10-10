using Xunit;
using HostLinuxPowerProfileProvider = UniversalDeviceToolkit.Platform.Linux.Hardware.LinuxPowerProfileProvider;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class LinuxPowerProfilesListTests
{
    [Fact]
    public void ParseListedProfiles_ShouldIgnoreDriverRows()
    {
        var profiles = HostLinuxPowerProfileProvider.ParseListedProfiles("""
              power-saver:
                Driver:     amd_pstate

            * balanced:
                Driver:     amd_pstate
                CpuDriver:  amd_pstate

              performance:
                Driver:     amd_pstate
                Degraded:   no
            """);

        Assert.Equal(new[] { "power-saver", "balanced", "performance" }, profiles);
    }

    [Fact]
    public void ParseListedProfiles_WhenEmpty_ShouldReturnNoProfiles()
    {
        Assert.Empty(HostLinuxPowerProfileProvider.ParseListedProfiles(null));
        Assert.Empty(HostLinuxPowerProfileProvider.ParseListedProfiles("   "));
    }
}
