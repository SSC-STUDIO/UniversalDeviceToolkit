using System;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;
using Xunit;
using Moq;

namespace UniversalDeviceToolkit.Tests.Features;

[Trait("Category", TestCategories.Unit)]
public class IFeatureTests : UnitTestBase
{
    [Fact]
    public void IFeature_ShouldHaveCorrectMethods()
    {
        var methodNames = new[]
        {
            nameof(IFeature<BatteryState>.IsSupportedAsync),
            nameof(IFeature<BatteryState>.GetAllStatesAsync),
            nameof(IFeature<BatteryState>.GetStateAsync),
            nameof(IFeature<BatteryState>.SetStateAsync)
        };

        foreach (var methodName in methodNames)
        {
            typeof(IFeature<BatteryState>).GetMethod(methodName).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task IFeature_MockImplementation_ShouldWorkCorrectly()
    {
        var mockFeature = new Mock<IFeature<BatteryState>>();
        
        mockFeature
            .Setup(f => f.IsSupportedAsync())
            .ReturnsAsync(true);
        
        mockFeature
            .Setup(f => f.GetAllStatesAsync())
            .ReturnsAsync(new[] { BatteryState.Conservation, BatteryState.Normal, BatteryState.RapidCharge });
        
        mockFeature
            .Setup(f => f.GetStateAsync())
            .ReturnsAsync(BatteryState.Normal);

        var isSupported = await mockFeature.Object.IsSupportedAsync();
        var states = await mockFeature.Object.GetAllStatesAsync();
        var currentState = await mockFeature.Object.GetStateAsync();

        isSupported.Should().BeTrue();
        states.Should().HaveCount(3);
        currentState.Should().Be(BatteryState.Normal);
    }
}


[Trait("Category", TestCategories.Unit)]
public class BatteryStateTests : UnitTestBase
{
    [Fact]
    public void BatteryState_ShouldHaveThreeStates()
    {
        var states = Enum.GetValues<BatteryState>();
        
        states.Should().HaveCount(3);
        states.Should().Contain(BatteryState.Conservation);
        states.Should().Contain(BatteryState.Normal);
        states.Should().Contain(BatteryState.RapidCharge);
    }

    [Fact]
    public void BatteryState_Values_ShouldBeCorrect()
    {
        ((int)BatteryState.Conservation).Should().Be(0);
        ((int)BatteryState.Normal).Should().Be(1);
        ((int)BatteryState.RapidCharge).Should().Be(2);
    }
}


[Trait("Category", TestCategories.Unit)]
public class PowerModeStateTests : UnitTestBase
{
    [Fact]
    public void PowerModeState_ShouldIncludeExtremeAndGodMode()
    {
        var states = Enum.GetValues<PowerModeState>();
        
        states.Should().HaveCount(5);
        states.Should().Contain(PowerModeState.Quiet);
        states.Should().Contain(PowerModeState.Balance);
        states.Should().Contain(PowerModeState.Performance);
        states.Should().Contain(PowerModeState.Extreme);
        states.Should().Contain(PowerModeState.GodMode);
    }

    [Fact]
    public void PowerModeState_GodMode_ShouldHaveSpecialValue()
    {
        ((int)PowerModeState.GodMode).Should().Be(254);
    }

    [Fact]
    public void PowerModeState_StandardModes_ShouldHaveSequentialValues()
    {
        ((int)PowerModeState.Quiet).Should().Be(0);
        ((int)PowerModeState.Balance).Should().Be(1);
        ((int)PowerModeState.Performance).Should().Be(2);
    }
}


[Trait("Category", TestCategories.Unit)]
public class HybridModeStateTests : UnitTestBase
{
    [Fact]
    public void HybridModeState_ShouldHaveFourStates()
    {
        var states = Enum.GetValues<HybridModeState>();
        
        states.Should().HaveCount(4);
        states.Should().Contain(HybridModeState.On);
        states.Should().Contain(HybridModeState.OnIGPUOnly);
        states.Should().Contain(HybridModeState.OnAuto);
        states.Should().Contain(HybridModeState.Off);
    }
}


[Trait("Category", TestCategories.Unit)]
public class GPUStateTests : UnitTestBase
{
    [Fact]
    public void GPUState_ShouldHaveSixStates()
    {
        var states = Enum.GetValues<GPUState>();
        
        states.Should().HaveCount(6);
        states.Should().Contain(GPUState.Unknown);
        states.Should().Contain(GPUState.NvidiaGpuNotFound);
        states.Should().Contain(GPUState.MonitorConnected);
        states.Should().Contain(GPUState.Active);
        states.Should().Contain(GPUState.Inactive);
        states.Should().Contain(GPUState.PoweredOff);
    }

    [Fact]
    public void GPUState_Unknown_ShouldBeFirst()
    {
        ((int)GPUState.Unknown).Should().Be(0);
    }
}


[Trait("Category", TestCategories.Unit)]
public class FanTableTypeTests : UnitTestBase
{
    [Fact]
    public void FanTableType_ShouldIncludePchTable()
    {
        var types = Enum.GetValues<FanTableType>();
        
        types.Should().HaveCount(6);
        types.Should().Contain(FanTableType.Unknown);
        types.Should().Contain(FanTableType.CPU);
        types.Should().Contain(FanTableType.CPUSensor);
        types.Should().Contain(FanTableType.GPU);
        types.Should().Contain(FanTableType.GPU2);
        types.Should().Contain(FanTableType.PCH);
    }
}
