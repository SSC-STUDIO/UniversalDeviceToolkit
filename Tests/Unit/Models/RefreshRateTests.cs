using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class RefreshRateTests
{
    #region DisplayName Tests

    [Fact]
    public void DisplayName_ShouldFormatWithHz()
    {
        var rate = new RefreshRate(144);
        rate.DisplayName.Should().Be("144 Hz");
    }

    [Fact]
    public void DisplayName_With60Hz_ShouldFormatCorrectly()
    {
        var rate = new RefreshRate(60);
        rate.DisplayName.Should().Be("60 Hz");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WhenSameFrequency_ShouldBeEqual()
    {
        var a = new RefreshRate(144);
        var b = new RefreshRate(144);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentFrequency_ShouldNotBeEqual()
    {
        var a = new RefreshRate(60);
        var b = new RefreshRate(144);
        a.Equals(b).Should().BeFalse();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldFormatWithHzSuffix()
    {
        var rate = new RefreshRate(60);
        rate.ToString().Should().Be("60Hz");
    }

    #endregion
}
