using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class CompatibilityIsLegionTests
{
    [Theory]
    [InlineData(LegionSeries.Legion_5)]
    [InlineData(LegionSeries.Legion_Pro_5)]
    [InlineData(LegionSeries.Lenovo_Slim)]
    [InlineData(LegionSeries.Legion_Slim_5)]
    [InlineData(LegionSeries.Legion_7)]
    [InlineData(LegionSeries.Legion_Pro_7)]
    [InlineData(LegionSeries.Legion_9)]
    [InlineData(LegionSeries.Legion_Go)]
    [InlineData(LegionSeries.LOQ)]
    [InlineData(LegionSeries.Legion_Legacy)]
    public void IsLegion_KnownLegionSeries_ShouldReturnTrue(LegionSeries series)
    {
        Compatibility.IsLegion(series).Should().BeTrue();
    }

    [Theory]
    [InlineData(LegionSeries.IdeaPad)]
    [InlineData(LegionSeries.IdeaPad_Gaming)]
    [InlineData(LegionSeries.YOGA)]
    [InlineData(LegionSeries.ThinkBook)]
    [InlineData(LegionSeries.Unknown)]
    public void IsLegion_NonLegionSeries_ShouldReturnFalse(LegionSeries series)
    {
        Compatibility.IsLegion(series).Should().BeFalse();
    }
}