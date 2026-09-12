using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class DpiScaleTests
{
    #region DisplayName Tests

    [Fact]
    public void DisplayName_ShouldFormatWithPercentSign()
    {
        var scale = new DpiScale(125);
        scale.DisplayName.Should().Be("125%");
    }

    [Fact]
    public void DisplayName_With100Percent_ShouldFormatCorrectly()
    {
        var scale = new DpiScale(100);
        scale.DisplayName.Should().Be("100%");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WhenSameScale_ShouldBeEqual()
    {
        var a = new DpiScale(150);
        var b = new DpiScale(150);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentScale_ShouldNotBeEqual()
    {
        var a = new DpiScale(100);
        var b = new DpiScale(125);
        a.Equals(b).Should().BeFalse();
    }

    #endregion
}
