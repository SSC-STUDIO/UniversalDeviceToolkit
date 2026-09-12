using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Settings;


public class GodModeSettings() : AbstractSettings<GodModeSettings.GodModeSettingsStore>("godmode.json")
{
    public class GodModeSettingsStore
    {
        public class Preset
        {
            public string Name { get; init; } = string.Empty;
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
            public StepperValue? GPUPowerBoost { get; init; }
            public StepperValue? GPUConfigurableTGP { get; init; }
            public StepperValue? GPUTemperatureLimit { get; init; }
            public StepperValue? GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline { get; init; }
            public StepperValue? GPUToCPUDynamicBoost { get; init; }
            public FanTable? FanTable { get; init; }
            public bool? FanFullSpeed { get; init; }
            public int? MinValueOffset { get; init; }
            public int? MaxValueOffset { get; init; }
        }

        public Guid ActivePresetId { get; set; }

        public Dictionary<Guid, Preset> Presets { get; set; } = [];
    }

    // ReSharper disable once StringLiteralTypo

    public override GodModeSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<GodModeSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    private static GodModeSettingsStore? Normalize(GodModeSettingsStore? store)
    {
        if (store is null)
            return null;

        store.Presets = store.Presets?
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => NormalizePreset(kv.Value))
            ?? [];
        return store;
    }

    private static GodModeSettingsStore.Preset NormalizePreset(GodModeSettingsStore.Preset preset) => new()
    {
        Name = preset.Name ?? string.Empty,
        PowerPlanGuid = preset.PowerPlanGuid,
        PowerMode = preset.PowerMode,
        SourcePowerMode = preset.SourcePowerMode,
        CPULongTermPowerLimit = preset.CPULongTermPowerLimit,
        CPUShortTermPowerLimit = preset.CPUShortTermPowerLimit,
        CPUPeakPowerLimit = preset.CPUPeakPowerLimit,
        CPUCrossLoadingPowerLimit = preset.CPUCrossLoadingPowerLimit,
        CPUPL1Tau = preset.CPUPL1Tau,
        APUsPPTPowerLimit = preset.APUsPPTPowerLimit,
        CPUTemperatureLimit = preset.CPUTemperatureLimit,
        GPUPowerBoost = preset.GPUPowerBoost,
        GPUConfigurableTGP = preset.GPUConfigurableTGP,
        GPUTemperatureLimit = preset.GPUTemperatureLimit,
        GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
        GPUToCPUDynamicBoost = preset.GPUToCPUDynamicBoost,
        FanTable = preset.FanTable,
        FanFullSpeed = preset.FanFullSpeed,
        MinValueOffset = preset.MinValueOffset,
        MaxValueOffset = preset.MaxValueOffset,
    };
}
