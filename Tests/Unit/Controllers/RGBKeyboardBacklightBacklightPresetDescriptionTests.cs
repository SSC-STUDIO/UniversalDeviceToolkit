using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class RGBKeyboardBacklightBacklightPresetDescriptionTests
{
    private static readonly RGBColor Red = new(255, 0, 0);
    private static readonly RGBColor Green = new(0, 255, 0);
    private static readonly RGBColor Blue = new(0, 0, 255);
    private static readonly RGBColor White = new(255, 255, 255);

    #region Default

    [Fact]
    public void Default_ShouldHaveStaticEffectSlowestSpeedHighBrightnessWhiteZones()
    {
        var d = RGBKeyboardBacklightBacklightPresetDescription.Default;
        d.Effect.Should().Be(RGBKeyboardBacklightEffect.Static);
        d.Speed.Should().Be(RGBKeyboardBacklightSpeed.Slowest);
        d.Brightness.Should().Be(RGBKeyboardBacklightBrightness.High);
        d.Zone1.Should().Be(RGBColor.White);
        d.Zone2.Should().Be(RGBColor.White);
        d.Zone3.Should().Be(RGBColor.White);
        d.Zone4.Should().Be(RGBColor.White);
    }

    #endregion

    #region Equality

    [Fact]
    public void Equals_AllFieldsSame_ShouldBeEqual()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentEffect_ShouldNotBeEqual()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Breath, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentSpeed_ShouldNotBeEqual()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Fastest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentBrightness_ShouldNotBeEqual()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.Low, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentZone1_ShouldNotBeEqual()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Blue, Green, Blue, White);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_SameValues_ShouldBeTrue()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Smooth, RGBKeyboardBacklightSpeed.Fast,
            RGBKeyboardBacklightBrightness.Low, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Smooth, RGBKeyboardBacklightSpeed.Fast,
            RGBKeyboardBacklightBrightness.Low, Red, Green, Blue, White);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentValues_ShouldBeTrue()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Breath, RGBKeyboardBacklightSpeed.Fast,
            RGBKeyboardBacklightBrightness.Low, Blue, Red, Green, White);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_ShouldBeSame()
    {
        var a = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.WaveRTL, RGBKeyboardBacklightSpeed.Slow,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        var b = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.WaveRTL, RGBKeyboardBacklightSpeed.Slow,
            RGBKeyboardBacklightBrightness.High, Red, Green, Blue, White);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShouldContainAllFieldNames()
    {
        var d = RGBKeyboardBacklightBacklightPresetDescription.Default;
        var s = d.ToString();
        s.Should().Contain("Effect");
        s.Should().Contain("Speed");
        s.Should().Contain("Brightness");
        s.Should().Contain("Zone1");
        s.Should().Contain("Zone2");
        s.Should().Contain("Zone3");
        s.Should().Contain("Zone4");
    }

    #endregion
}
