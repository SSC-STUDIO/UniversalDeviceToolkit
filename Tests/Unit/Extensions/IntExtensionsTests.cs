using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class IntExtensionsTests
{
    [Theory]
    [InlineData(0b0001, 0, true)]
    [InlineData(0b0001, 1, false)]
    [InlineData(0b0010, 1, true)]
    [InlineData(0b0010, 0, false)]
    [InlineData(0b1111, 3, true)]
    [InlineData(0b1000, 2, false)]
    [InlineData(0b10101010, 0, false)]
    [InlineData(0b10101010, 1, true)]
    [InlineData(0b10101010, 3, true)]
    [InlineData(0b10101010, 7, true)]
    public void IsBitSet_WithVariousInputs_ShouldReturnExpected(int value, int position, bool expected)
    {
        value.IsBitSet(position).Should().Be(expected);
    }
}
