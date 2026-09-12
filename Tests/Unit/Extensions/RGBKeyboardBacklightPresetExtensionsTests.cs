using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class RGBKeyboardBacklightPresetExtensionsTests
{
    [Theory]
    [InlineData(RGBKeyboardBacklightPreset.Off, RGBKeyboardBacklightPreset.One)]
    [InlineData(RGBKeyboardBacklightPreset.One, RGBKeyboardBacklightPreset.Two)]
    [InlineData(RGBKeyboardBacklightPreset.Two, RGBKeyboardBacklightPreset.Three)]
    [InlineData(RGBKeyboardBacklightPreset.Three, RGBKeyboardBacklightPreset.Four)]
    [InlineData(RGBKeyboardBacklightPreset.Four, RGBKeyboardBacklightPreset.Off)]
    public void Next_ShouldCycleInExpectedOrder(RGBKeyboardBacklightPreset current, RGBKeyboardBacklightPreset expected)
    {
        var result = current.Next();

        result.Should().Be(expected);
    }

    [Fact]
    public void Next_ShouldCycleThroughAllValues()
    {
        var visited = new System.Collections.Generic.HashSet<RGBKeyboardBacklightPreset>();
        var current = RGBKeyboardBacklightPreset.Off;

        for (var i = 0; i < 5; i++)
        {
            visited.Add(current);
            current = current.Next();
        }

        // After 5 steps from Off, we should be back to Off and visited all 5 presets
        current.Should().Be(RGBKeyboardBacklightPreset.Off);
        visited.Should().HaveCount(5);
        visited.Should().Contain(RGBKeyboardBacklightPreset.Off);
        visited.Should().Contain(RGBKeyboardBacklightPreset.One);
        visited.Should().Contain(RGBKeyboardBacklightPreset.Two);
        visited.Should().Contain(RGBKeyboardBacklightPreset.Three);
        visited.Should().Contain(RGBKeyboardBacklightPreset.Four);
    }
}
