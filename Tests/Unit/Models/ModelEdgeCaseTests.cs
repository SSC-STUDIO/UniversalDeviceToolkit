using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

/// <summary>
/// Edge cases for core value types extracted from bulk "AdditionalEnumAndModel" padding.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ModelEdgeCaseTests
{
    [Fact]
    public void ProcessInfo_Constructor_NullPath_ShouldWork()
    {
        var info = new ProcessInfo("test", null);
        info.Name.Should().Be("test");
        info.ExecutablePath.Should().BeNull();
    }

    [Fact]
    public void ProcessInfo_FromPath_EmptyString_ShouldWork()
    {
        var info = ProcessInfo.FromPath("");
        info.Name.Should().Be("");
        info.ExecutablePath.Should().Be("");
    }

    [Fact]
    public void ProcessInfo_ToString_ShouldContainNameAndPath()
    {
        var info = new ProcessInfo("MyApp", @"C:\MyApp.exe");
        info.ToString().Should().Contain("MyApp").And.Contain(@"C:\MyApp.exe");
    }

    [Fact]
    public void Device_Constructor_ShouldSetAllProperties()
    {
        var guid = Guid.NewGuid();
        var device = new Device(
            "TestDevice", "A test device", "Bus Description",
            "PCI\\VEN_10DE", guid, "Display", true, true);

        device.Name.Should().Be("TestDevice");
        device.Description.Should().Be("A test device");
        device.BusReportedDeviceDescription.Should().Be("Bus Description");
        device.DeviceInstanceId.Should().Be("PCI\\VEN_10DE");
        device.ClassGuid.Should().Be(guid);
        device.ClassName.Should().Be("Display");
        device.IsRemovable.Should().BeTrue();
        device.IsDisconnected.Should().BeTrue();
    }

    [Fact]
    public void FanTableData_WithLargeArrays_ShouldWork()
    {
        ushort[] speeds = new ushort[100];
        ushort[] temps = new ushort[100];
        for (int i = 0; i < 100; i++)
        {
            speeds[i] = (ushort)(i * 100);
            temps[i] = (ushort)i;
        }

        var data = new FanTableData(FanTableType.CPU, 0, 0, speeds, temps);
        data.FanSpeeds.Should().HaveCount(100);
        data.Temps.Should().HaveCount(100);
        data.FanSpeeds[99].Should().Be(9900);
        data.Temps[99].Should().Be(99);
    }

    [Fact]
    public void DriverInfo_DifferentDates_ShouldNotBeEqual()
    {
        var a = new DriverInfo("DEV1", "HW1", null, new DateTime(2020, 1, 1));
        var b = new DriverInfo("DEV1", "HW1", null, new DateTime(2025, 1, 1));
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void WindowsPowerPlan_Equality_IsByGuidNotName()
    {
        var guid = Guid.NewGuid();
        var a = new WindowsPowerPlan(guid, "Balanced", true);
        var b = new WindowsPowerPlan(guid, "High Performance", false);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();

        var c = new WindowsPowerPlan(Guid.NewGuid(), "Balanced", true);
        a.Equals(c).Should().BeFalse();
    }
}
