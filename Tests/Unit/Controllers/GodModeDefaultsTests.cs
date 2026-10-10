using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GodModeDefaultsTests
{
    [Fact]
    public void Default_ShouldHaveAllNulls()
    {
        var defaults = new GodModeDefaults();
        defaults.CPULongTermPowerLimit.Should().BeNull();
        defaults.CPUShortTermPowerLimit.Should().BeNull();
        defaults.CPUPeakPowerLimit.Should().BeNull();
        defaults.CPUCrossLoadingPowerLimit.Should().BeNull();
        defaults.CPUPL1Tau.Should().BeNull();
        defaults.APUsPPTPowerLimit.Should().BeNull();
        defaults.CPUTemperatureLimit.Should().BeNull();
        defaults.GPUPowerBoost.Should().BeNull();
        defaults.GPUConfigurableTGP.Should().BeNull();
        defaults.GPUTemperatureLimit.Should().BeNull();
        defaults.GPUToCPUDynamicBoost.Should().BeNull();
        defaults.EnableOverclocking.Should().BeNull();
        defaults.FanTable.Should().BeNull();
        defaults.FanFullSpeed.Should().BeNull();
    }

    [Fact]
    public void Init_Values_ShouldBeRetained()
    {
        ushort[] fanSpeeds = [1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000];
        var fanTable = new FanTable(fanSpeeds);

        var defaults = new GodModeDefaults
        {
            CPULongTermPowerLimit = 45,
            CPUShortTermPowerLimit = 65,
            CPUPeakPowerLimit = 115,
            CPUCrossLoadingPowerLimit = 55,
            CPUPL1Tau = 56,
            APUsPPTPowerLimit = 30,
            CPUTemperatureLimit = 95,
            GPUPowerBoost = 15,
            GPUConfigurableTGP = 120,
            GPUTemperatureLimit = 87,
            GPUToCPUDynamicBoost = 5,
            EnableOverclocking = true,
            FanFullSpeed = false,
            FanTable = fanTable
        };

        defaults.CPULongTermPowerLimit.Should().Be(45);
        defaults.CPUShortTermPowerLimit.Should().Be(65);
        defaults.CPUPeakPowerLimit.Should().Be(115);
        defaults.CPUCrossLoadingPowerLimit.Should().Be(55);
        defaults.CPUPL1Tau.Should().Be(56);
        defaults.APUsPPTPowerLimit.Should().Be(30);
        defaults.CPUTemperatureLimit.Should().Be(95);
        defaults.GPUPowerBoost.Should().Be(15);
        defaults.GPUConfigurableTGP.Should().Be(120);
        defaults.GPUTemperatureLimit.Should().Be(87);
        defaults.GPUToCPUDynamicBoost.Should().Be(5);
        defaults.EnableOverclocking.Should().BeTrue();
        defaults.FanFullSpeed.Should().BeFalse();
        defaults.FanTable.Should().NotBeNull();
    }

    [Fact]
    public void ToString_ShouldContainAllPropertyNames()
    {
        var defaults = new GodModeDefaults { CPULongTermPowerLimit = 10 };
        var str = defaults.ToString();
        str.Should().Contain("CPULongTermPowerLimit");
        str.Should().Contain("CPUShortTermPowerLimit");
        str.Should().Contain("GPUPowerBoost");
        str.Should().Contain("FanFullSpeed");
    }
}

[Trait("Category", TestCategories.Unit)]
public class MachineInformationPropertyDataTests
{
    [Fact]
    public void SupportsGodMode_V1_ShouldBeTrue()
    {
        var pd = new MachineInformation.PropertyData { SupportsGodModeV1 = true };
        pd.SupportsGodMode.Should().BeTrue();
    }

    [Fact]
    public void SupportsGodMode_V2_ShouldBeTrue()
    {
        var pd = new MachineInformation.PropertyData { SupportsGodModeV2 = true };
        pd.SupportsGodMode.Should().BeTrue();
    }

    [Fact]
    public void SupportsGodMode_V3_ShouldBeTrue()
    {
        var pd = new MachineInformation.PropertyData { SupportsGodModeV3 = true };
        pd.SupportsGodMode.Should().BeTrue();
    }

    [Fact]
    public void SupportsGodMode_V4_ShouldBeTrue()
    {
        var pd = new MachineInformation.PropertyData { SupportsGodModeV4 = true };
        pd.SupportsGodMode.Should().BeTrue();
    }

    [Fact]
    public void SupportsGodMode_None_ShouldBeFalse()
    {
        var pd = new MachineInformation.PropertyData();
        pd.SupportsGodMode.Should().BeFalse();
    }

    [Fact]
    public void SupportsBootLogoChange_ShouldMapToAlias()
    {
        var pd = new MachineInformation.PropertyData { SupportBootLogoChange = true };
        pd.SupportsBootLogoChange.Should().BeTrue();
    }

    [Fact]
    public void AllBoolProperties_Default_ShouldBeFalse()
    {
        var pd = new MachineInformation.PropertyData();
        pd.SupportsExtremeMode.Should().BeFalse();
        pd.SupportsGSync.Should().BeFalse();
        pd.SupportsIGPUMode.Should().BeFalse();
        pd.SupportsAIMode.Should().BeFalse();
        pd.HasQuietToPerformanceModeSwitchingBug.Should().BeFalse();
        pd.HasGodModeToOtherModeSwitchingBug.Should().BeFalse();
        pd.HasReapplyParameterIssue.Should().BeFalse();
        pd.HasSpectrumProfileSwitchingBug.Should().BeFalse();
        pd.IsExcludedFromLenovoLighting.Should().BeFalse();
        pd.IsAmdDevice.Should().BeFalse();
        pd.IsChineseModel.Should().BeFalse();
    }
}