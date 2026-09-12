using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class SpectrumKeyboardBacklightEffectTypeExtensionsTests
{
    [Theory]
    [InlineData(SpectrumKeyboardBacklightEffectType.AudioBounce, true)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AudioRipple, true)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AuroraSync, true)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Always, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.RainbowScrew, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.RainbowWave, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.ColorChange, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.ColorWave, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.ColorPulse, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Smooth, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Rain, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Ripple, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Type, false)]
    public void IsAllLightsEffect_ShouldClassifyCorrectly(SpectrumKeyboardBacklightEffectType type, bool expected)
    {
        var result = type.IsAllLightsEffect();

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SpectrumKeyboardBacklightEffectType.Type, true)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Ripple, true)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Always, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.RainbowScrew, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.RainbowWave, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.ColorChange, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.ColorWave, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.ColorPulse, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Smooth, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.Rain, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AudioBounce, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AudioRipple, false)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AuroraSync, false)]
    public void IsWholeKeyboardEffect_ShouldClassifyCorrectly(SpectrumKeyboardBacklightEffectType type, bool expected)
    {
        var result = type.IsWholeKeyboardEffect();

        result.Should().Be(expected);
    }
}
