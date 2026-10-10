using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class DriverInfoTests
{
    [Fact]
    public void Properties_ShouldReturnConstructorValues()
    {
        var date = new DateTime(2025, 3, 15);
        var version = new Version(10, 0, 22631);
        var info = new DriverInfo(
            deviceId: @"PCI\VEN_10DE&DEV_2786",
            hardwareId: @"PCI\VEN_10DE&DEV_2786&SUBSYS_00000000",
            version: version,
            date: date);

        info.DeviceId.Should().Be(@"PCI\VEN_10DE&DEV_2786");
        info.HardwareId.Should().Be(@"PCI\VEN_10DE&DEV_2786&SUBSYS_00000000");
        info.Version.Should().Be(version);
        info.Date.Should().Be(date);
    }

    [Fact]
    public void Constructor_NullVersionAndDate_ShouldWork()
    {
        var info = new DriverInfo("DEV1", "HW1", null, null);
        info.Version.Should().BeNull();
        info.Date.Should().BeNull();
    }

    [Fact]
    public void Equals_SameValues_ShouldBeEqual()
    {
        var v = new Version(2, 0);
        var d = new DateTime(2025, 1, 1);
        var a = new DriverInfo("DEV1", "HW1", v, d);
        var b = new DriverInfo("DEV1", "HW1", v, d);
        a.Should().Be(b);
    }

    [Fact]
    public void GetHashCode_SameValues_ShouldMatch()
    {
        var v = new Version(2, 0);
        var a = new DriverInfo("DEV1", "HW1", v, null);
        var b = new DriverInfo("DEV1", "HW1", v, null);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
