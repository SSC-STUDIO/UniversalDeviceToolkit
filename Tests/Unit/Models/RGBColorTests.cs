using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class RGBColorTests
{
    [Fact]
    public void Constructor_ShouldSetRGBValues()
    {
        var color = new RGBColor(100, 200, 50);
        color.R.Should().Be(100);
        color.G.Should().Be(200);
        color.B.Should().Be(50);
    }

    [Fact]
    public void StaticColors_ShouldHaveExpectedValues()
    {
        RGBColor.Red.R.Should().Be(255);
        RGBColor.Red.G.Should().Be(0);
        RGBColor.Red.B.Should().Be(0);

        RGBColor.White.R.Should().Be(255);
        RGBColor.White.G.Should().Be(255);
        RGBColor.White.B.Should().Be(255);
    }

    [Fact]
    public void Equals_SameRGB_ShouldBeEqual()
    {
        var c1 = new RGBColor(100, 200, 50);
        var c2 = new RGBColor(100, 200, 50);

        c1.Equals(c2).Should().BeTrue();
        (c1 == c2).Should().BeTrue();
        (c1 != c2).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentRGB_ShouldNotBeEqual()
    {
        var c1 = new RGBColor(100, 200, 50);
        var c2 = new RGBColor(100, 200, 51);

        c1.Equals(c2).Should().BeFalse();
        (c1 != c2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ShouldNotBeEqual()
    {
        var c1 = new RGBColor(100, 200, 50);
        c1.Equals("not a color").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameRGB_ShouldMatch()
    {
        var c1 = new RGBColor(100, 200, 50);
        var c2 = new RGBColor(100, 200, 50);

        c1.GetHashCode().Should().Be(c2.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldContainRGB()
    {
        var c = new RGBColor(100, 200, 50);
        var str = c.ToString();
        str.Should().Contain("100");
        str.Should().Contain("200");
        str.Should().Contain("50");
    }

    [Fact]
    public void BoundaryValues_ShouldWork()
    {
        var min = new RGBColor(0, 0, 0);
        var max = new RGBColor(255, 255, 255);
        min.R.Should().Be(0);
        max.B.Should().Be(255);
    }
}
