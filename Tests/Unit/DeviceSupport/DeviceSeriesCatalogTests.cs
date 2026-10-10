using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public sealed class DeviceSeriesCatalogTests
{
    private static readonly MethodInfo SeriesResolver = typeof(Compatibility).GetMethod("GetLegionSeries", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The device series resolver was not found.");

    private static readonly IReadOnlyDictionary<string, LegionSeries> CatalogSeries = new Dictionary<string, LegionSeries>(StringComparer.OrdinalIgnoreCase)
    {
        ["lenovo-legion-pro-7"] = LegionSeries.Legion_Pro_7,
        ["lenovo-legion-pro-5"] = LegionSeries.Legion_Pro_5,
        ["lenovo-legion-9"] = LegionSeries.Legion_9,
        ["lenovo-legion-7"] = LegionSeries.Legion_7,
        ["lenovo-legion-slim-5"] = LegionSeries.Legion_Slim_5,
        ["lenovo-legion-go"] = LegionSeries.Legion_Go,
        ["lenovo-loq"] = LegionSeries.LOQ,
        ["lenovo-legion-5"] = LegionSeries.Legion_5,
        ["lenovo-ideapad-gaming"] = LegionSeries.IdeaPad_Gaming,
        ["lenovo-legacy-limited"] = LegionSeries.Legion_Legacy
    };

    [Theory]
    [InlineData("83JG", LegionSeries.LOQ)]
    [InlineData("83JH", LegionSeries.LOQ)]
    [InlineData("83RV", LegionSeries.Legion_Pro_5)]
    [InlineData("83RW", LegionSeries.Legion_5)]
    [InlineData("83AG", LegionSeries.Legion_9)]
    public void ResolveSeries_WhenVerifiedMachineTypeMatches_ShouldUseItsCatalogSeries(string machineType, LegionSeries expected)
    {
        ResolveSeries(machineType).Should().Be(expected);
    }

    [Theory]
    [InlineData("83JGCTO1WW", LegionSeries.LOQ)]
    [InlineData("83JHCTO1WW", LegionSeries.LOQ)]
    [InlineData("83RVCTO1WW", LegionSeries.Legion_Pro_5)]
    [InlineData("83RWCTO1WW", LegionSeries.Legion_5)]
    [InlineData(" 83agcto1ww ", LegionSeries.Legion_9)]
    [InlineData("LENOVO_MT_83DF_BU_idea_FM_Legion Y9000P IRX9", LegionSeries.Legion_Pro_5)]
    public void ResolveSeries_WhenMachineTypeContainsCtoOrSku_ShouldUseItsCatalogSeries(string machineType, LegionSeries expected)
    {
        ResolveSeries(machineType).Should().Be(expected);
    }

    [Theory]
    [InlineData("NOT83AG")]
    [InlineData("83")]
    [InlineData("")]
    public void ResolveSeries_WhenMachineTypeIsNotRecognizable_ShouldKeepUnknownSeries(string machineType)
    {
        ResolveSeries(machineType).Should().Be(LegionSeries.Unknown);
    }

    [Fact]
    public async Task ResolveSeries_WhenBothMapAndCatalogIdentifyMachineType_ShouldAgreeWithCatalog()
    {
        var mapField = typeof(Compatibility).GetField("MachineTypeMap", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The device series machine-type map was not found.");
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, LegionSeries>>(mapField.GetValue(null));
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var checkedTypes = new List<string>();

        foreach (var pack in catalog.DevicePacks)
        {
            if (!CatalogSeries.TryGetValue(pack.Id, out var expected)) continue;
            foreach (var machineType in pack.MachineTypes)
            {
                if (!map.ContainsKey(machineType)) continue;
                ResolveSeries(machineType).Should().Be(expected, "machine type {0} belongs to catalog pack {1}", machineType, pack.Id);
                checkedTypes.Add(machineType);
            }
        }

        checkedTypes.Should().NotBeEmpty();
    }

    private static LegionSeries ResolveSeries(string machineType) =>
        Assert.IsType<LegionSeries>(SeriesResolver.Invoke(null, ["Unrecognized model", machineType]));
}
