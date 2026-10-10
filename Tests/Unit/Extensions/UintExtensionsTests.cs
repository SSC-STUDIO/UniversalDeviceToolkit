using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class UintExtensionsTests
{
    [Theory]
    [InlineData(0x01020304u, 0x04030201u)]
    [InlineData(0x00000001u, 0x01000000u)]
    [InlineData(0xFF000000u, 0x000000FFu)]
    [InlineData(0x00000000u, 0x00000000u)]
    [InlineData(0xDEADBEEFu, 0xEFBEADDEu)]
    public void ReverseEndianness_ShouldSwapBytes(uint input, uint expected)
    {
        input.ReverseEndianness().Should().Be(expected);
    }

    [Fact]
    public void GetNthBit_ShouldReturnCorrectBit()
    {
        uint value = 0b1010;
        value.GetNthBit(0).Should().BeFalse();
        value.GetNthBit(1).Should().BeTrue();
        value.GetNthBit(2).Should().BeFalse();
        value.GetNthBit(3).Should().BeTrue();
    }

    [Fact]
    public void SetNthBit_WhenSettingTrue_ShouldSetBit()
    {
        uint value = 0b0000;
        value.SetNthBit(2, true).Should().Be(0b0100u);
    }

    [Fact]
    public void SetNthBit_WhenSettingFalse_ShouldClearBit()
    {
        uint value = 0b1111;
        value.SetNthBit(2, false).Should().Be(0b1011u);
    }

    [Fact]
    public void SetNthBit_SettingSameBitTwice_ShouldBeIdempotent()
    {
        uint value = 0;
        var afterSet = value.SetNthBit(5, true);
        afterSet.SetNthBit(5, true).Should().Be(afterSet);
    }
}
