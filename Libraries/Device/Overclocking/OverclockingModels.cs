using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace UniversalDeviceToolkit.Lib;

public readonly struct DiscreteCapability(CapabilityID id, int value)
{
    public CapabilityID Id { get; } = id;
    public int Value { get; } = value;
}

public readonly struct GPUOverclockInfo(int coreDeltaMhz, int memoryDeltaMhz)
{
    public static readonly GPUOverclockInfo Zero = new();

    public int CoreDeltaMhz { get; } = coreDeltaMhz;
    public int MemoryDeltaMhz { get; } = memoryDeltaMhz;

    #region Equality

    public override bool Equals(object? obj) => obj is GPUOverclockInfo other && CoreDeltaMhz == other.CoreDeltaMhz && MemoryDeltaMhz == other.MemoryDeltaMhz;

    public override int GetHashCode() => HashCode.Combine(CoreDeltaMhz, MemoryDeltaMhz);

    public static bool operator ==(GPUOverclockInfo left, GPUOverclockInfo right) => left.Equals(right);

    public static bool operator !=(GPUOverclockInfo left, GPUOverclockInfo right) => !left.Equals(right);

    #endregion

    public override string ToString() => $"{nameof(CoreDeltaMhz)}: {CoreDeltaMhz}, {nameof(MemoryDeltaMhz)}: {MemoryDeltaMhz}";

}

public readonly struct GodModeDefaults
{
    public int? CPULongTermPowerLimit { get; init; }
    public int? CPUShortTermPowerLimit { get; init; }
    public int? CPUPeakPowerLimit { get; init; }
    public int? CPUCrossLoadingPowerLimit { get; init; }
    public int? CPUPL1Tau { get; init; }
    public int? APUsPPTPowerLimit { get; init; }
    public int? CPUTemperatureLimit { get; init; }
    public int? PrecisionBoostOverdriveScaler { get; init; }
    public int? PrecisionBoostOverdriveBoostFrequency { get; init; }
    public int? AllCoreCurveOptimizer { get; init; }
    public bool? EnableAllCoreCurveOptimizer { get; init; }
    public bool? EnableOverclocking { get; init; }
    public int? GPUPowerBoost { get; init; }
    public int? GPUConfigurableTGP { get; init; }
    public int? GPUTemperatureLimit { get; init; }
    public int? GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline { get; init; }
    public int? GPUToCPUDynamicBoost { get; init; }
    public FanTable? FanTable { get; init; }
    public bool? FanFullSpeed { get; init; }

    public override string ToString() =>
        $"{nameof(CPULongTermPowerLimit)}: {CPULongTermPowerLimit}," +
        $" {nameof(CPUShortTermPowerLimit)}: {CPUShortTermPowerLimit}," +
        $" {nameof(CPUPeakPowerLimit)}: {CPUPeakPowerLimit}," +
        $" {nameof(CPUCrossLoadingPowerLimit)}: {CPUCrossLoadingPowerLimit}," +
        $" {nameof(CPUPL1Tau)}: {CPUPL1Tau}," +
        $" {nameof(APUsPPTPowerLimit)}: {APUsPPTPowerLimit}," +
        $" {nameof(CPUTemperatureLimit)}: {CPUTemperatureLimit}," +
        $" {nameof(GPUPowerBoost)}: {GPUPowerBoost}," +
        $" {nameof(GPUConfigurableTGP)}: {GPUConfigurableTGP}," +
        $" {nameof(GPUTemperatureLimit)}: {GPUTemperatureLimit}," +
        $" {nameof(GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline)}: {GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline}," +
        $" {nameof(GPUToCPUDynamicBoost)}: {GPUToCPUDynamicBoost}," +
        $" {nameof(FanTable)}: {FanTable}," +
        $" {nameof(FanFullSpeed)}: {FanFullSpeed}";
}

public readonly struct GodModeState
{
    public Guid ActivePresetId { get; init; }
    public ReadOnlyDictionary<Guid, GodModePreset> Presets { get; init; }
}

