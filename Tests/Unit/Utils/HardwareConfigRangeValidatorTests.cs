using System;
using System.Collections.ObjectModel;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public sealed class HardwareConfigRangeValidatorTests
{
    [Fact]
    public void IsStepperInDeviceRange_Null_IsValid()
    {
        HardwareConfigRangeValidator.IsStepperInDeviceRange(null).Should().BeTrue();
    }

    [Fact]
    public void IsStepperInDeviceRange_WithinMinMax_IsValid()
    {
        var stepper = new StepperValue(50, 0, 100, 1, [], 50);
        HardwareConfigRangeValidator.IsStepperInDeviceRange(stepper).Should().BeTrue();
    }

    [Fact]
    public void IsStepperInDeviceRange_OutsideMinMax_IsInvalid()
    {
        var stepper = new StepperValue(200, 0, 100, 1, [], 50);
        HardwareConfigRangeValidator.IsStepperInDeviceRange(stepper).Should().BeFalse();
    }

    [Fact]
    public void IsStepperInDeviceRange_DiscreteSteps_MustContainValue()
    {
        var ok = new StepperValue(20, 0, 0, 0, [10, 20, 30], 20);
        var bad = new StepperValue(25, 0, 0, 0, [10, 20, 30], 20);
        HardwareConfigRangeValidator.IsStepperInDeviceRange(ok).Should().BeTrue();
        HardwareConfigRangeValidator.IsStepperInDeviceRange(bad).Should().BeFalse();
    }

    [Fact]
    public void IsGpuOverclockInRange_WithinLimits_IsValid()
    {
        var info = new GPUOverclockInfo(100, 200);
        HardwareConfigRangeValidator.IsGpuOverclockInRange(info).Should().BeTrue();
    }

    [Fact]
    public void IsGpuOverclockInRange_ExceedsCore_IsInvalid()
    {
        var info = new GPUOverclockInfo(9999, 0);
        HardwareConfigRangeValidator.IsGpuOverclockInRange(info).Should().BeFalse();
    }

    [Fact]
    public void IsGpuOverclockInRange_Negative_IsInvalid()
    {
        var info = new GPUOverclockInfo(-10, 0);
        HardwareConfigRangeValidator.IsGpuOverclockInRange(info).Should().BeFalse();
    }

    [Fact]
    public void IsFanCurveEntryInRange_DefaultCurve_IsValid()
    {
        var entry = new FanCurveEntry();
        HardwareConfigRangeValidator.IsFanCurveEntryInRange(entry).Should().BeTrue();
    }

    [Fact]
    public void IsFanCurveEntryInRange_TempOutOfRange_IsInvalid()
    {
        var entry = new FanCurveEntry();
        entry.CurveNodes.Clear();
        entry.CurveNodes.Add(new CurveNode { Temperature = 200, TargetPercent = 50 });
        HardwareConfigRangeValidator.IsFanCurveEntryInRange(entry).Should().BeFalse();
    }

    [Fact]
    public void IsFanCurveEntryInRange_PercentOutOfRange_IsInvalid()
    {
        var entry = new FanCurveEntry();
        entry.CurveNodes.Clear();
        entry.CurveNodes.Add(new CurveNode { Temperature = 60, TargetPercent = 150 });
        HardwareConfigRangeValidator.IsFanCurveEntryInRange(entry).Should().BeFalse();
    }

    [Fact]
    public void IsGodModePresetInRange_IllegalStepper_IsInvalid()
    {
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            Name = "test",
            CPULongTermPowerLimit = new StepperValue(999, 10, 100, 1, [], 55),
        };

        HardwareConfigRangeValidator.IsGodModePresetInRange(preset).Should().BeFalse();
    }

    [Fact]
    public void IsGodModePresetInRange_Valid_IsValid()
    {
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            Name = "test",
            CPULongTermPowerLimit = new StepperValue(55, 10, 100, 1, [], 55),
            MinValueOffset = 0,
            MaxValueOffset = 0,
        };

        HardwareConfigRangeValidator.IsGodModePresetInRange(preset).Should().BeTrue();
    }

    [Fact]
    public void AreFanCurveEntriesInRange_NullOrEmpty_IsValid()
    {
        HardwareConfigRangeValidator.AreFanCurveEntriesInRange(null).Should().BeTrue();
        HardwareConfigRangeValidator.AreFanCurveEntriesInRange(Array.Empty<FanCurveEntry>()).Should().BeTrue();
    }
}
