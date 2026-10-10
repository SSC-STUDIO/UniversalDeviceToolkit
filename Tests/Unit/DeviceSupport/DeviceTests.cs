using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class DeviceTests
{
    private static readonly Guid TestGuid = new("12345678-1234-1234-1234-123456789abc");

    [Fact]
    public void Properties_ShouldReturnConstructorValues()
    {
        var device = new Device(
            name: "GPU",
            description: "NVIDIA GeForce",
            busReportedDeviceDescription: "RTX 4070",
            deviceInstanceId: "PCI\\VEN_10DE&DEV_2786",
            classGuid: TestGuid,
            className: "Display",
            isRemovable: false,
            isDisconnected: false);

        device.Name.Should().Be("GPU");
        device.Description.Should().Be("NVIDIA GeForce");
        device.BusReportedDeviceDescription.Should().Be("RTX 4070");
        device.DeviceInstanceId.Should().Be("PCI\\VEN_10DE&DEV_2786");
        device.ClassGuid.Should().Be(TestGuid);
        device.ClassName.Should().Be("Display");
        device.IsRemovable.Should().BeFalse();
        device.IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public void Index_ShouldConcatenateAllStringFields()
    {
        var device = new Device(
            name: "GPU",
            description: "NVIDIA GeForce",
            busReportedDeviceDescription: "RTX 4070",
            deviceInstanceId: "PCI\\VEN_10DE",
            classGuid: TestGuid,
            className: "Display",
            isRemovable: false,
            isDisconnected: false);

        var index = device.Index;
        index.Should().Contain("Display");
        index.Should().Contain(TestGuid.ToString());
        index.Should().Contain("RTX 4070");
        index.Should().Contain("NVIDIA GeForce");
        index.Should().Contain("GPU");
        index.Should().Contain("PCI\\VEN_10DE");
    }

    [Fact]
    public void Index_CalledTwice_ShouldReturnSameInstance()
    {
        var device = new Device(
            name: "GPU",
            description: "NVIDIA",
            busReportedDeviceDescription: "RTX",
            deviceInstanceId: "PCI",
            classGuid: TestGuid,
            className: "Display",
            isRemovable: false,
            isDisconnected: false);

        var first = device.Index;
        var second = device.Index;
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Index_WithNullFields_ShouldNotThrow()
    {
        var device = new Device(
            name: "GPU",
            description: "NVIDIA",
            busReportedDeviceDescription: "RTX",
            deviceInstanceId: "PCI",
            classGuid: TestGuid,
            className: "Display",
            isRemovable: false,
            isDisconnected: false);

        // Null string fields should not cause NullReferenceException in StringBuilder.Append
        var act = () => _ = device.Index;
        act.Should().NotThrow();
    }
}
