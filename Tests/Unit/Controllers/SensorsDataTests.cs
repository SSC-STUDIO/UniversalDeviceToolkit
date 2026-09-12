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
public class SensorsDataTests : UnitTestBase
{
    [Fact]
    public void SensorsData_Empty_ShouldHaveEmptyComponents()
    {
        var empty = SensorsData.Empty;

        empty.CPU.Should().Be(SensorData.Empty);
        empty.GPU.Should().Be(SensorData.Empty);
    }

    [Fact]
    public void SensorsData_WithValidData_ShouldSetPropertiesCorrectly()
    {
        var cpuData = new SensorData(
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

        var gpuData = new SensorData(
            utilization: 80,
            maxUtilization: 100,
            coreClock: 1500,
            maxCoreClock: 2000,
            memoryClock: 6000,
            maxMemoryClock: 8000,
            temperature: 70,
            maxTemperature: 95,
            wattage: 100,
            voltage: 1.0,
            fanSpeed: 2500,
            maxFanSpeed: 5000);

        var sensorsData = new SensorsData(cpuData, gpuData);

        sensorsData.CPU.Should().Be(cpuData);
        sensorsData.GPU.Should().Be(gpuData);
    }

    [Fact]
    public void SensorsData_ToString_ShouldContainCPUAndGPU()
    {
        var data = new SensorsData(SensorData.Empty, SensorData.Empty);
        var str = data.ToString();

        str.Should().Contain("CPU");
        str.Should().Contain("GPU");
    }

    [Fact]
    public void Empty_ShouldContainTwoEmptySensorData()
    {
        var empty = SensorsData.Empty;
        empty.CPU.Should().Be(SensorData.Empty);
        empty.GPU.Should().Be(SensorData.Empty);
    }

    [Fact]
    public void ToString_ShouldContainCPUAndGPU()
    {
        var data = new SensorsData(SensorData.Empty, SensorData.Empty);
        var s = data.ToString();
        s.Should().Contain("CPU");
        s.Should().Contain("GPU");
    }
}
