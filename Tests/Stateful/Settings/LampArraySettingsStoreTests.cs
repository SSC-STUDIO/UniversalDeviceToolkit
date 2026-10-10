using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class LampArraySettingsStoreTests
{
    #region LampEffectConfig Tests

    [Fact]
    public void LampEffectConfig_Defaults_ShouldBeRainbow()
    {
        var config = new LampArraySettings.LampEffectConfig();
        config.EffectType.Should().Be(LampEffectType.Rainbow);
        config.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void LampEffectConfig_SetValues_ShouldWork()
    {
        var config = new LampArraySettings.LampEffectConfig
        {
            EffectType = LampEffectType.Static,
            Parameters = new Dictionary<string, object> { { "color", "#FF0000" }, { "speed", 1.5 } }
        };
        config.EffectType.Should().Be(LampEffectType.Static);
        config.Parameters.Should().HaveCount(2);
        config.Parameters["color"].Should().Be("#FF0000");
    }

    #endregion

    #region LampArraySettingsStore Tests

    [Fact]
    public void LampArraySettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new LampArraySettings.LampArraySettingsStore();
        store.Brightness.Should().Be(1.0);
        store.Speed.Should().Be(1.0);
        store.SmoothTransition.Should().BeTrue();
        store.DefaultEffect.Should().BeNull();
        store.PerLampEffects.Should().BeEmpty();
    }

    [Fact]
    public void LampArraySettingsStore_SetValues_ShouldWork()
    {
        var effectConfig = new LampArraySettings.LampEffectConfig
        {
            EffectType = LampEffectType.Wave,
            Parameters = new Dictionary<string, object> { { "direction", "left" } }
        };
        var store = new LampArraySettings.LampArraySettingsStore
        {
            Brightness = 0.5,
            Speed = 2.0,
            SmoothTransition = false,
            DefaultEffect = effectConfig,
            PerLampEffects = new Dictionary<int, LampArraySettings.LampEffectConfig>
            {
                { 0, new() { EffectType = LampEffectType.Breathe } },
                { 1, new() { EffectType = LampEffectType.Meteor } }
            }
        };

        store.Brightness.Should().Be(0.5);
        store.Speed.Should().Be(2.0);
        store.SmoothTransition.Should().BeFalse();
        store.DefaultEffect.Should().NotBeNull();
        store.DefaultEffect!.EffectType.Should().Be(LampEffectType.Wave);
        store.PerLampEffects.Should().HaveCount(2);
        store.PerLampEffects[0].EffectType.Should().Be(LampEffectType.Breathe);
        store.PerLampEffects[1].EffectType.Should().Be(LampEffectType.Meteor);
    }

    #endregion

    #region LampEffectType Enum Tests

    [Theory]
    [InlineData(LampEffectType.Static)]
    [InlineData(LampEffectType.Breathe)]
    [InlineData(LampEffectType.Wave)]
    [InlineData(LampEffectType.Rainbow)]
    [InlineData(LampEffectType.Meteor)]
    [InlineData(LampEffectType.Ripple)]
    [InlineData(LampEffectType.Sparkle)]
    [InlineData(LampEffectType.Gradient)]
    [InlineData(LampEffectType.CustomPattern)]
    public void LampEffectType_ShouldContainExpectedValues(LampEffectType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    #endregion
}

