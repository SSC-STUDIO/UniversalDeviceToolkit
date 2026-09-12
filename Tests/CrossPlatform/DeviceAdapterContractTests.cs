using UniversalDeviceToolkit.Abstractions.Hardware;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class DeviceAdapterContractTests
{
    [Fact]
    public void UnknownIdentity_ShouldPreservePlatformAndSource()
    {
        var identity = DeviceIdentity.Unknown("macos", "test-probe");

        Assert.Equal("macos", identity.Platform);
        Assert.Equal("test-probe", identity.Source);
        Assert.Empty(identity.Vendor);
        Assert.Empty(identity.Model);
    }

    [Fact]
    public void UnavailableCapability_ShouldNeverBeWritable()
    {
        var capability = DeviceCapability.Unavailable("fan-control", "No safe backend was detected.");

        Assert.False(capability.IsAvailable);
        Assert.False(capability.CanWrite);
        Assert.False(capability.IsReadOnly);
        Assert.Equal("No safe backend was detected.", capability.Reason);
    }

    [Fact]
    public void SupportInfo_ShouldOnlyReportHardwareControlWhenFeatureIsVisible()
    {
        var enabled = new DeviceSupportInfo(
            "Safe basic mode",
            "generic-pc-basic",
            "Generic PC Basic",
            ["diagnostics", "lenovo-hardware-controls"],
            [],
            "test");
        var hidden = enabled with { HiddenFeatures = ["lenovo-hardware-controls"] };

        Assert.True(enabled.IsHardwareControlAvailable);
        Assert.False(hidden.IsHardwareControlAvailable);
    }
}
