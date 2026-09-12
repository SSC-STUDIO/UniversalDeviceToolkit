using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Platform.Linux;
using UniversalDeviceToolkit.Platform.Linux.IO;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class LinuxDeviceAdapterTests
{
    [Fact]
    public async Task ReadSnapshot_ShouldProjectDetectedCapabilitiesAsReadOnly()
    {
        var snapshot = await new LinuxDeviceAdapter(new FakePlatformServices()).ReadSnapshotAsync();

        Assert.Equal("linux", snapshot.Identity.Platform);
        Assert.Equal(RuntimeInformation.OSArchitecture.ToString(), snapshot.Identity.Architecture);
        Assert.Equal(DeviceSupportMatcher.GenericBasicPackId, snapshot.Support.DevicePackId);
        Assert.All(snapshot.Capabilities, capability => Assert.False(capability.CanWrite));
        Assert.Contains(snapshot.Capabilities, capability => capability.Id == "gpu-management" && capability.IsAvailable);
        Assert.Contains(snapshot.Capabilities, capability => capability.Id == "fan-control" && capability.IsAvailable);
        Assert.Contains(snapshot.Capabilities, capability => capability.Id == "read-only-telemetry" && capability.IsAvailable);
        Assert.Empty(snapshot.SensorReadings);
    }

    [Fact]
    public async Task ReadSnapshot_ShouldReadDmiIdentityAndPowerSupplyFromSysfs()
    {
        var fs = new MemoryLinuxFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/dmi/id/sys_vendor"] = "Dell Inc.\n",
            ["/sys/class/dmi/id/product_name"] = "XPS 15 9520\n",
            ["/sys/class/dmi/id/bios_version"] = "1.2.3\n",
            ["/sys/class/power_supply/AC/type"] = "Mains\n",
            ["/sys/class/power_supply/AC/online"] = "1\n",
        });

        var snapshot = await new LinuxDeviceAdapter(new FakePlatformServices(), fileSystem: fs).ReadSnapshotAsync();

        Assert.Equal("linux", snapshot.Identity.Platform);
        Assert.Equal("Dell Inc.", snapshot.Identity.Vendor);
        Assert.Equal("XPS 15 9520", snapshot.Identity.ProductName);
        Assert.Equal("1.2.3", snapshot.Identity.BiosVersion);
        Assert.Equal("Connected", snapshot.PowerStatus);
        Assert.Equal("linux-dmi-sysfs", snapshot.Source);
    }

    private sealed class FakePlatformServices : IPlatformServices
    {
        public string PlatformName => "linux";
        public bool SupportsGpuManagement => true;
        public bool SupportsFanControl => true;
        public bool SupportsKeyboardBacklight => true;
        public bool SupportsBatteryManagement => true;
        public bool SupportsDisplayControl => true;
        public bool SupportsPowerProfile => true;
        public bool SupportsSystemTelemetry => true;
    }
}
