using System.Drawing;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class ResolutionTests
{
    #region CompareTo Tests

    [Fact]
    public void CompareTo_WhenWidthGreater_ShouldReturnPositive()
    {
        var a = new Resolution(1920, 1080);
        var b = new Resolution(1280, 720);
        a.CompareTo(b).Should().BePositive();
    }

    [Fact]
    public void CompareTo_WhenWidthLesser_ShouldReturnNegative()
    {
        var a = new Resolution(1280, 720);
        var b = new Resolution(1920, 1080);
        a.CompareTo(b).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_WhenSameWidthDifferentHeight_ShouldCompareByHeight()
    {
        var a = new Resolution(1920, 1080);
        var b = new Resolution(1920, 720);
        a.CompareTo(b).Should().BePositive();
    }

    [Fact]
    public void CompareTo_WhenSame_ShouldReturnZero()
    {
        var a = new Resolution(1920, 1080);
        var b = new Resolution(1920, 1080);
        a.CompareTo(b).Should().Be(0);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WhenSameValues_ShouldBeEqual()
    {
        var a = new Resolution(1920, 1080);
        var b = new Resolution(1920, 1080);
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenDifferent_ShouldNotBeEqual()
    {
        var a = new Resolution(1920, 1080);
        var b = new Resolution(3840, 2160);
        a.Equals(b).Should().BeFalse();
    }

    #endregion

    #region DisplayName Tests

    [Fact]
    public void DisplayName_ShouldFormatWithMultiplicationSign()
    {
        var r = new Resolution(1920, 1080);
        r.DisplayName.Should().Be("1920 × 1080");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ShouldFormatWithX()
    {
        var r = new Resolution(1920, 1080);
        r.ToString().Should().Be("1920x1080");
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ImplicitConversionToSize_ShouldWork()
    {
        var r = new Resolution(1920, 1080);
        Size s = r;
        s.Width.Should().Be(1920);
        s.Height.Should().Be(1080);
    }

    [Fact]
    public void ExplicitConversionFromSize_ShouldWork()
    {
        var s = new Size(1920, 1080);
        var r = (Resolution)s;
        r.Width.Should().Be(1920);
        r.Height.Should().Be(1080);
    }

    #endregion
}
