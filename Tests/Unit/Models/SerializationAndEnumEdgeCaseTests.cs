using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class SerializationAndEnumEdgeCaseTests
{
    #region LegacyPowerPlanGuidJsonConverter Tests

    [Fact]
    public void LegacyPowerPlanGuidJsonConverter_Read_LegacyFormat_ShouldExtractGuid()
    {
        var guid = Guid.NewGuid();
        var legacy = $"Microsoft:PowerPlan\\{{{guid}}}";
        var json = "\"Microsoft:PowerPlan\\\\{" + guid + "}\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        var result = JsonSerializer.Deserialize<Guid>(json, options);

        result.Should().Be(guid);
    }

    [Fact]
    public void LegacyPowerPlanGuidJsonConverter_Read_PlainGuid_ShouldParseDirectly()
    {
        var guid = Guid.NewGuid();
        var json = $"\"{guid}\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        var result = JsonSerializer.Deserialize<Guid>(json, options);

        result.Should().Be(guid);
    }

    [Fact]
    public void LegacyPowerPlanGuidJsonConverter_Read_EmptyString_ShouldReturnEmptyGuid()
    {
        var json = "\"\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        var result = JsonSerializer.Deserialize<Guid>(json, options);

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void LegacyPowerPlanGuidJsonConverter_Read_InvalidGuid_ShouldReturnEmptyGuid()
    {
        var json = "\"not-a-guid\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        var result = JsonSerializer.Deserialize<Guid>(json, options);

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void LegacyPowerPlanGuidJsonConverter_Read_LegacyPrefixNoClosingBrace_ShouldReturnEmptyGuid()
    {
        var json = "\"Microsoft:PowerPlan\\\\{no-closing-brace\"";
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        var result = JsonSerializer.Deserialize<Guid>(json, options);

        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void LegacyPowerPlanGuidJsonConverter_Write_ShouldOutputGuidString()
    {
        var guid = Guid.NewGuid();
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        var json = JsonSerializer.Serialize(guid, options);

        json.Should().Be($"\"{guid}\"");
    }

    #endregion

    #region PInvokeExtensions Constants Tests

    [Fact]
    public void PInvokeExtensions_ErrorSuccess_ShouldBeZero()
    {
        PInvokeExtensions.ERROR_SUCCESS.Should().Be(0);
    }

    [Fact]
    public void PInvokeExtensions_ErrorNoMoreItems_ShouldBe259()
    {
        PInvokeExtensions.ERROR_NO_MORE_ITEMS.Should().Be(259);
    }

    [Fact]
    public void PInvokeExtensions_KF_FLAG_DEFAULT_ShouldBeZero()
    {
        PInvokeExtensions.KF_FLAG_DEFAULT.Should().Be(0u);
    }

    [Fact]
    public void PInvokeExtensions_ConsoleDisplayState_Enum_ShouldHaveThreeValues()
    {
        var values = Enum.GetValues<PInvokeExtensions.CONSOLE_DISPLAY_STATE>();
        values.Should().HaveCount(3);
        values.Should().Contain(PInvokeExtensions.CONSOLE_DISPLAY_STATE.Off);
        values.Should().Contain(PInvokeExtensions.CONSOLE_DISPLAY_STATE.On);
        values.Should().Contain(PInvokeExtensions.CONSOLE_DISPLAY_STATE.Dimmed);
    }

    [Fact]
    public void PInvokeExtensions_DisplayBrightnessSettingGuid_ShouldBeValid()
    {
        PInvokeExtensions.DISPLAY_BRIGTHNESS_SETTING_GUID.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region ModifierKey Enum Tests

    [Theory]
    [InlineData(ModifierKey.None, 0)]
    [InlineData(ModifierKey.Shift, 1)]
    [InlineData(ModifierKey.Ctrl, 2)]
    [InlineData(ModifierKey.Alt, 4)]
    public void ModifierKey_ShouldHaveExpectedValues(ModifierKey key, int expectedValue)
    {
        ((int)key).Should().Be(expectedValue);
    }

    [Fact]
    public void ModifierKey_CanCombineFlags()
    {
        var combined = ModifierKey.Shift | ModifierKey.Ctrl;
        combined.Should().HaveFlag(ModifierKey.Shift);
        combined.Should().HaveFlag(ModifierKey.Ctrl);
        combined.Should().NotHaveFlag(ModifierKey.Alt);
    }

    #endregion

    #region LegionSeries Enum Tests

    [Theory]
    [InlineData(LegionSeries.Legion_5, 0)]
    [InlineData(LegionSeries.Legion_Pro_5, 1)]
    [InlineData(LegionSeries.Legion_Slim_5, 2)]
    [InlineData(LegionSeries.Legion_7, 3)]
    [InlineData(LegionSeries.Legion_Pro_7, 4)]
    [InlineData(LegionSeries.Legion_9, 5)]
    [InlineData(LegionSeries.Legion_Go, 6)]
    [InlineData(LegionSeries.LOQ, 11)]
    [InlineData(LegionSeries.YOGA, 12)]
    [InlineData(LegionSeries.Unknown, 255)]
    public void LegionSeries_ShouldHaveExpectedValues(LegionSeries series, int expectedValue)
    {
        ((int)series).Should().Be(expectedValue);
    }

    #endregion

    #region RebootType Enum Tests

    [Theory]
    [InlineData(RebootType.NotRequired, 0)]
    [InlineData(RebootType.Forced, 1)]
    [InlineData(RebootType.Requested, 3)]
    [InlineData(RebootType.ForcedPowerOff, 4)]
    [InlineData(RebootType.Delayed, 5)]
    public void RebootType_ShouldHaveExpectedValues(RebootType type, int expectedValue)
    {
        ((int)type).Should().Be(expectedValue);
    }

    #endregion

    #region TemperatureUnit Enum Tests

    [Fact]
    public void TemperatureUnit_Celsius_ShouldBeZero()
    {
        ((int)TemperatureUnit.C).Should().Be(0);
    }

    [Fact]
    public void TemperatureUnit_Fahrenheit_ShouldBeOne()
    {
        ((int)TemperatureUnit.F).Should().Be(1);
    }

    [Fact]
    public void TemperatureUnit_ShouldHaveTwoValues()
    {
        Enum.GetValues<TemperatureUnit>().Should().HaveCount(2);
    }

    #endregion

    #region FanType Enum Tests

    [Theory]
    [InlineData(FanType.Cpu, 0)]
    [InlineData(FanType.Gpu, 1)]
    [InlineData(FanType.System, 2)]
    public void FanType_ShouldHaveExpectedValues(FanType type, int expectedValue)
    {
        ((int)type).Should().Be(expectedValue);
    }

    #endregion

    #region FanTableType Enum Tests

    [Fact]
    public void FanTableType_ShouldHaveSixValues()
    {
        Enum.GetValues<FanTableType>().Should().HaveCount(6);
    }

    [Fact]
    public void FanTableType_Unknown_ShouldBeFirstValue()
    {
        ((int)FanTableType.Unknown).Should().Be(0);
    }

    #endregion

    #region SpecialKey Enum Tests

    [Theory]
    [InlineData(SpecialKey.FnF9, 1)]
    [InlineData(SpecialKey.FnLockOn, 2)]
    [InlineData(SpecialKey.FnLockOff, 3)]
    [InlineData(SpecialKey.FnPrtSc, 4)]
    [InlineData(SpecialKey.FnPrtSc2, 45)]
    [InlineData(SpecialKey.CameraOn, 12)]
    [InlineData(SpecialKey.CameraOff, 13)]
    [InlineData(SpecialKey.FnR, 16)]
    public void SpecialKey_ShouldHaveExpectedValues(SpecialKey key, int expectedValue)
    {
        ((int)key).Should().Be(expectedValue);
    }

    #endregion

    #region SoftwareStatus Enum Tests

    [Fact]
    public void SoftwareStatus_ShouldHaveThreeValues()
    {
        Enum.GetValues<SoftwareStatus>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(SoftwareStatus.Enabled, 0)]
    [InlineData(SoftwareStatus.Disabled, 1)]
    [InlineData(SoftwareStatus.NotFound, 2)]
    public void SoftwareStatus_ShouldHaveExpectedValues(SoftwareStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }

    #endregion

    #region PawnIOState Enum Tests

    [Fact]
    public void PawnIOState_ShouldHaveTwoValues()
    {
        Enum.GetValues<PawnIOState>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(PawnIOState.NotInstalled, 0)]
    [InlineData(PawnIOState.Installed, 1)]
    public void PawnIOState_ShouldHaveExpectedValues(PawnIOState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    #endregion

    #region LibreHardwareMonitorInitialState Enum Tests

    [Theory]
    [InlineData(LibreHardwareMonitorInitialState.Fail, 0)]
    [InlineData(LibreHardwareMonitorInitialState.Initialized, 1)]
    [InlineData(LibreHardwareMonitorInitialState.Success, 2)]
    [InlineData(LibreHardwareMonitorInitialState.PawnIONotInstalled, 3)]
    public void LibreHardwareMonitorInitialState_ShouldHaveExpectedValues(LibreHardwareMonitorInitialState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    #endregion

    #region PowerAdapterStatus Enum Tests

    [Fact]
    public void PowerAdapterStatus_ShouldHaveThreeValues()
    {
        Enum.GetValues<PowerAdapterStatus>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(PowerAdapterStatus.Connected, 0)]
    [InlineData(PowerAdapterStatus.ConnectedLowWattage, 1)]
    [InlineData(PowerAdapterStatus.Disconnected, 2)]
    public void PowerAdapterStatus_ShouldHaveExpectedValues(PowerAdapterStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }

    #endregion

    #region ProcessEventInfoType Enum Tests

    [Fact]
    public void ProcessEventInfoType_ShouldHaveTwoValues()
    {
        Enum.GetValues<ProcessEventInfoType>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(ProcessEventInfoType.Started, 0)]
    [InlineData(ProcessEventInfoType.Stopped, 1)]
    public void ProcessEventInfoType_ShouldHaveExpectedValues(ProcessEventInfoType type, int expectedValue)
    {
        ((int)type).Should().Be(expectedValue);
    }

    #endregion

    #region LightingChangeState Enum Tests

    [Theory]
    [InlineData(LightingChangeState.Panel, 0)]
    [InlineData(LightingChangeState.Ports, 1)]
    public void LightingChangeState_ShouldHaveExpectedValues(LightingChangeState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    #endregion

    #region FanCurveEntry Additional Edge Cases

    [Fact]
    public void FanCurveEntry_RampUpThresholds_Default_ShouldBeNull()
    {
        var entry = new FanCurveEntry();
        entry.RampUpThresholds.Should().BeNull();
    }

    [Fact]
    public void FanCurveEntry_RampDownThresholds_Default_ShouldBeNull()
    {
        var entry = new FanCurveEntry();
        entry.RampDownThresholds.Should().BeNull();
    }

    [Fact]
    public void FanCurveEntry_AccelerationDcrReduction_Default_ShouldBe1()
    {
        var entry = new FanCurveEntry();
        entry.AccelerationDcrReduction.Should().Be(1);
    }

    [Fact]
    public void FanCurveEntry_DecelerationDcrReduction_Default_ShouldBe2()
    {
        var entry = new FanCurveEntry();
        entry.DecelerationDcrReduction.Should().Be(2);
    }

    [Fact]
    public void FanCurveEntry_CurveNodes_CollectionChanged_ShouldFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "CurveNodes") fired = true; };
        entry.CurveNodes.Add(new CurveNode { Temperature = 35, TargetPercent = 10 });
        fired.Should().BeTrue();
    }

    [Fact]
    public void FanCurveEntry_RampUpThresholds_Set_ShouldFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "RampUpThresholds") fired = true; };
        entry.RampUpThresholds = new[] { 45, 55, 65 };
        fired.Should().BeTrue();
    }

    [Fact]
    public void FanCurveEntry_RampUpThresholds_NullToNonNull_ShouldFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "RampUpThresholds") fired = true; };
        entry.RampUpThresholds = new[] { 45, 55 };
        fired.Should().BeTrue();
    }

    [Fact]
    public void FanCurveEntry_RampUpThresholds_SetNullFromNonNull_ShouldFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        entry.RampUpThresholds = new[] { 45, 55 };
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "RampUpThresholds") fired = true; };
        entry.RampUpThresholds = null;
        fired.Should().BeTrue();
    }

    [Fact]
    public void FanCurveEntry_RampDownThresholds_Set_ShouldFirePropertyChanged()
    {
        var entry = new FanCurveEntry();
        var fired = false;
        entry.PropertyChanged += (s, e) => { if (e.PropertyName == "RampDownThresholds") fired = true; };
        entry.RampDownThresholds = new[] { 85, 75, 65 };
        fired.Should().BeTrue();
    }

    [Fact]
    public void FanCurveEntry_ExportJson_ImportJson_ShouldRoundTrip()
    {
        var original = new FanCurveEntry
        {
            Type = FanType.Gpu,
            CriticalTemp = 95,
            MaxPwm = 200.0,
            AccelerationDcrReduction = 3,
            DecelerationDcrReduction = 4
        };
        original.CurveNodes.Clear();
        original.CurveNodes.Add(new CurveNode { Temperature = 30, TargetPercent = 10 });
        original.CurveNodes.Add(new CurveNode { Temperature = 60, TargetPercent = 50 });
        original.CurveNodes.Add(new CurveNode { Temperature = 90, TargetPercent = 100 });

        var json = original.ExportToJson();
        var imported = FanCurveEntry.ImportFromJson(json);

        imported.Type.Should().Be(FanType.Gpu);
        imported.CriticalTemp.Should().Be(95);
        imported.MaxPwm.Should().Be(200.0);
        imported.AccelerationDcrReduction.Should().Be(3);
        imported.DecelerationDcrReduction.Should().Be(4);
        imported.CurveNodes.Should().HaveCount(3);
        imported.CurveNodes[0].Temperature.Should().Be(30);
        imported.CurveNodes[2].TargetPercent.Should().Be(100);
    }

    [Fact]
    public void FanCurveEntry_ImportFromJson_InvalidJson_ShouldThrow()
    {
        var act = () => FanCurveEntry.ImportFromJson("not valid json");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void FanCurveEntry_ToConfig_ShouldReturnValidConfig()
    {
        var entry = new FanCurveEntry
        {
            CriticalTemp = 88,
            MaxPwm = 240.0,
            AccelerationDcrReduction = 5,
            DecelerationDcrReduction = 6
        };

        var config = entry.ToConfig();

        config.CriticalTemp.Should().Be(88);
        config.MaxPwm.Should().Be(240.0);
        config.AccelerationDcrReduction.Should().Be(5);
        config.DecelerationDcrReduction.Should().Be(6);
    }

    #endregion

    #region IntExtensions Additional Edge Cases

    [Theory]
    [InlineData(-1, 0, true)]
    [InlineData(-1, 31, true)]
    [InlineData(int.MinValue, 31, true)]
    [InlineData(int.MaxValue, 0, true)]
    [InlineData(0, 0, false)]
    [InlineData(1, 31, false)]
    public void IsBitSet_WithBoundaryValues_ShouldReturnExpected(int value, int position, bool expected)
    {
        value.IsBitSet(position).Should().Be(expected);
    }

    #endregion

    #region DictionaryExtensions Additional Edge Cases

    [Fact]
    public void AddRange_EmptySource_ShouldNotModifyDictionary()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1 };
        dict.AddRange(new Dictionary<string, int>());
        dict.Should().HaveCount(1);
        dict["a"].Should().Be(1);
    }

    [Fact]
    public void AsReadOnlyDictionary_Empty_ShouldReturnEmptyReadOnly()
    {
        var dict = new Dictionary<string, int>();
        var ro = dict.AsReadOnlyDictionary();
        ro.Should().BeEmpty();
    }

    [Fact]
    public void AsReadOnlyDictionary_ModificationToOriginal_ShouldReflectInReadOnly()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1 };
        var ro = dict.AsReadOnlyDictionary();
        dict["b"] = 2;
        ro.Should().ContainKey("b");
    }

    #endregion

    #region EnumExtensions Additional Edge Cases

    [Fact]
    public void GetFlagsDisplayName_ZeroValue_ShouldReturnEmpty()
    {
        BootLogoFormat format = 0;
        format.GetFlagsDisplayName().Should().BeEmpty();
    }

    [Fact]
    public void GetFlagsDisplayName_AllFlags_ShouldContainAll()
    {
        BootLogoFormat format = BootLogoFormat.Bmp | BootLogoFormat.Png | BootLogoFormat.Jpeg;
        var result = format.GetFlagsDisplayName();
        result.Should().Contain("Bmp");
        result.Should().Contain("Png");
        result.Should().Contain("Jpeg");
    }

    #endregion

    #region FanCurveEntry ImportFromJson Minimal Tests

    [Fact]
    public void FanCurveEntry_ImportFromJson_MinimalJson_ShouldUseDefaults()
    {
        var json = "{}";
        var imported = FanCurveEntry.ImportFromJson(json);
        imported.Type.Should().Be(FanType.Cpu);
        imported.CriticalTemp.Should().Be(90);
        imported.MaxPwm.Should().Be(255.0);
    }

    [Fact]
    public void FanCurveEntry_ExportToJson_ShouldBeDeserializable()
    {
        var entry = new FanCurveEntry();
        var json = entry.ExportToJson();
        json.Should().NotBeNullOrWhiteSpace();
        var imported = FanCurveEntry.ImportFromJson(json);
        imported.Should().NotBeNull();
    }

    #endregion
}
