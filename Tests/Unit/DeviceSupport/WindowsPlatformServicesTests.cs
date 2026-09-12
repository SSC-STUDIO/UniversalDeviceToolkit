using UniversalDeviceToolkit.Platform.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

public sealed class WindowsPlatformServicesTests
{
    [Fact]
    public void GenericWindowsProjection_ShouldOnlyAdvertiseReadOnlyTelemetry()
    {
        var services = new WindowsPlatformServices();

        Assert.Equal("windows", services.PlatformName);
        Assert.False(services.SupportsGpuManagement);
        Assert.False(services.SupportsFanControl);
        Assert.False(services.SupportsKeyboardBacklight);
        Assert.False(services.SupportsBatteryManagement);
        Assert.False(services.SupportsDisplayControl);
        Assert.False(services.SupportsPowerProfile);
        Assert.True(services.SupportsSystemTelemetry);
    }
}
