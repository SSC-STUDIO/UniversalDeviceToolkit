using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

/// <summary>
/// Domain/firmware value contracts that are not covered by dedicated feature tests.
/// Extracted from bulk struct/enum padding files before those were removed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DomainValueContractTests
{
    [Theory]
    [InlineData(BootLogoFormat.Jpeg, 0x01)]
    [InlineData(BootLogoFormat.Bmp, 0x10)]
    [InlineData(BootLogoFormat.Png, 0x20)]
    public void BootLogoFormat_ShouldKeepFirmwareBitFlags(BootLogoFormat format, byte expected)
    {
        ((byte)format).Should().Be(expected);
    }

    [Fact]
    public void BootLogoFormat_AllSupportedFlags_ShouldCombineTo0x31()
    {
        var all = BootLogoFormat.Jpeg | BootLogoFormat.Bmp | BootLogoFormat.Png;
        ((byte)all).Should().Be(0x31);
        all.Should().HaveFlag(BootLogoFormat.Jpeg)
            .And.HaveFlag(BootLogoFormat.Bmp)
            .And.HaveFlag(BootLogoFormat.Png);
    }

    [Theory]
    [InlineData(DriverKey.FnF10, 32)]
    [InlineData(DriverKey.FnSpace, 4096)]
    public void DriverKey_ShouldKeepKnownScanCodes(DriverKey key, int expected)
    {
        ((int)key).Should().Be(expected);
    }

    [Theory]
    [InlineData(FanType.Cpu, 0)]
    [InlineData(FanType.Gpu, 1)]
    public void FanType_ShouldKeepStableOrdering(FanType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData(LegionSeries.IdeaPad, 9)]
    [InlineData(LegionSeries.IdeaPad_Gaming, 10)]
    [InlineData(LegionSeries.ThinkBook, 13)]
    public void LegionSeries_ExtendedIds_ShouldStayStable(LegionSeries series, int expected)
    {
        ((int)series).Should().Be(expected);
    }

    [Theory]
    [InlineData(LampEffectType.Static, 0)]
    [InlineData(LampEffectType.AuroraSync, 11)]
    public void LampEffectType_ShouldKeepStableEndpoints(LampEffectType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Theory]
    [InlineData(SpectrumKeyboardBacklightEffectType.AudioBounce)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AudioRipple)]
    [InlineData(SpectrumKeyboardBacklightEffectType.AuroraSync)]
    public void SpectrumAllLightsEffects_ShouldClearKeysArray(SpectrumKeyboardBacklightEffectType type)
    {
        ushort[] keys = [1, 2, 3, 4];
        var effect = new SpectrumKeyboardBacklightEffect(
            type,
            SpectrumKeyboardBacklightSpeed.Speed2,
            SpectrumKeyboardBacklightDirection.None,
            SpectrumKeyboardBacklightClockwiseDirection.None,
            [],
            keys);

        effect.Keys.Should().BeEmpty(because: $"{type} is an all-lights effect and must not retain per-key maps");
        effect.Type.Should().Be(type);
    }

    [Fact]
    public void SpectrumNonAllLightsEffects_ShouldRetainKeys()
    {
        ushort[] keys = [1, 2, 3];
        var effect = new SpectrumKeyboardBacklightEffect(
            SpectrumKeyboardBacklightEffectType.Always,
            SpectrumKeyboardBacklightSpeed.None,
            SpectrumKeyboardBacklightDirection.BottomToTop,
            SpectrumKeyboardBacklightClockwiseDirection.None,
            [],
            keys);

        effect.Keys.Should().ContainInOrder(1, 2, 3);
    }
}