public readonly struct GodModePreset
{
    public string Name { get; init; }
    public Guid? PowerPlanGuid { get; init; }
    public WindowsPowerMode? PowerMode { get; init; }
    public PowerModeState? SourcePowerMode { get; init; }
    public StepperValue? CPULongTermPowerLimit { get; init; }
    public StepperValue? CPUShortTermPowerLimit { get; init; }
    public StepperValue? CPUPeakPowerLimit { get; init; }
    public StepperValue? CPUCrossLoadingPowerLimit { get; init; }
    public StepperValue? CPUPL1Tau { get; init; }
    public StepperValue? APUsPPTPowerLimit { get; init; }
    public StepperValue? CPUTemperatureLimit { get; init; }
    public StepperValue? PrecisionBoostOverdriveScaler { get; init; }
    public StepperValue? PrecisionBoostOverdriveBoostFrequency { get; init; }
    public StepperValue? AllCoreCurveOptimizer { get; init; }
    public bool? EnableAllCoreCurveOptimizer { get; init; }
    public bool? EnableOverclocking { get; init; }
    public StepperValue? GPUPowerBoost { get; init; }
    public StepperValue? GPUConfigurableTGP { get; init; }
    public StepperValue? GPUTemperatureLimit { get; init; }
    public StepperValue? GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline { get; init; }
    public StepperValue? GPUToCPUDynamicBoost { get; init; }
    public FanTableInfo? FanTableInfo { get; init; }
    public bool? FanFullSpeed { get; init; }
    public int? MinValueOffset { get; init; }
    public int? MaxValueOffset { get; init; }

    public override string ToString() =>
        $"{nameof(Name)}: {Name}," +
        $" {nameof(CPULongTermPowerLimit)}: {CPULongTermPowerLimit}," +
        $" {nameof(CPUShortTermPowerLimit)}: {CPUShortTermPowerLimit}," +
        $" {nameof(CPUPeakPowerLimit)}: {CPUPeakPowerLimit}," +
        $" {nameof(CPUCrossLoadingPowerLimit)}: {CPUCrossLoadingPowerLimit}," +
        $" {nameof(CPUPL1Tau)}: {CPUPL1Tau}," +
        $" {nameof(APUsPPTPowerLimit)}: {APUsPPTPowerLimit}," +
        $" {nameof(CPUTemperatureLimit)}: {CPUTemperatureLimit}," +
        $" {nameof(GPUPowerBoost)}: {GPUPowerBoost}," +
        $" {nameof(GPUConfigurableTGP)}: {GPUConfigurableTGP}," +
        $" {nameof(GPUTemperatureLimit)}: {GPUTemperatureLimit}," +
        $" {nameof(GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline)}: {GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline}," +
        $" {nameof(GPUToCPUDynamicBoost)}: {GPUToCPUDynamicBoost}," +
        $" {nameof(FanTableInfo)}: {FanTableInfo}," +
        $" {nameof(FanFullSpeed)}: {FanFullSpeed}," +
        $" {nameof(MinValueOffset)}: {MinValueOffset}," +
        $" {nameof(MaxValueOffset)}: {MaxValueOffset}";
}

public readonly struct RangeCapability(CapabilityID id, int defaultValue, int min, int max, int step)
{
    public CapabilityID Id { get; } = id;
    public int DefaultValue { get; } = defaultValue;
    public int Min { get; } = min;
    public int Max { get; } = max;
    public int Step { get; } = step;
}

public readonly struct StepperValue(int value, int min, int max, int step, int[] steps, int? defaultValue)
{
    public int Value { get; } = value;
    public int Min { get; } = min;
    public int Max { get; } = max;
    public int Step { get; } = step;
    public int[] Steps { get; } = steps;
    public int? DefaultValue { get; } = defaultValue;

    public StepperValue WithValue(int value) => new(value, Min, Max, Step, Steps, DefaultValue);

    public override string ToString() =>
        $"{nameof(Value)}: {Value}," +
        $" {nameof(Min)}: {Min}," +
        $" {nameof(Max)}: {Max}," +
        $" {nameof(Step)}: {Step}," +
        $" {nameof(Steps)}: [{string.Join(", ", Steps ?? [])}]," +
        $" {nameof(DefaultValue)} : {DefaultValue}";
}

public readonly struct AmdWmiCommand
{
    public string Name { get; init; }
    public uint Id { get; init; }
    public bool IsSet { get; init; }

    public override string ToString() => $"{Name} (0x{Id:X8})";
}

public readonly struct OverclockingProfile
{
    public uint? FMax { get; init; }
    public List<double?> CoreValues { get; init; }
}
