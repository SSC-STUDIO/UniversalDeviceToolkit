using System;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Features.Hybrid;

[Trait("Category", TestCategories.Unit)]
public class HybridModeStateLogicTests
{
    #region HybridModeState Tests

    [Fact]
    public void HybridModeState_ShouldHaveFourStates()
    {
        // Arrange & Act
        var states = Enum.GetValues<HybridModeState>();

        // Assert
        states.Should().HaveCount(4);
        states.Should().Contain(HybridModeState.On);
        states.Should().Contain(HybridModeState.OnIGPUOnly);
        states.Should().Contain(HybridModeState.OnAuto);
        states.Should().Contain(HybridModeState.Off);
    }

    [Fact]
    public void HybridModeState_Values_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        ((int)HybridModeState.On).Should().Be(0);
        ((int)HybridModeState.OnIGPUOnly).Should().Be(1);
        ((int)HybridModeState.OnAuto).Should().Be(2);
        ((int)HybridModeState.Off).Should().Be(3);
    }

    #endregion

    #region GSyncState Tests

    [Fact]
    public void GSyncState_ShouldHaveTwoStates()
    {
        // Arrange & Act
        var states = Enum.GetValues<GSyncState>();

        // Assert
        states.Should().HaveCount(2);
        states.Should().Contain(GSyncState.On);
        states.Should().Contain(GSyncState.Off);
    }

    [Fact]
    public void GSyncState_Values_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        ((int)GSyncState.Off).Should().Be(0);
        ((int)GSyncState.On).Should().Be(1);
    }

    #endregion

    #region IGPUModeState Tests

    [Fact]
    public void IGPUModeState_ShouldHaveThreeStates()
    {
        // Arrange & Act
        var states = Enum.GetValues<IGPUModeState>();

        // Assert
        states.Should().HaveCount(3);
        states.Should().Contain(IGPUModeState.Default);
        states.Should().Contain(IGPUModeState.IGPUOnly);
        states.Should().Contain(IGPUModeState.Auto);
    }

    [Fact]
    public void IGPUModeState_Values_ShouldBeCorrect()
    {
        // Arrange & Act & Assert
        ((int)IGPUModeState.Default).Should().Be(0);
        ((int)IGPUModeState.IGPUOnly).Should().Be(1);
        ((int)IGPUModeState.Auto).Should().Be(2);
    }

    #endregion

    #region State Packing/Unpacking Tests

    [Fact]
    public void HybridModeState_Pack_ShouldWorkCorrectly()
    {
        // Test GSync On + any IGPUMode = HybridMode Off
        var state1 = Pack(GSyncState.On, IGPUModeState.Default);
        state1.Should().Be(HybridModeState.Off);

        var state2 = Pack(GSyncState.On, IGPUModeState.IGPUOnly);
        state2.Should().Be(HybridModeState.Off);

        // Test GSync Off + IGPUMode Default = HybridMode On
        var state3 = Pack(GSyncState.Off, IGPUModeState.Default);
        state3.Should().Be(HybridModeState.On);

        // Test GSync Off + IGPUMode IGPUOnly = HybridMode OnIGPUOnly
        var state4 = Pack(GSyncState.Off, IGPUModeState.IGPUOnly);
        state4.Should().Be(HybridModeState.OnIGPUOnly);

        // Test GSync Off + IGPUMode Auto = HybridMode OnAuto
        var state5 = Pack(GSyncState.Off, IGPUModeState.Auto);
        state5.Should().Be(HybridModeState.OnAuto);
    }

    [Fact]
    public void HybridModeState_Unpack_ShouldWorkCorrectly()
    {
        // Test HybridMode Off -> (GSync On, IGPUMode Default)
        var (gSync0, igpu0) = Unpack(HybridModeState.Off);
        gSync0.Should().Be(GSyncState.On);
        igpu0.Should().Be(IGPUModeState.Default);

        // Test HybridMode On -> (GSync Off, IGPUMode Default)
        var (gSync1, igpu1) = Unpack(HybridModeState.On);
        gSync1.Should().Be(GSyncState.Off);
        igpu1.Should().Be(IGPUModeState.Default);

        // Test HybridMode OnIGPUOnly -> (GSync Off, IGPUMode IGPUOnly)
        var (gSync2, igpu2) = Unpack(HybridModeState.OnIGPUOnly);
        gSync2.Should().Be(GSyncState.Off);
        igpu2.Should().Be(IGPUModeState.IGPUOnly);

        // Test HybridMode OnAuto -> (GSync Off, IGPUMode Auto)
        var (gSync3, igpu3) = Unpack(HybridModeState.OnAuto);
        gSync3.Should().Be(GSyncState.Off);
        igpu3.Should().Be(IGPUModeState.Auto);
    }

    [Fact]
    public void HybridModeState_PackUnpack_ShouldBeSymmetric()
    {
        // Test that Pack and Unpack are inverse operations
        foreach (var hybridState in Enum.GetValues<HybridModeState>())
        {
            var (gSync, igpu) = Unpack(hybridState);
            var packedState = Pack(gSync, igpu);
            packedState.Should().Be(hybridState);
        }
    }

    #endregion

    #region Helper Methods

    // Replicate the Pack logic from HybridModeFeature
    private static HybridModeState Pack(GSyncState gSync, IGPUModeState igpuMode)
    {
        return (gSync, igpuMode) switch
        {
            (GSyncState.Off, IGPUModeState.Default) => HybridModeState.On,
            (GSyncState.Off, IGPUModeState.IGPUOnly) => HybridModeState.OnIGPUOnly,
            (GSyncState.Off, IGPUModeState.Auto) => HybridModeState.OnAuto,
            (GSyncState.On, _) => HybridModeState.Off, // Any IGPUMode with GSync On = Off
            _ => throw new InvalidOperationException($"Invalid combination: {gSync}, {igpuMode}")
        };
    }

    // Replicate the Unpack logic from HybridModeFeature
    private static (GSyncState, IGPUModeState) Unpack(HybridModeState state)
    {
        return state switch
        {
            HybridModeState.On => (GSyncState.Off, IGPUModeState.Default),
            HybridModeState.OnIGPUOnly => (GSyncState.Off, IGPUModeState.IGPUOnly),
            HybridModeState.OnAuto => (GSyncState.Off, IGPUModeState.Auto),
            HybridModeState.Off => (GSyncState.On, IGPUModeState.Default),
            _ => throw new InvalidOperationException($"Invalid state: {state}")
        };
    }

    #endregion
}