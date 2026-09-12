using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class MathExtensionsTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(3, 10, 0)]
    [InlineData(4, 10, 0)]
    [InlineData(5, 10, 10)]
    [InlineData(6, 10, 10)]
    [InlineData(14, 10, 10)]
    [InlineData(15, 10, 20)]
    [InlineData(7, 5, 5)]
    [InlineData(12, 5, 10)]
    [InlineData(13, 5, 15)]
    [InlineData(100, 25, 100)]
    [InlineData(113, 25, 125)]
    [InlineData(137, 25, 125)]
    public void RoundNearest_WithVariousInputs_ShouldRoundCorrectly(int value, int factor, int expected)
    {
        MathExtensions.RoundNearest(value, factor).Should().Be(expected);
    }

    [Fact]
    public void RoundNearest_WithNegativeValue_ShouldRoundAwayFromZero()
    {
        MathExtensions.RoundNearest(-7, 5).Should().Be(-5);
        MathExtensions.RoundNearest(-8, 5).Should().Be(-10);
    }
}
