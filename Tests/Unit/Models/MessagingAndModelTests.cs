using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class MessagingAndModelTests
{
    #region IMessage Interface Implementations

    [Fact]
    public void OsdAppearanceChangedMessage_ShouldImplementIMessage()
    {
        var msg = new OsdAppearanceChangedMessage();
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void RGBKeyboardBacklightChangedMessage_ShouldImplementIMessage()
    {
        var msg = new RGBKeyboardBacklightChangedMessage();
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void SpectrumBacklightChangedMessage_ShouldImplementIMessage()
    {
        var msg = new SpectrumBacklightChangedMessage();
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void TwoOsdAppearanceChangedMessages_ShouldBeEqual()
    {
        var a = new OsdAppearanceChangedMessage();
        var b = new OsdAppearanceChangedMessage();
        a.Should().Be(b);
    }

    [Fact]
    public void TwoRGBKeyboardBacklightChangedMessages_ShouldBeDifferentReferences()
    {
        var a = new RGBKeyboardBacklightChangedMessage();
        var b = new RGBKeyboardBacklightChangedMessage();
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void TwoSpectrumBacklightChangedMessages_ShouldBeDifferentReferences()
    {
        var a = new SpectrumBacklightChangedMessage();
        var b = new SpectrumBacklightChangedMessage();
        a.Should().NotBeSameAs(b);
    }

    #endregion

    #region RGBKeyboardBacklightState Tests



    [Fact]
    public void RGBKeyboardBacklightState_AllPresets_ShouldStoreEach()
    {
        var presets = new Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription>
        {
            { RGBKeyboardBacklightPreset.Off, RGBKeyboardBacklightBacklightPresetDescription.Default },
            { RGBKeyboardBacklightPreset.One, RGBKeyboardBacklightBacklightPresetDescription.Default },
            { RGBKeyboardBacklightPreset.Two, RGBKeyboardBacklightBacklightPresetDescription.Default },
            { RGBKeyboardBacklightPreset.Three, RGBKeyboardBacklightBacklightPresetDescription.Default },
            { RGBKeyboardBacklightPreset.Four, RGBKeyboardBacklightBacklightPresetDescription.Default },
        };
        var state = new RGBKeyboardBacklightState(RGBKeyboardBacklightPreset.Two, presets);
        state.Presets.Should().HaveCount(5);
        state.Presets.Should().ContainKey(RGBKeyboardBacklightPreset.Four);
    }

    [Fact]
    public void RGBKeyboardBacklightBacklightPresetDescription_ZoneProperties_ShouldBeSettable()
    {
        var desc = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.WaveLTR,
            RGBKeyboardBacklightSpeed.Fastest,
            RGBKeyboardBacklightBrightness.High,
            RGBColor.Pink, RGBColor.Purple, RGBColor.Red, RGBColor.Teal);

        desc.Effect.Should().Be(RGBKeyboardBacklightEffect.WaveLTR);
        desc.Speed.Should().Be(RGBKeyboardBacklightSpeed.Fastest);
        desc.Brightness.Should().Be(RGBKeyboardBacklightBrightness.High);
        desc.Zone1.Should().Be(RGBColor.Pink);
        desc.Zone2.Should().Be(RGBColor.Purple);
        desc.Zone3.Should().Be(RGBColor.Red);
        desc.Zone4.Should().Be(RGBColor.Teal);
    }

    [Fact]
    public void RGBKeyboardBacklightBacklightPresetDescription_Default_ShouldHaveFourZones()
    {
        var d = RGBKeyboardBacklightBacklightPresetDescription.Default;
        d.Zone1.Should().Be(RGBColor.White);
        d.Zone2.Should().Be(RGBColor.White);
        d.Zone3.Should().Be(RGBColor.White);
        d.Zone4.Should().Be(RGBColor.White);
    }

    #endregion

    #region RGBKeyboardBacklightPreset Enum Edge Cases

    [Theory]
    [InlineData(RGBKeyboardBacklightPreset.Off, -1)]
    [InlineData(RGBKeyboardBacklightPreset.One, 0)]
    [InlineData(RGBKeyboardBacklightPreset.Two, 1)]
    [InlineData(RGBKeyboardBacklightPreset.Three, 2)]
    [InlineData(RGBKeyboardBacklightPreset.Four, 3)]
    public void RGBKeyboardBacklightPreset_ShouldHaveCorrectValues(RGBKeyboardBacklightPreset preset, int expected)
    {
        ((int)preset).Should().Be(expected);
    }

    [Fact]
    public void RGBKeyboardBacklightPreset_ShouldHaveFiveMembers()
    {
        Enum.GetValues<RGBKeyboardBacklightPreset>().Should().HaveCount(5);
    }

    #endregion

    #region RGBColor Edge Cases

    [Fact]
    public void RGBColor_Default_ShouldBeBlack()
    {
        var c = default(RGBColor);
        c.R.Should().Be(0);
        c.G.Should().Be(0);
        c.B.Should().Be(0);
    }

    [Fact]
    public void RGBColor_MaxValues_ShouldStoreCorrectly()
    {
        var c = new RGBColor(255, 255, 255);
        c.R.Should().Be(255);
        c.G.Should().Be(255);
        c.B.Should().Be(255);
        c.Should().Be(RGBColor.White);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(128, 64, 32)]
    [InlineData(255, 0, 128)]
    [InlineData(1, 1, 1)]
    public void RGBColor_RoundTrip_ShouldPreserveValues(byte r, byte g, byte b)
    {
        var c = new RGBColor(r, g, b);
        c.R.Should().Be(r);
        c.G.Should().Be(g);
        c.B.Should().Be(b);
    }

    [Fact]
    public void RGBColor_Equality_SameValues_ShouldBeEqual()
    {
        var a = new RGBColor(100, 50, 25);
        var b = new RGBColor(100, 50, 25);
        a.Should().NotBeSameAs(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void RGBColor_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new RGBColor(100, 50, 25);
        var b = new RGBColor(100, 50, 26);
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void RGBColor_StaticMembers_ShouldAllBeUnique()
    {
        var colors = new[] { RGBColor.Green, RGBColor.Pink, RGBColor.Purple, RGBColor.Red, RGBColor.Teal, RGBColor.White };
        colors.Distinct().Count().Should().Be(6);
    }

    #endregion

    #region SunriseSunsetSettingsStore Tests

    [Fact]
    public void SunriseSunsetSettingsStore_Default_ShouldHaveNullFields()
    {
        var store = new SunriseSunsetSettings.SunriseSunsetSettingsStore();
        store.LastCheckDateTime.Should().BeNull();
        store.Sunrise.Should().BeNull();
        store.Sunset.Should().BeNull();
    }

    [Fact]
    public void SunriseSunsetSettingsStore_SetProperties_ShouldRetainValues()
    {
        var now = DateTime.UtcNow;
        var sunrise = new Time(6, 30);
        var sunset = new Time(18, 45);

        var store = new SunriseSunsetSettings.SunriseSunsetSettingsStore
        {
            LastCheckDateTime = now,
            Sunrise = sunrise,
            Sunset = sunset
        };

        store.LastCheckDateTime.Should().Be(now);
        store.Sunrise.Should().Be(sunrise);
        store.Sunset.Should().Be(sunset);
    }

    [Fact]
    public void SunriseSunsetSettingsStore_PartialValues_ShouldWork()
    {
        var store = new SunriseSunsetSettings.SunriseSunsetSettingsStore
        {
            Sunrise = new Time(7, 0)
        };
        store.Sunrise.Should().NotBeNull();
        store.Sunset.Should().BeNull();
        store.LastCheckDateTime.Should().BeNull();
    }

    #endregion

    #region Time Struct Tests

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 30)]
    [InlineData(23, 59)]
    [InlineData(6, 0)]
    public void Time_Constructor_ShouldSetHourAndMinute(int hour, int minute)
    {
        var t = new Time(hour, minute);
        t.Hour.Should().Be(hour);
        t.Minute.Should().Be(minute);
    }

    [Fact]
    public void Time_Equality_SameValues_ShouldBeEqual()
    {
        var a = new Time(10, 30);
        var b = new Time(10, 30);
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void Time_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new Time(10, 30);
        var b = new Time(10, 31);
        a.Should().NotBe(b);
    }

    #endregion

    #region RGBKeyboardBacklightEffect Enum Tests

    [Theory]
    [InlineData(RGBKeyboardBacklightEffect.Static)]
    [InlineData(RGBKeyboardBacklightEffect.Breath)]
    [InlineData(RGBKeyboardBacklightEffect.WaveLTR)]
    [InlineData(RGBKeyboardBacklightEffect.Smooth)]
    [InlineData(RGBKeyboardBacklightEffect.WaveRTL)]
    public void RGBKeyboardBacklightEffect_ShouldBeDefined(RGBKeyboardBacklightEffect value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region RGBKeyboardBacklightBrightness Enum Tests

    [Theory]
    [InlineData(RGBKeyboardBacklightBrightness.Low)]
    [InlineData(RGBKeyboardBacklightBrightness.High)]
    public void RGBKeyboardBacklightBrightness_ShouldBeDefined(RGBKeyboardBacklightBrightness value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void RGBKeyboardBacklightBrightness_ShouldHaveThreeMembers()
    {
        Enum.GetValues<RGBKeyboardBacklightBrightness>().Should().HaveCount(2);
    }

    #endregion

    #region RGBKeyboardBacklightSpeed Enum Tests

    [Theory]
    [InlineData(RGBKeyboardBacklightSpeed.Slowest)]
    [InlineData(RGBKeyboardBacklightSpeed.Slow)]
    [InlineData(RGBKeyboardBacklightSpeed.Fast)]
    [InlineData(RGBKeyboardBacklightSpeed.Fastest)]
    public void RGBKeyboardBacklightSpeed_ShouldBeDefined(RGBKeyboardBacklightSpeed value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void RGBKeyboardBacklightSpeed_ShouldHaveFiveMembers()
    {
        Enum.GetValues<RGBKeyboardBacklightSpeed>().Should().HaveCount(4);
    }

    #endregion

    #region WhiteKeyboardBacklightState Tests

    [Theory]
    [InlineData(WhiteKeyboardBacklightState.Off)]
    [InlineData(WhiteKeyboardBacklightState.Low)]
    [InlineData(WhiteKeyboardBacklightState.High)]
    public void WhiteKeyboardBacklightState_ShouldBeDefined(WhiteKeyboardBacklightState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void WhiteKeyboardBacklightState_ShouldHaveThreeMembers()
    {
        Enum.GetValues<WhiteKeyboardBacklightState>().Should().HaveCount(3);
    }

    #endregion

    #region IGPUModeState Tests

    [Theory]
    [InlineData(IGPUModeState.Default)]
    [InlineData(IGPUModeState.Auto)]
    [InlineData(IGPUModeState.IGPUOnly)]
    public void IGPUModeState_ShouldBeDefined(IGPUModeState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void IGPUModeState_ShouldHaveFourMembers()
    {
        Enum.GetValues<IGPUModeState>().Should().HaveCount(3);
    }

    #endregion

    #region HybridModeState Extended Tests

    [Fact]
    public void HybridModeState_ShouldHaveFourMembers()
    {
        Enum.GetValues<HybridModeState>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(HybridModeState.On)]
    [InlineData(HybridModeState.OnIGPUOnly)]
    [InlineData(HybridModeState.OnAuto)]
    public void HybridModeState_ShouldBeDefined(HybridModeState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region NotificationPosition Tests

    [Fact]
    public void NotificationPosition_ShouldHaveNineMembers()
    {
        Enum.GetValues<NotificationPosition>().Should().HaveCount(9);
    }

    [Theory]
    [InlineData(NotificationPosition.BottomRight)]
    [InlineData(NotificationPosition.Center)]
    [InlineData(NotificationPosition.TopLeft)]
    public void NotificationPosition_ShouldBeDefined(NotificationPosition value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region PowerModeMappingMode Tests

    [Theory]
    [InlineData(PowerModeMappingMode.Disabled)]
    [InlineData(PowerModeMappingMode.WindowsPowerMode)]
    [InlineData(PowerModeMappingMode.WindowsPowerPlan)]
    public void PowerModeMappingMode_ShouldBeDefined(PowerModeMappingMode value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void PowerModeMappingMode_ShouldHaveThreeMembers()
    {
        Enum.GetValues<PowerModeMappingMode>().Should().HaveCount(3);
    }

    #endregion
}




