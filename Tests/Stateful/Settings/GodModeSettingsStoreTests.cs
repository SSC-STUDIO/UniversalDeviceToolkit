using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class GodModeSettingsStoreTests
{
    #region Preset Defaults Tests

    [Fact]
    public void Preset_Defaults_ShouldHaveNullValues()
    {
        var preset = new GodModeSettings.GodModeSettingsStore.Preset();
        preset.Name.Should().BeEmpty();
        preset.PowerPlanGuid.Should().BeNull();
        preset.PowerMode.Should().BeNull();
        preset.SourcePowerMode.Should().BeNull();
        preset.CPULongTermPowerLimit.Should().BeNull();
        preset.CPUShortTermPowerLimit.Should().BeNull();
        preset.CPUPeakPowerLimit.Should().BeNull();
        preset.CPUCrossLoadingPowerLimit.Should().BeNull();
        preset.CPUPL1Tau.Should().BeNull();
        preset.APUsPPTPowerLimit.Should().BeNull();
        preset.CPUTemperatureLimit.Should().BeNull();
        preset.GPUPowerBoost.Should().BeNull();
    }

    [Fact]
    public void Preset_WithStepperValues_ShouldRetainValues()
    {
        var cpuLimit = new StepperValue(45, 0, 125, 1, [], 45);
        var gpuBoost = new StepperValue(15, 0, 25, 1, [], 15);
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            Name = "Test Preset",
            CPULongTermPowerLimit = cpuLimit,
            GPUPowerBoost = gpuBoost
        };

        preset.Name.Should().Be("Test Preset");
        preset.CPULongTermPowerLimit.Should().NotBeNull();
        preset.CPULongTermPowerLimit!.Value.Value.Should().Be(45);
        preset.GPUPowerBoost.Should().NotBeNull();
        preset.GPUPowerBoost!.Value.Value.Should().Be(15);
    }

    [Fact]
    public void Preset_PowerPlanGuid_ShouldAcceptGuid()
    {
        var guid = Guid.NewGuid();
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            PowerPlanGuid = guid
        };
        preset.PowerPlanGuid.Should().Be(guid);
    }

    #endregion

    #region GodModeSettingsStore Defaults Tests

    [Fact]
    public void GodModeSettingsStore_Defaults_ShouldHaveEmptyPresets()
    {
        var store = new GodModeSettings.GodModeSettingsStore();
        store.Presets.Should().NotBeNull();
        store.Presets.Should().BeEmpty();
    }

    #endregion
}


