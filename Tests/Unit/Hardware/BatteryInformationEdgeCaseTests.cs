using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class BatteryInformationEdgeCaseTests
{
    #region BatteryInformation.Empty Tests

    [Fact]
    public void Empty_ShouldHaveDefaultValues()
    {
        var b = BatteryInformation.Empty;
        b.IsCharging.Should().BeFalse();
        b.BatteryPercentage.Should().Be(0);
        b.BatteryLifeRemaining.Should().Be(0);
        b.DischargeRate.Should().Be(0);
        b.DesignCapacity.Should().Be(0);
        b.FullChargeCapacity.Should().Be(0);
        b.CycleCount.Should().Be(0);
        b.IsLowBattery.Should().BeFalse();
        b.BatteryTemperatureC.Should().BeNull();
        b.ManufactureDate.Should().BeNull();
        b.FirstUseDate.Should().BeNull();
        b.ModelName.Should().BeNull();
        b.AvgTemperatureC.Should().BeNull();
    }

    #endregion

    #region BatteryHealth Edge Cases

    [Fact]
    public void BatteryHealth_WhenDesignCapacityNegative_ShouldReturnZero()
    {
        var b = new BatteryInformation(false, 100, 0, 0, 0, 0, 0, 0, -500, 400, 0, false, null, null, null, null);
        b.BatteryHealth.Should().Be(0.0);
    }

    [Fact]
    public void BatteryHealth_WhenFullChargeZero_ShouldReturnZero()
    {
        var b = new BatteryInformation(false, 100, 0, 0, 0, 0, 0, 0, 5000, 0, 0, false, null, null, null, null);
        b.BatteryHealth.Should().Be(0.0);
    }

    [Fact]
    public void BatteryHealth_WhenFullChargeExceedsDesign_ShouldBeAbove100()
    {
        var b = new BatteryInformation(false, 100, 0, 0, 0, 0, 0, 0, 4000, 5000, 0, false, null, null, null, null);
        b.BatteryHealth.Should().Be(125.0);
    }

    [Fact]
    public void BatteryHealth_ShouldRoundToTwoDecimals_WhenNonTerminating()
    {
        var b = new BatteryInformation(false, 100, 0, 0, 0, 0, 0, 0, 3000, 2000, 0, false, null, null, null, null);
        b.BatteryHealth.Should().Be(66.67);
    }

    #endregion

    #region WithAvgTemp Edge Cases

    [Fact]
    public void WithAvgTemp_ShouldChainMultipleTimes()
    {
        var b = BatteryInformation.Empty.WithAvgTemp(30.0).WithAvgTemp(40.0).WithAvgTemp(50.0);
        b.AvgTemperatureC.Should().Be(50.0);
    }

    [Fact]
    public void WithAvgTemp_ShouldPreserveAllBatteryFields()
    {
        var original = new BatteryInformation(
            true, 85, 3600, 7200, -1500, -2000, -1000, 85,
            6000, 5100, 500, false, 35.0,
            new DateTime(2023, 6, 15), new DateTime(2023, 7, 1), "TestModel");
        var updated = original.WithAvgTemp(42.0);

        updated.IsCharging.Should().BeTrue();
        updated.BatteryPercentage.Should().Be(85);
        updated.BatteryLifeRemaining.Should().Be(3600);
        updated.FullBatteryLifeRemaining.Should().Be(7200);
        updated.DischargeRate.Should().Be(-1500);
        updated.MinDischargeRate.Should().Be(-2000);
        updated.MaxDischargeRate.Should().Be(-1000);
        updated.EstimateChargeRemaining.Should().Be(85);
        updated.DesignCapacity.Should().Be(6000);
        updated.FullChargeCapacity.Should().Be(5100);
        updated.CycleCount.Should().Be(500);
        updated.IsLowBattery.Should().BeFalse();
        updated.BatteryTemperatureC.Should().Be(35.0);
        updated.ManufactureDate.Should().Be(new DateTime(2023, 6, 15));
        updated.FirstUseDate.Should().Be(new DateTime(2023, 7, 1));
        updated.ModelName.Should().Be("TestModel");
        updated.AvgTemperatureC.Should().Be(42.0);
    }

    [Fact]
    public void WithAvgTemp_NegativeTemperature_ShouldAccept()
    {
        var b = BatteryInformation.Empty.WithAvgTemp(-10.0);
        b.AvgTemperatureC.Should().Be(-10.0);
    }

    [Fact]
    public void WithAvgTemp_ZeroTemperature_ShouldAccept()
    {
        var b = BatteryInformation.Empty.WithAvgTemp(0.0);
        b.AvgTemperatureC.Should().Be(0.0);
    }

    #endregion
}
