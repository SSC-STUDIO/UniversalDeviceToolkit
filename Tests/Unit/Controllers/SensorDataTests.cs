using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class SensorDataTests : UnitTestBase
{
    [Fact]
    public void SensorData_Empty_ShouldHaveZeroValues()
    {
        var empty = SensorData.Empty;

        empty.Utilization.Should().Be(-1);
        empty.CoreClock.Should().Be(-1);
        empty.Temperature.Should().Be(-1);
        empty.FanSpeed.Should().Be(-1);
        empty.Wattage.Should().Be(-1);
    }

    [Fact]
    public void SensorData_WithAllParameters_ShouldSetPropertiesCorrectly()
    {
        var data = new SensorData(
            utilization: 75,
            maxUtilization: 100,
            coreClock: 3500,
            maxCoreClock: 4500,
            memoryClock: 0,
            maxMemoryClock: 0,
            temperature: 72,
            maxTemperature: 100,
            wattage: 55,
            voltage: 1.25,
            fanSpeed: 3500,
            maxFanSpeed: 5500);

        data.Utilization.Should().Be(75);
        data.MaxUtilization.Should().Be(100);
        data.CoreClock.Should().Be(3500);
        data.MaxCoreClock.Should().Be(4500);
        data.Temperature.Should().Be(72);
        data.MaxTemperature.Should().Be(100);
        data.Wattage.Should().Be(55);
        data.Voltage.Should().Be(1.25);
        data.FanSpeed.Should().Be(3500);
        data.MaxFanSpeed.Should().Be(5500);
    }

    [Fact]
    public void SensorData_ToString_ShouldContainKeyMetrics()
    {
        var data = new SensorData(
            utilization: 50,
            maxUtilization: 100,
            coreClock: 3000,
            maxCoreClock: 4000,
            memoryClock: 0,
            maxMemoryClock: 0,
            temperature: 65,
            maxTemperature: 100,
            wattage: 45,
            voltage: 1.2,
            fanSpeed: 3000,
            maxFanSpeed: 5000);

        var str = data.ToString();

        str.Should().Contain("50%");
        str.Should().Contain("3000MHz");
        str.Should().Contain("65C");
        str.Should().Contain("3000RPM");
        str.Should().Contain("45W");
    }

    [Fact]
    public void SensorData_WithExtendedParameters_ShouldSetExtendedProperties()
    {
        var data = new SensorData(
            utilization: 50,
            maxUtilization: 100,
            coreClock: 3000,
            maxCoreClock: 4000,
            memoryClock: 0,
            maxMemoryClock: 0,
            temperature: 65,
            maxTemperature: 100,
            wattage: 45,
            voltage: 1.2,
            fanSpeed: 3000,
            maxFanSpeed: 5000);

        data.Utilization.Should().Be(50);
        data.Temperature.Should().Be(65);
        data.Wattage.Should().Be(45);
        data.Voltage.Should().Be(1.2);
    }

    #region Full Constructor

    [Fact]
    public void FullConstructor_ShouldSetAllProperties()
    {
        var data = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            wattage: 65, voltage: 1.2,
            fanSpeed: 2500, maxFanSpeed: 5000);

        data.Utilization.Should().Be(50);
        data.MaxUtilization.Should().Be(100);
        data.CoreClock.Should().Be(3000);
        data.MaxCoreClock.Should().Be(5000);
        data.MemoryClock.Should().Be(1200);
        data.MaxMemoryClock.Should().Be(2000);
        data.Temperature.Should().Be(70);
        data.MaxTemperature.Should().Be(100);
        data.Wattage.Should().Be(65);
        data.Voltage.Should().Be(1.2);
        data.FanSpeed.Should().Be(2500);
        data.MaxFanSpeed.Should().Be(5000);
    }

    #endregion

    #region Short Constructor (no wattage/voltage)

    [Fact]
    public void ShortConstructor_ShouldDefaultWattageToNegativeOneAndVoltageToZero()
    {
        var data = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            fanSpeed: 2500, maxFanSpeed: 5000);

        data.Wattage.Should().Be(-1);
        data.Voltage.Should().Be(0);
    }

    #endregion

    #region Empty

    [Fact]
    public void Empty_ShouldHaveAllNegativeOneExceptVoltage()
    {
        var empty = SensorData.Empty;
        empty.Utilization.Should().Be(-1);
        empty.MaxUtilization.Should().Be(-1);
        empty.CoreClock.Should().Be(-1);
        empty.MaxCoreClock.Should().Be(-1);
        empty.MemoryClock.Should().Be(-1);
        empty.MaxMemoryClock.Should().Be(-1);
        empty.Temperature.Should().Be(-1);
        empty.MaxTemperature.Should().Be(-1);
        empty.Wattage.Should().Be(-1);
        empty.Voltage.Should().Be(0);
        empty.FanSpeed.Should().Be(-1);
        empty.MaxFanSpeed.Should().Be(-1);
    }

    #endregion

    #region WithMinMax

    [Fact]
    public void WithMinMax_ShouldPreserveOriginalFields()
    {
        var original = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            wattage: 65, voltage: 1.2,
            fanSpeed: 2500, maxFanSpeed: 5000);

        var result = original.WithMinMax(0.5, 1.5, 40, 90);

        result.Utilization.Should().Be(50);
        result.CoreClock.Should().Be(3000);
        result.Temperature.Should().Be(70);
        result.Wattage.Should().Be(65);
        result.Voltage.Should().Be(1.2);
        result.FanSpeed.Should().Be(2500);
    }

    [Fact]
    public void WithMinMax_ShouldSetMinMaxFields()
    {
        var original = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            wattage: 65, voltage: 1.2,
            fanSpeed: 2500, maxFanSpeed: 5000);

        var result = original.WithMinMax(0.5, 1.5, 40, 90);

        result.MinVoltage.Should().Be(0.5);
        result.MaxVoltage.Should().Be(1.5);
        result.MinTemperature.Should().Be(40);
        result.MaxTemperatureRecord.Should().Be(90);
    }

    [Fact]
    public void WithMinMax_DefaultValuesBeforeCall_ShouldBeZero()
    {
        var original = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            wattage: 65, voltage: 1.2,
            fanSpeed: 2500, maxFanSpeed: 5000);

        original.MinVoltage.Should().Be(0);
        original.MaxVoltage.Should().Be(0);
        original.MinTemperature.Should().Be(0);
        original.MaxTemperatureRecord.Should().Be(0);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShouldContainKeyMetrics()
    {
        var data = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            wattage: 65, voltage: 1.2,
            fanSpeed: 2500, maxFanSpeed: 5000);

        var s = data.ToString();
        s.Should().Contain("50%");
        s.Should().Contain("3000MHz");
        s.Should().Contain("70C");
        s.Should().Contain("2500RPM");
        s.Should().Contain("65W");
        s.Should().Contain("1.2V");
    }

    #endregion
}
