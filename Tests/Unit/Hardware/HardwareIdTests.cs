using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class HardwareIdTests
{
    #region Equality Tests

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var a = new HardwareId("8086", "1234");
        var b = new HardwareId("8086", "1234");
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenCaseDifferent_ShouldStillBeEqual()
    {
        var a = new HardwareId("8086", "ABCD");
        var b = new HardwareId("8086", "abcd");
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentVendor_ShouldNotBeEqual()
    {
        var a = new HardwareId("8086", "1234");
        var b = new HardwareId("10DE", "1234");
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenDifferentDevice_ShouldNotBeEqual()
    {
        var a = new HardwareId("8086", "1234");
        var b = new HardwareId("8086", "5678");
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithDefaultStruct_ShouldNotThrow()
    {
        var a = new HardwareId("8086", "1234");
        var b = default(HardwareId);
        var act = () => a.Equals(b);
        act.Should().NotThrow();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_EqualValues_ShouldReturnSameHash()
    {
        var a = new HardwareId("8086", "1234");
        var b = new HardwareId("8086", "1234");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion
}
