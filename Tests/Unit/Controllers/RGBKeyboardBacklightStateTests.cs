using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class RGBKeyboardBacklightStateTests
{
    #region RGBKeyboardBacklightBacklightPresetDescription Tests

    [Fact]
    public void PresetDescription_Constructor_ShouldSetFields()
    {
        var desc = new RGBKeyboardBacklightBacklightPresetDescription(
            RGBKeyboardBacklightEffect.Static,
            RGBKeyboardBacklightSpeed.Slowest,
            RGBKeyboardBacklightBrightness.High,
            RGBColor.Red, RGBColor.Green, RGBColor.Teal, RGBColor.White);

        desc.Effect.Should().Be(RGBKeyboardBacklightEffect.Static);
        desc.Speed.Should().Be(RGBKeyboardBacklightSpeed.Slowest);
        desc.Brightness.Should().Be(RGBKeyboardBacklightBrightness.High);
        desc.Zone1.Should().Be(RGBColor.Red);
        desc.Zone2.Should().Be(RGBColor.Green);
        desc.Zone3.Should().Be(RGBColor.Teal);
        desc.Zone4.Should().Be(RGBColor.White);
    }

    [Fact]
    public void PresetDescription_Default_ShouldHaveExpectedValues()
    {
        var desc = RGBKeyboardBacklightBacklightPresetDescription.Default;
        desc.Effect.Should().Be(RGBKeyboardBacklightEffect.Static);
        desc.Speed.Should().Be(RGBKeyboardBacklightSpeed.Slowest);
        desc.Brightness.Should().Be(RGBKeyboardBacklightBrightness.High);
    }

    #endregion

    #region RGBKeyboardBacklightState Tests

    [Fact]
    public void State_Constructor_ShouldSetFields()
    {
        var presets = new Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription>
        {
            { RGBKeyboardBacklightPreset.One, RGBKeyboardBacklightBacklightPresetDescription.Default }
        };
        var state = new RGBKeyboardBacklightState(RGBKeyboardBacklightPreset.One, presets);

        state.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.One);
        state.Presets.Should().HaveCount(1);
    }

    [Fact]
    public void State_EmptyPresets_ShouldWork()
    {
        var state = new RGBKeyboardBacklightState(RGBKeyboardBacklightPreset.Off, []);
        state.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Off);
        state.Presets.Should().BeEmpty();
    }

    #endregion

    #region RGBKeyboardBacklightPreset Enum Tests

    [Theory]
    [InlineData(RGBKeyboardBacklightPreset.Off)]
    [InlineData(RGBKeyboardBacklightPreset.One)]
    [InlineData(RGBKeyboardBacklightPreset.Two)]
    [InlineData(RGBKeyboardBacklightPreset.Three)]
    public void RGBKeyboardBacklightPreset_ShouldContainExpectedValues(RGBKeyboardBacklightPreset preset)
    {
        Enum.IsDefined(preset).Should().BeTrue();
    }

    #endregion
}

