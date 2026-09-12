using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

/// <summary>
/// Numeric contracts for firmware/EC-facing mode values.
/// These encodings are part of the on-wire/BIOS interface and must not drift silently.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class FirmwareModeValueContractTests
{
    [Theory]
    [InlineData(PowerModeState.Quiet, 0)]
    [InlineData(PowerModeState.Balance, 1)]
    [InlineData(PowerModeState.Performance, 2)]
    [InlineData(PowerModeState.Extreme, 223)]
    [InlineData(PowerModeState.GodMode, 254)]
    public void PowerModeState_ShouldKeepFirmwareEncoding(PowerModeState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(ThermalModeState.Unknown, 0)]
    [InlineData(ThermalModeState.Quiet, 1)]
    [InlineData(ThermalModeState.Balance, 2)]
    [InlineData(ThermalModeState.Performance, 3)]
    [InlineData(ThermalModeState.Extreme, 224)]
    [InlineData(ThermalModeState.GodMode, 255)]
    public void ThermalModeState_ShouldKeepFirmwareEncoding(ThermalModeState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(CapabilityID.IGPUMode, 0x00010000u)]
    [InlineData(CapabilityID.FlipToStart, 0x00030000u)]
    [InlineData(CapabilityID.NvidiaGPUDynamicDisplaySwitching, 0x00040000u)]
    [InlineData(CapabilityID.AMDSmartShiftMode, 0x00050001u)]
    [InlineData(CapabilityID.OverDrive, 0x001A0000u)]
    [InlineData(CapabilityID.AIChip, 0x000E0000u)]
    [InlineData(CapabilityID.CPUShortTermPowerLimit, 0x0101FF00u)]
    [InlineData(CapabilityID.CPULongTermPowerLimit, 0x0102FF00u)]
    public void CapabilityID_ShouldKeepWmiFeatureIds(CapabilityID id, uint expectedValue)
    {
        ((uint)id).Should().Be(expectedValue);
    }
}
