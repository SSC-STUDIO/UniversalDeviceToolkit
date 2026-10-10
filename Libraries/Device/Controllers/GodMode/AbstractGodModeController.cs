using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.GodMode;

public abstract class AbstractGodModeController(GodModeSettings settings)
    : IGodModeController
{
    private static readonly PowerModeState[] BasePresetPowerModes =
    [
        PowerModeState.Quiet,
        PowerModeState.Balance,
        PowerModeState.Performance
    ];

    public event EventHandler<Guid>? PresetChanged;

    public abstract Task<bool> NeedsVantageDisabledAsync();

    public abstract Task<bool> NeedsLegionZoneDisabledAsync();

    public Task<Guid> GetActivePresetIdAsync() => Task.FromResult(settings.Store.ActivePresetId);

    public Task<string?> GetActivePresetNameAsync()
    {
        var store = settings.Store;
        var name = store.Presets
            .Where(p => p.Key == store.ActivePresetId)
            .Select(p => p.Value.Name)
            .FirstOrDefault();
        return Task.FromResult(name);
    }

    public async Task<GodModeState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var store = settings.Store;
        var defaultState = await GetDefaultStateAsync().ConfigureAwait(false);

        if (!IsValidStore(store))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Loading initial state...");

            var initialState = await CreateInitialStateAsync(defaultState).ConfigureAwait(false);
            SaveState(initialState);
            return initialState;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Loading state from store...");

        var state = await LoadStateFromStoreAsync(store, defaultState).ConfigureAwait(false);
        var migratedState = await EnsureBasePresetsAsync(state, defaultState).ConfigureAwait(false);
        migratedState = NormalizeGeneratedDefaultPreset(migratedState);
        if (HasStateChanged(state, migratedState))
            SaveState(migratedState);

        return migratedState;
    }

    public async Task SetStateAsync(GodModeState state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state...");

        var stateToSave = await EnsureBasePresetsForSaveAsync(state).ConfigureAwait(false);
        SaveState(stateToSave);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State saved.");
    }

    private void SaveState(GodModeState state)
    {
        settings.Store.ActivePresetId = state.ActivePresetId;
        settings.Store.Presets = ToSettingsPresets(state.Presets);
        settings.SynchronizeStore();
    }

    private static Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset> ToSettingsPresets(IReadOnlyDictionary<Guid, GodModePreset> source)
    {
        var presets = new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>();

        foreach (var (id, preset) in source)
        {
            presets.Add(id, new()
            {
                Name = preset.Name,
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
                FanTable = preset.FanTableInfo?.Table,
                FanFullSpeed = preset.FanFullSpeed,
                MinValueOffset = preset.MinValueOffset,
                MaxValueOffset = preset.MaxValueOffset,
            });
        }

        return presets;
    }

    public abstract Task ApplyStateAsync();

    public Task<FanTable> GetDefaultFanTableAsync()
    {
        var fanTable = new FanTable([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        return Task.FromResult(fanTable);
    }

    public abstract Task<FanTable> GetMinimumFanTableAsync();

    public abstract Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync();

    public abstract Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state);

    protected abstract Task<GodModePreset> GetDefaultStateAsync();

    protected void RaisePresetChanged(Guid presetId) => PresetChanged?.Invoke(this, presetId);

    protected async Task<(Guid, GodModeSettings.GodModeSettingsStore.Preset)> GetActivePresetAsync()
    {
        if (!IsValidStore(settings.Store))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Invalid store, generating default one.");

            var state = await GetStateAsync().ConfigureAwait(false);
            await SetStateAsync(state).ConfigureAwait(false);
        }

        var activePresetId = settings.Store.ActivePresetId;
        var presets = settings.Store.Presets;

        if (presets.TryGetValue(activePresetId, out var activePreset))
            return (activePresetId, activePreset);

        throw ExceptionHelper.GodModePresetNotFound(activePresetId);
    }

    protected async Task<bool> IsValidFanTableAsync(FanTable fanTable)
    {
        var minimumFanTable = await GetMinimumFanTableAsync().ConfigureAwait(false);
        var minimum = minimumFanTable.GetTable();
        return fanTable.GetTable().Where((t, i) => t < minimum[i] || t > 10u).IsEmpty();
    }

    private static bool IsValidStore(GodModeSettings.GodModeSettingsStore store) => store.Presets.Count != 0 && store.Presets.ContainsKey(store.ActivePresetId);

    private async Task<GodModeState> CreateInitialStateAsync(GodModePreset defaultState)
    {
        var presets = await CreateBasePresetsAsync(defaultState).ConfigureAwait(false);
        if (presets.Count == 0)
        {
            var id = Guid.NewGuid();
            presets[id] = defaultState;
        }

        var activePresetId = GetPreferredActivePresetId(presets)
            ?? throw ExceptionHelper.NoGodModePresetCreated();

        return new GodModeState
        {
            ActivePresetId = activePresetId,
            Presets = presets.AsReadOnlyDictionary()
        };
    }

    private async Task<GodModeState> EnsureBasePresetsForSaveAsync(GodModeState state)
    {
        var normalizedInputState = NormalizeGeneratedDefaultPreset(state);
        if (HasAllBasePresets(normalizedInputState.Presets.Values) && normalizedInputState.Presets.ContainsKey(normalizedInputState.ActivePresetId))
            return normalizedInputState;

        var defaultState = await GetDefaultStateAsync().ConfigureAwait(false);
        var normalizedState = await EnsureBasePresetsAsync(normalizedInputState, defaultState).ConfigureAwait(false);
        normalizedState = NormalizeGeneratedDefaultPreset(normalizedState);
        if (normalizedState.Presets.ContainsKey(normalizedState.ActivePresetId))
            return normalizedState;

        var presets = new Dictionary<Guid, GodModePreset>(normalizedState.Presets);
        var activePresetId = GetPreferredActivePresetId(presets)
                             ?? throw ExceptionHelper.NoGodModePresetAvailable();
        return normalizedState with { ActivePresetId = activePresetId };
    }

    private static GodModeState NormalizeGeneratedDefaultPreset(GodModeState state)
    {
        if (state.Presets is null || state.Presets.Count == 0)
            return state;

        var presets = new Dictionary<Guid, GodModePreset>(state.Presets);
        if (!HasAllBasePresets(presets.Values))
            return state;

        var generatedDefaultPresetIds = presets
            .Where(kv => IsGeneratedDefaultPreset(kv.Value))
            .Select(kv => kv.Key)
            .ToArray();

        if (generatedDefaultPresetIds.Length == 0)
            return state;

        var customPresetCount = presets.Count(kv => !kv.Value.SourcePowerMode.HasValue && !IsGeneratedDefaultPreset(kv.Value));
        var shouldRemoveGeneratedDefault = presets.ContainsKey(state.ActivePresetId) &&
                                           IsGeneratedDefaultPreset(presets[state.ActivePresetId]) &&
                                           customPresetCount == 0;

        if (!shouldRemoveGeneratedDefault)
            return state;

        foreach (var presetId in generatedDefaultPresetIds)
            presets.Remove(presetId);

        if (presets.Count == 0)
            return state;

        var activePresetId = presets.ContainsKey(state.ActivePresetId)
            ? state.ActivePresetId
            : GetPreferredActivePresetId(presets) ?? presets.OrderBy(kv => kv.Value.Name).Select(kv => kv.Key).First();

        return new GodModeState
        {
            ActivePresetId = activePresetId,
            Presets = presets.AsReadOnlyDictionary()
        };
    }

    private static bool IsGeneratedDefaultPreset(GodModePreset preset) =>
        !preset.SourcePowerMode.HasValue &&
        !preset.PowerPlanGuid.HasValue &&
        !preset.PowerMode.HasValue &&
        IsGeneratedDefaultPresetName(preset.Name);

    private static bool IsGeneratedDefaultPresetName(string? name)
    {
        var normalizedName = name?.Trim();
        return string.IsNullOrWhiteSpace(normalizedName) ||
               string.Equals(normalizedName, "Default", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedName, "Default V1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedName, "Default V2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasStateChanged(GodModeState previous, GodModeState current)
    {
        if (previous.ActivePresetId != current.ActivePresetId)
            return true;

        if (previous.Presets is null || current.Presets is null)
            return previous.Presets != current.Presets;

        return previous.Presets.Count != current.Presets.Count ||
               !previous.Presets.Keys.OrderBy(id => id).SequenceEqual(current.Presets.Keys.OrderBy(id => id));
    }

    private static Guid? GetPreferredActivePresetId(Dictionary<Guid, GodModePreset> presets)
    {
        if (presets.Count == 0)
            return null;

        return presets
            .Where(kv => kv.Value.SourcePowerMode == PowerModeState.Performance)
            .Select(kv => kv.Key)
            .DefaultIfEmpty(presets.OrderBy(kv => kv.Value.Name).Select(kv => kv.Key).First())
            .First();
    }

    private async Task<GodModeState> EnsureBasePresetsAsync(GodModeState state, GodModePreset defaultState)
    {
        if (HasAllBasePresets(state.Presets.Values))
            return state;

        var presets = new Dictionary<Guid, GodModePreset>(state.Presets);
        var basePresets = await CreateBasePresetsAsync(defaultState).ConfigureAwait(false);

        foreach (var (_, preset) in basePresets)
        {
            if (HasBasePreset(presets.Values, preset.SourcePowerMode))
                continue;

            presets[Guid.NewGuid()] = preset;
        }

        var activePresetId = presets.ContainsKey(state.ActivePresetId)
            ? state.ActivePresetId
            : GetPreferredActivePresetId(presets) ?? state.ActivePresetId;

        return state with
        {
            ActivePresetId = activePresetId,
            Presets = presets.AsReadOnlyDictionary()
        };
    }

    private static bool HasAllBasePresets(IEnumerable<GodModePreset> presets)
    {
        var presetList = presets.ToArray();
        return BasePresetPowerModes.All(mode => HasBasePreset(presetList, mode));
    }

    private async Task<Dictionary<Guid, GodModePreset>> CreateBasePresetsAsync(GodModePreset defaultState)
    {
        var presets = new Dictionary<Guid, GodModePreset>();
        var defaults = await GetDefaultsInOtherPowerModesAsync().ConfigureAwait(false);

        foreach (var mode in BasePresetPowerModes)
        {
            if (!defaults.TryGetValue(mode, out var modeDefaults))
                continue;

            if (!HasConfigurableDefault(modeDefaults))
                continue;

            presets[Guid.NewGuid()] = CreatePresetFromDefaults(defaultState, mode, modeDefaults);
        }

        return presets;
    }

    private static bool HasBasePreset(IEnumerable<GodModePreset> presets, PowerModeState? mode)
    {
        if (mode is null)
            return false;

        return presets.Any(preset => preset.SourcePowerMode == mode);
    }

    private static bool HasConfigurableDefault(GodModeDefaults defaults) =>
        defaults.CPULongTermPowerLimit.HasValue ||
        defaults.CPUShortTermPowerLimit.HasValue ||
        defaults.CPUPeakPowerLimit.HasValue ||
        defaults.CPUCrossLoadingPowerLimit.HasValue ||
        defaults.CPUPL1Tau.HasValue ||
        defaults.APUsPPTPowerLimit.HasValue ||
        defaults.CPUTemperatureLimit.HasValue ||
        defaults.GPUPowerBoost.HasValue ||
        defaults.GPUConfigurableTGP.HasValue ||
        defaults.GPUTemperatureLimit.HasValue ||
        defaults.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline.HasValue ||
        defaults.GPUToCPUDynamicBoost.HasValue ||
        defaults.FanTable.HasValue ||
        defaults.FanFullSpeed.HasValue;

    private static GodModePreset CreatePresetFromDefaults(GodModePreset defaultState, PowerModeState mode, GodModeDefaults defaults)
    {
        return defaultState with
        {
            Name = mode.GetDisplayName(),
            SourcePowerMode = mode,
            CPULongTermPowerLimit = CreateStepperValue(defaultState.CPULongTermPowerLimit, defaults.CPULongTermPowerLimit),
            CPUShortTermPowerLimit = CreateStepperValue(defaultState.CPUShortTermPowerLimit, defaults.CPUShortTermPowerLimit),
            CPUPeakPowerLimit = CreateStepperValue(defaultState.CPUPeakPowerLimit, defaults.CPUPeakPowerLimit),
            CPUCrossLoadingPowerLimit = CreateStepperValue(defaultState.CPUCrossLoadingPowerLimit, defaults.CPUCrossLoadingPowerLimit),
            CPUPL1Tau = CreateStepperValue(defaultState.CPUPL1Tau, defaults.CPUPL1Tau),
            APUsPPTPowerLimit = CreateStepperValue(defaultState.APUsPPTPowerLimit, defaults.APUsPPTPowerLimit),
            CPUTemperatureLimit = CreateStepperValue(defaultState.CPUTemperatureLimit, defaults.CPUTemperatureLimit),
            GPUPowerBoost = CreateStepperValue(defaultState.GPUPowerBoost, defaults.GPUPowerBoost),
            GPUConfigurableTGP = CreateStepperValue(defaultState.GPUConfigurableTGP, defaults.GPUConfigurableTGP),
            GPUTemperatureLimit = CreateStepperValue(defaultState.GPUTemperatureLimit, defaults.GPUTemperatureLimit),
            GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = CreateStepperValue(
                defaultState.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
                defaults.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline),
            GPUToCPUDynamicBoost = CreateStepperValue(defaultState.GPUToCPUDynamicBoost, defaults.GPUToCPUDynamicBoost),
            FanTableInfo = defaultState.FanTableInfo is { } fanTableInfo && defaults.FanTable is { } fanTable
                ? new FanTableInfo(fanTableInfo.Data, fanTable)
                : defaultState.FanTableInfo,
            FanFullSpeed = defaults.FanFullSpeed ?? defaultState.FanFullSpeed,
            MinValueOffset = 0,
            MaxValueOffset = 0
        };
    }

    private async Task<GodModeState> LoadStateFromStoreAsync(GodModeSettings.GodModeSettingsStore store, GodModePreset defaultState)
    {
        var states = new Dictionary<Guid, GodModePreset>();

        foreach (var (id, preset) in store.Presets)
        {
            states.Add(id, new GodModePreset
            {
                Name = preset.Name,
                PowerPlanGuid = preset.PowerPlanGuid,
                PowerMode = preset.PowerMode,
                SourcePowerMode = preset.SourcePowerMode,
                CPULongTermPowerLimit = CreateStepperValue(defaultState.CPULongTermPowerLimit, preset.CPULongTermPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUShortTermPowerLimit = CreateStepperValue(defaultState.CPUShortTermPowerLimit, preset.CPUShortTermPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUPeakPowerLimit = CreateStepperValue(defaultState.CPUPeakPowerLimit, preset.CPUPeakPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUCrossLoadingPowerLimit = CreateStepperValue(defaultState.CPUCrossLoadingPowerLimit, preset.CPUCrossLoadingPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUPL1Tau = CreateStepperValue(defaultState.CPUPL1Tau, preset.CPUPL1Tau, preset.MinValueOffset, preset.MaxValueOffset),
                APUsPPTPowerLimit = CreateStepperValue(defaultState.APUsPPTPowerLimit, preset.APUsPPTPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUTemperatureLimit = CreateStepperValue(defaultState.CPUTemperatureLimit, preset.CPUTemperatureLimit, preset.MinValueOffset, preset.MaxValueOffset),
                GPUPowerBoost = CreateStepperValue(defaultState.GPUPowerBoost, preset.GPUPowerBoost, preset.MinValueOffset, preset.MaxValueOffset),
                GPUConfigurableTGP = CreateStepperValue(defaultState.GPUConfigurableTGP, preset.GPUConfigurableTGP, preset.MinValueOffset, preset.MaxValueOffset),
                GPUTemperatureLimit = CreateStepperValue(defaultState.GPUTemperatureLimit, preset.GPUTemperatureLimit, preset.MinValueOffset, preset.MaxValueOffset),
                GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = CreateStepperValue(defaultState.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
                    preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
                    preset.MinValueOffset,
                    preset.MaxValueOffset),
                GPUToCPUDynamicBoost = CreateStepperValue(defaultState.GPUToCPUDynamicBoost, preset.GPUToCPUDynamicBoost),
                FanTableInfo = await GetFanTableInfoAsync(preset, defaultState.FanTableInfo?.Data).ConfigureAwait(false),
                FanFullSpeed = preset.FanFullSpeed,
                MinValueOffset = preset.MinValueOffset ?? defaultState.MinValueOffset,
                MaxValueOffset = preset.MaxValueOffset ?? defaultState.MaxValueOffset
            });
        }

        return new GodModeState
        {
            ActivePresetId = store.ActivePresetId,
            Presets = states.AsReadOnlyDictionary()
        };
    }

    private static StepperValue? CreateStepperValue(StepperValue? state, int? value)
    {
        if (!value.HasValue)
            return null;

        return CreateStepperValue(state, state?.WithValue(value.Value));
    }

    private static StepperValue? CreateStepperValue(StepperValue? state, StepperValue? store = null, int? minValueOffset = 0, int? maxValueOffset = 0)
    {
        if (state is not { } stateValue)
            return null;

        if (stateValue.Steps.Length > 0)
        {
            var value = store?.Value ?? stateValue.Value;
            var steps = stateValue.Steps;
            var defaultValue = stateValue.DefaultValue;

            if (!steps.Contains(value))
            {
                var valueTemp = value;
                value = steps.MinBy(v => Math.Abs((long)v - valueTemp));
            }

            return new(value, 0, 0, 0, steps, defaultValue);
        }

        if (stateValue.Step > 0)
        {
            var value = store?.Value ?? stateValue.Value;
            var min = Math.Max(0, stateValue.Min + (minValueOffset ?? 0));
            var max = stateValue.Max + (maxValueOffset ?? 0);
            var step = stateValue.Step;
            var defaultValue = stateValue.DefaultValue;

            value = MathExtensions.RoundNearest(value, step);

            if (value < min || value > max)
                value = defaultValue ?? Math.Clamp(value, min, max);

            return new(value, min, max, step, [], defaultValue);
        }

        return null;
    }

    private async Task<FanTableInfo?> GetFanTableInfoAsync(GodModeSettings.GodModeSettingsStore.Preset preset, FanTableData[]? fanTableData)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting fan table info...");

        if (fanTableData is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Fan table data is null");
            return null;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Fan table data retrieved: {fanTableData}");

        var fanTable = preset.FanTable ?? await GetDefaultFanTableAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Fan table retrieved: {fanTable}");

        if (!await IsValidFanTableAsync(fanTable).ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Fan table invalid, replacing with default...");

            fanTable = await GetDefaultFanTableAsync().ConfigureAwait(false);
        }

        return new FanTableInfo(fanTableData, fanTable);
    }
}
