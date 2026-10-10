using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GPUOverclockInfoTests
{
    #region Equality Tests

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var a = new GPUOverclockInfo(100, 200);
        var b = new GPUOverclockInfo(100, 200);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenDifferentCore_ShouldNotBeEqual()
    {
        var a = new GPUOverclockInfo(100, 200);
        var b = new GPUOverclockInfo(101, 200);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenDifferentMemory_ShouldNotBeEqual()
    {
        var a = new GPUOverclockInfo(100, 200);
        var b = new GPUOverclockInfo(100, 201);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithBoxedDifferentType_ShouldReturnFalse()
    {
        var a = new GPUOverclockInfo(100, 200);
        a.Equals("not an overclock info").Should().BeFalse();
    }

    [Fact]
    public void Zero_ShouldHaveZeroValues()
    {
        GPUOverclockInfo.Zero.CoreDeltaMhz.Should().Be(0);
        GPUOverclockInfo.Zero.MemoryDeltaMhz.Should().Be(0);
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_EqualValues_ShouldReturnSameHash()
    {
        var a = new GPUOverclockInfo(100, 200);
        var b = new GPUOverclockInfo(100, 200);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldContainValues()
    {
        var info = new GPUOverclockInfo(100, 200);
        var text = info.ToString();
        text.Should().Contain("100").And.Contain("200");
    }

    #endregion
}
