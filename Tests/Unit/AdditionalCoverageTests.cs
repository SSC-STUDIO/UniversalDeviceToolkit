using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests;

[Trait("Category", TestCategories.Unit)]
public class AdditionalCoverageTests
{
    #region ProcessInfo Additional Tests

    [Fact]
    public void ProcessInfo_GetHashCode_NullPath_ShouldNotThrow()
    {
        var info = new ProcessInfo("test", null);
        var act = () => info.GetHashCode();
        act.Should().NotThrow();
    }

    [Fact]
    public void ProcessInfo_GetHashCode_SameNameNullPath_ShouldMatch()
    {
        var a = new ProcessInfo("test", null);
        var b = new ProcessInfo("test", null);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ProcessInfo_Equals_BoxedNonProcessInfo_ShouldReturnFalse()
    {
        var info = new ProcessInfo("test", @"C:\test.exe");
        info.Equals("not a process info").Should().BeFalse();
    }

    [Fact]
    public void ProcessInfo_Equals_BoxedSameProcessInfo_ShouldReturnTrue()
    {
        var a = new ProcessInfo("test", @"C:\test.exe");
        object b = new ProcessInfo("test", @"C:\test.exe");
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void ProcessInfo_FromPath_MultipleDots_ShouldExtractCorrectName()
    {
        var info = ProcessInfo.FromPath(@"C:\my.app.name.exe");
        info.Name.Should().Be("my.app.name");
    }

    [Fact]
    public void ProcessInfo_FromPath_NoExtension_ShouldExtractFullName()
    {
        var info = ProcessInfo.FromPath(@"C:\folder\myapp");
        info.Name.Should().Be("myapp");
    }

    [Fact]
    public void ProcessInfo_Equality_CaseInsensitiveName_ShouldNotBeEqual()
    {
        var a = new ProcessInfo("Test", @"C:\test.exe");
        var b = new ProcessInfo("test", @"C:\test.exe");
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void ProcessInfo_CompareTo_BothNullPaths_ShouldCompareByName()
    {
        var a = new ProcessInfo("aaa", null);
        var b = new ProcessInfo("zzz", null);
        a.CompareTo(b).Should().BeNegative();
    }

    #endregion

    #region Device Struct Equality Tests

    [Fact]
    public void Device_Equality_SameValues_ShouldBeEqual()
    {
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var a = new Device("N", "D", "B", "I", guid, "C", false, false);
        var b = new Device("N", "D", "B", "I", guid, "C", false, false);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Device_Equality_DifferentName_ShouldNotBeEqual()
    {
        var guid = Guid.NewGuid();
        var a = new Device("A", "D", "B", "I", guid, "C", false, false);
        var b = new Device("Z", "D", "B", "I", guid, "C", false, false);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Device_Equality_DifferentGuid_ShouldNotBeEqual()
    {
        var a = new Device("N", "D", "B", "I", Guid.NewGuid(), "C", false, false);
        var b = new Device("N", "D", "B", "I", Guid.NewGuid(), "C", false, false);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Device_Equality_BoxedNonDevice_ShouldReturnFalse()
    {
        var device = new Device("N", "D", "B", "I", Guid.NewGuid(), "C", false, false);
        device.Equals("not a device").Should().BeFalse();
    }

    [Fact]
    public void Device_GetHashCode_SameValues_ShouldMatch()
    {
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var a = new Device("N", "D", "B", "I", guid, "C", false, false);
        var b = new Device("N", "D", "B", "I", guid, "C", false, false);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion

    #region BatteryInformation Additional Fields

    [Fact]
    public void BatteryInformation_ChargeRates_ShouldBeSettable()
    {
        var info = new BatteryInformation(
            isCharging: true, batteryPercentage: 75,
            batteryLifeRemaining: 3600, fullBatteryLifeRemaining: 7200,
            dischargeRate: -1500, minDischargeRate: -2000, maxDischargeRate: -1000,
            estimateChargeRemaining: 75,
            designCapacity: 6000, fullChargeCapacity: 5100, cycleCount: 500,
            isLowBattery: false, batteryTemperatureC: 35.0,
            manufactureDate: new DateTime(2023, 6, 15),
            firstUseDate: new DateTime(2023, 7, 1), modelName: "TestModel");

        info.DischargeRate.Should().Be(-1500);
        info.MinDischargeRate.Should().Be(-2000);
        info.MaxDischargeRate.Should().Be(-1000);
        info.BatteryLifeRemaining.Should().Be(3600);
        info.FullBatteryLifeRemaining.Should().Be(7200);
    }

    [Fact]
    public void BatteryInformation_IsLowBattery_True_ShouldReflect()
    {
        var info = new BatteryInformation(
            false, 5, 600, 1200, -100, -150, -50, 5,
            5000, 250, 0, true, null, null, null, null);
        info.IsLowBattery.Should().BeTrue();
        info.BatteryPercentage.Should().Be(5);
    }

    [Fact]
    public void BatteryInformation_CycleCount_ShouldBeSettable()
    {
        var info = new BatteryInformation(
            false, 100, 0, 0, 0, 0, 0, 100,
            5000, 4800, 999, false, null, null, null, null);
        info.CycleCount.Should().Be(999);
    }

    [Fact]
    public void BatteryInformation_ManufactureDate_ShouldBeSettable()
    {
        var date = new DateTime(2024, 1, 15);
        var info = new BatteryInformation(
            false, 100, 0, 0, 0, 0, 0, 100,
            5000, 4800, 100, false, null, date, null, null);
        info.ManufactureDate.Should().Be(date);
    }

    [Fact]
    public void BatteryInformation_ModelName_ShouldBeSettable()
    {
        var info = new BatteryInformation(
            false, 100, 0, 0, 0, 0, 0, 100,
            5000, 4800, 100, false, null, null, null, "L24M4PF4");
        info.ModelName.Should().Be("L24M4PF4");
    }

    #endregion

    #region PowerModeState Enum Tests

    [Theory]
    [InlineData(PowerModeState.Quiet)]
    [InlineData(PowerModeState.Balance)]
    [InlineData(PowerModeState.Performance)]
    [InlineData(PowerModeState.Extreme)]
    [InlineData(PowerModeState.GodMode)]
    public void PowerModeState_ShouldBeDefined(PowerModeState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void PowerModeState_Extreme_ShouldHaveExpectedValue()
    {
        ((int)PowerModeState.Extreme).Should().Be(223);
    }

    [Fact]
    public void PowerModeState_GodMode_ShouldHaveExpectedValue()
    {
        ((int)PowerModeState.GodMode).Should().Be(254);
    }

    #endregion

    #region GPUState Enum Tests

    [Theory]
    [InlineData(GPUState.Unknown)]
    [InlineData(GPUState.NvidiaGpuNotFound)]
    [InlineData(GPUState.MonitorConnected)]
    [InlineData(GPUState.Active)]
    [InlineData(GPUState.Inactive)]
    [InlineData(GPUState.PoweredOff)]
    public void GPUState_AllValues_ShouldBeDefined(GPUState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region FanState/FanType Enum Tests

    [Theory]
    [InlineData(FanState.Auto)]
    [InlineData(FanState.Manual)]
    public void FanState_ShouldBeDefined(FanState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void FanState_Default_ShouldBeAuto()
    {
        default(FanState).Should().Be(FanState.Auto);
    }

    [Theory]
    [InlineData(FanType.Cpu)]
    [InlineData(FanType.Gpu)]
    public void FanType_ShouldBeDefined(FanType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region BatteryNightChargeState Enum Tests

    [Theory]
    [InlineData(BatteryNightChargeState.On)]
    [InlineData(BatteryNightChargeState.Off)]
    public void BatteryNightChargeState_ShouldBeDefined(BatteryNightChargeState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region AlwaysOnUSBState Enum Tests

    [Theory]
    [InlineData(AlwaysOnUSBState.Off)]
    [InlineData(AlwaysOnUSBState.OnWhenSleeping)]
    [InlineData(AlwaysOnUSBState.OnAlways)]
    public void AlwaysOnUSBState_ShouldBeDefined(AlwaysOnUSBState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region AutorunState Enum Tests

    [Theory]
    [InlineData(AutorunState.Enabled)]
    [InlineData(AutorunState.EnabledDelayed)]
    [InlineData(AutorunState.Disabled)]
    public void AutorunState_ShouldBeDefined(AutorunState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region DriverKey Enum Tests

    [Fact]
    public void DriverKey_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<DriverKey>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(DriverKey.FnF10, 32)]
    [InlineData(DriverKey.FnF4, 256)]
    [InlineData(DriverKey.FnF8, 8192)]
    [InlineData(DriverKey.FnSpace, 4096)]
    public void DriverKey_ShouldHaveExpectedValues(DriverKey key, int expected)
    {
        ((int)key).Should().Be(expected);
    }

    #endregion

    #region WindowsPowerPlan Additional Tests

    [Fact]
    public void WindowsPowerPlan_IsActive_False_ShouldWork()
    {
        var plan = new WindowsPowerPlan(Guid.NewGuid(), "Power Saver", false);
        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void WindowsPowerPlan_Equals_BoxedNonPlan_ShouldReturnFalse()
    {
        var plan = new WindowsPowerPlan(Guid.NewGuid(), "Balanced", true);
        plan.Equals("not a plan").Should().BeFalse();
    }

    [Fact]
    public void WindowsPowerPlan_GetHashCode_DifferentNamesSameGuid_ShouldMatch()
    {
        var guid = Guid.NewGuid();
        var a = new WindowsPowerPlan(guid, "Balanced", true);
        var b = new WindowsPowerPlan(guid, "High Performance", false);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion

    #region SpectrumLayout Enum Tests

    [Theory]
    [InlineData(SpectrumLayout.KeyboardOnly)]
    [InlineData(SpectrumLayout.KeyboardAndFront)]
    [InlineData(SpectrumLayout.Full)]
    [InlineData(SpectrumLayout.FullAlternative)]
    public void SpectrumLayout_ShouldBeDefined(SpectrumLayout value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region NotificationType Extended Coverage

    [Theory]
    [InlineData(NotificationType.ACAdapterConnected)]
    [InlineData(NotificationType.ACAdapterDisconnected)]
    [InlineData(NotificationType.CameraOn)]
    [InlineData(NotificationType.CameraOff)]
    [InlineData(NotificationType.MicrophoneOn)]
    [InlineData(NotificationType.MicrophoneOff)]
    [InlineData(NotificationType.PanelLogoLightingOn)]
    [InlineData(NotificationType.PanelLogoLightingOff)]
    [InlineData(NotificationType.PortLightingOn)]
    [InlineData(NotificationType.PortLightingOff)]
    [InlineData(NotificationType.PowerModeQuiet)]
    [InlineData(NotificationType.PowerModeBalance)]
    [InlineData(NotificationType.PowerModePerformance)]
    [InlineData(NotificationType.RGBKeyboardBacklightOff)]
    public void NotificationType_ExtendedValues_ShouldBeDefined(NotificationType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region DriverInfo Additional Tests

    [Fact]
    public void DriverInfo_DifferentDeviceIds_ShouldNotBeEqual()
    {
        var a = new DriverInfo("DEV1", "HW1", new Version(1, 0), null);
        var b = new DriverInfo("DEV2", "HW1", new Version(1, 0), null);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void DriverInfo_DifferentHardwareIds_ShouldNotBeEqual()
    {
        var a = new DriverInfo("DEV1", "HW1", new Version(1, 0), null);
        var b = new DriverInfo("DEV1", "HW2", new Version(1, 0), null);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void DriverInfo_DifferentVersions_ShouldNotBeEqual()
    {
        var a = new DriverInfo("DEV1", "HW1", new Version(1, 0), null);
        var b = new DriverInfo("DEV1", "HW1", new Version(2, 0), null);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void DriverInfo_DifferentDates_ShouldNotBeEqual()
    {
        var a = new DriverInfo("DEV1", "HW1", new Version(1, 0), new DateTime(2025, 1, 1));
        var b = new DriverInfo("DEV1", "HW1", new Version(1, 0), new DateTime(2025, 6, 1));
        a.Equals(b).Should().BeFalse();
    }

    #endregion

    #region RGBKeyboardBacklightState Additional Tests

    [Fact]
    public void RGBKeyboardBacklightState_MultiplePresets_ShouldRetainAll()
    {
        var presets = new Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription>
        {
            { RGBKeyboardBacklightPreset.One, RGBKeyboardBacklightBacklightPresetDescription.Default },
            { RGBKeyboardBacklightPreset.Two, new RGBKeyboardBacklightBacklightPresetDescription(
                RGBKeyboardBacklightEffect.Breath, RGBKeyboardBacklightSpeed.Fast,
                RGBKeyboardBacklightBrightness.Low, RGBColor.Red, RGBColor.Green, RGBColor.Teal, RGBColor.White) },
            { RGBKeyboardBacklightPreset.Three, RGBKeyboardBacklightBacklightPresetDescription.Default }
        };
        var state = new RGBKeyboardBacklightState(RGBKeyboardBacklightPreset.Two, presets);

        state.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Two);
        state.Presets.Should().HaveCount(3);
        state.Presets[RGBKeyboardBacklightPreset.Two].Effect.Should().Be(RGBKeyboardBacklightEffect.Breath);
    }

    #endregion

    #region StepperValue Additional Tests

    [Fact]
    public void StepperValue_NullSteps_ShouldWork()
    {
        var sv = new StepperValue(10, 0, 100, 1, null!, 50);
        sv.Steps.Should().BeNull();
    }

    [Fact]
    public void StepperValue_WithNullDefaultValue_ShouldBeNull()
    {
        var sv = new StepperValue(10, 0, 100, 1, [0, 50, 100], null);
        sv.DefaultValue.Should().BeNull();
    }

    [Fact]
    public void StepperValue_WithSteps_ShouldPreserveArray()
    {
        int[] steps = [0, 25, 50, 75, 100];
        var sv = new StepperValue(50, 0, 100, 25, steps, 50);
        sv.Steps.Should().BeSameAs(steps);
    }

    #endregion
}

