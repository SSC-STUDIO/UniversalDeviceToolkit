using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers.GodMode;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Bridges GodModeController for the Electron Custom Mode Settings modal
/// (WPF GodModeSettingsWindow parity): load enriched state (fan table sensor
/// data + defaults), persist presets, and apply to hardware.
/// </summary>
public static class GodModeHandlers
{
    private static readonly SemaphoreSlim MutationGate = new(1, 1);

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("godMode.getState", (_, cancellationToken) => HandleGetStateAsync(cancellationToken));
        rpc.RegisterHandler("godMode.setState", (request, cancellationToken) => HandleSetStateAsync(request, cancellationToken));
        rpc.RegisterHandler("godMode.apply", (_, cancellationToken) => HandleApplyAsync(cancellationToken));
    }

    private static GodModeController Controller => IoCContainer.Resolve<GodModeController>();

    internal static async Task<BridgeResult> HandleGetStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controller = Controller;
            if (!await controller.IsSupportedAsync().ConfigureAwait(false))
                return BridgeResult.Error(BridgeErrorCodes.GodModeUnsupported, "Custom Mode is not supported on this device.");

            var state = await controller.GetStateAsync().ConfigureAwait(false);
            var minimum = await controller.GetMinimumFanTableAsync().ConfigureAwait(false);
            var defaultTable = await controller.GetDefaultFanTableAsync().ConfigureAwait(false);
            var defaults = await controller.GetDefaultsInOtherPowerModesAsync().ConfigureAwait(false);

            var needsVantage = await controller.NeedsVantageDisabledAsync().ConfigureAwait(false);
            var needsLegionZone = await controller.NeedsLegionZoneDisabledAsync().ConfigureAwait(false);
            var vantageStatus = await IoCContainer.Resolve<VantageDisabler>().GetStatusAsync().ConfigureAwait(false);
            var legionZoneStatus = await IoCContainer.Resolve<LegionZoneDisabler>().GetStatusAsync().ConfigureAwait(false);

            return BridgeResult.Ok(new
            {
                state = SerializeState(state),
                minimumFanTable = minimum.GetTable().Select(v => (int)v).ToArray(),
                defaultFanTable = defaultTable.GetTable().Select(v => (int)v).ToArray(),
                defaults = defaults.ToDictionary(
                    kv => kv.Key.ToString(),
                    kv => SerializeDefaults(kv.Value)),
                warnVantage = needsVantage && vantageStatus == SoftwareStatus.Enabled,
                warnLegionZone = needsLegionZone && legionZoneStatus == SoftwareStatus.Enabled,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MapGodModeException(ex);
        }
    }

    internal static async Task<BridgeResult> HandleSetStateAsync(BridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("state", out var stateElement))
                return BridgeResult.Error(-32602, "Missing parameter 'state'.");

            var state = ParseState(stateElement);
            var apply = request.Parameters.TryGetProperty("apply", out var applyProp)
                        && applyProp.ValueKind == JsonValueKind.True;

            await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var controller = Controller;
                if (!await controller.IsSupportedAsync().ConfigureAwait(false))
                    return BridgeResult.Error(BridgeErrorCodes.GodModeUnsupported, "Custom Mode is not supported on this device.");

                await controller.SetStateAsync(state).ConfigureAwait(false);

                if (apply)
                {
                    var blocked = await GetApplyBlockedReasonAsync(controller).ConfigureAwait(false);
                    if (blocked is not null)
                        return BridgeResult.Error(-32603, blocked);

                    await controller.ApplyStateAsync().ConfigureAwait(false);
                }

                var refreshed = await controller.GetStateAsync().ConfigureAwait(false);
                return BridgeResult.Ok(new { state = SerializeState(refreshed), applied = apply });
            }
            finally
            {
                MutationGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MapGodModeException(ex);
        }
    }

    internal static async Task<BridgeResult> HandleApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var controller = Controller;
                if (!await controller.IsSupportedAsync().ConfigureAwait(false))
                    return BridgeResult.Error(BridgeErrorCodes.GodModeUnsupported, "Custom Mode is not supported on this device.");

                var blocked = await GetApplyBlockedReasonAsync(controller).ConfigureAwait(false);
                if (blocked is not null)
                    return BridgeResult.Error(-32603, blocked);

                await controller.ApplyStateAsync().ConfigureAwait(false);
                return BridgeResult.Ok(new { ok = true });
            }
            finally
            {
                MutationGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MapGodModeException(ex);
        }
    }

    private static async Task<string?> GetApplyBlockedReasonAsync(GodModeController controller)
    {
        if (await controller.NeedsVantageDisabledAsync().ConfigureAwait(false) &&
            await IoCContainer.Resolve<VantageDisabler>().GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
            return "Custom Mode cannot be applied while Lenovo Vantage is enabled.";

        if (await controller.NeedsLegionZoneDisabledAsync().ConfigureAwait(false) &&
            await IoCContainer.Resolve<LegionZoneDisabler>().GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
            return "Custom Mode cannot be applied while Legion Zone is enabled.";

        return null;
    }

    internal static BridgeResult MapGodModeException(Exception ex) => ex switch
    {
        BridgeErrorException bridge => BridgeResult.Error(bridge.Code, bridge.Message),
        WmiWriteIndeterminateException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        WmiWriteBusyException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        WmiWriteUnavailableException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        WmiWriteFailedIndeterminateException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        _ => BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"),
    };

    private static object SerializeState(GodModeState state)
    {
        var presets = new Dictionary<string, object>();
        foreach (var (id, preset) in state.Presets)
            presets[id.ToString()] = SerializePreset(preset);

        return new
        {
            ActivePresetId = state.ActivePresetId.ToString(),
            Presets = presets,
        };
    }

    private static object SerializePreset(GodModePreset preset) => new
    {
        Name = preset.Name,
        PowerPlanGuid = preset.PowerPlanGuid?.ToString(),
        PowerMode = preset.PowerMode?.ToString(),
        SourcePowerMode = preset.SourcePowerMode?.ToString(),
        CPULongTermPowerLimit = SerializeStepper(preset.CPULongTermPowerLimit),
        CPUShortTermPowerLimit = SerializeStepper(preset.CPUShortTermPowerLimit),
        CPUPeakPowerLimit = SerializeStepper(preset.CPUPeakPowerLimit),
        CPUCrossLoadingPowerLimit = SerializeStepper(preset.CPUCrossLoadingPowerLimit),
        CPUPL1Tau = SerializeStepper(preset.CPUPL1Tau),
        APUsPPTPowerLimit = SerializeStepper(preset.APUsPPTPowerLimit),
        CPUTemperatureLimit = SerializeStepper(preset.CPUTemperatureLimit),
        GPUPowerBoost = SerializeStepper(preset.GPUPowerBoost),
        GPUConfigurableTGP = SerializeStepper(preset.GPUConfigurableTGP),
        GPUTemperatureLimit = SerializeStepper(preset.GPUTemperatureLimit),
        GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline =
            SerializeStepper(preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline),
        GPUToCPUDynamicBoost = SerializeStepper(preset.GPUToCPUDynamicBoost),
        FanTable = preset.FanTableInfo is { } info
            ? SerializeFanTable(info.Table)
            : null,
        FanSensors = preset.FanTableInfo is { } fanInfo
            ? fanInfo.Data.Select(SerializeFanSensor).ToArray()
            : Array.Empty<object>(),
        FanFullSpeed = preset.FanFullSpeed,
        MinValueOffset = preset.MinValueOffset,
        MaxValueOffset = preset.MaxValueOffset,
    };

    private static object? SerializeStepper(StepperValue? stepper)
    {
        if (stepper is null)
            return null;
        var value = stepper.Value;
        return new
        {
            Value = value.Value,
            Min = value.Min,
            Max = value.Max,
            Step = value.Step,
            Steps = value.Steps ?? [],
            DefaultValue = value.DefaultValue,
        };
    }

    private static object SerializeFanTable(FanTable table) => new
    {
        FSTM = table.FSTM,
        FSID = table.FSID,
        FSTL = table.FSTL,
        FSS0 = table.FSS0,
        FSS1 = table.FSS1,
        FSS2 = table.FSS2,
        FSS3 = table.FSS3,
        FSS4 = table.FSS4,
        FSS5 = table.FSS5,
        FSS6 = table.FSS6,
        FSS7 = table.FSS7,
        FSS8 = table.FSS8,
        FSS9 = table.FSS9,
    };

    private static object SerializeFanSensor(FanTableData data) => new
    {
        Type = data.Type.ToString(),
        FanSpeeds = data.FanSpeeds.Select(v => (int)v).ToArray(),
        Temps = data.Temps.Select(v => (int)v).ToArray(),
    };

    private static object SerializeDefaults(GodModeDefaults defaults) => new
    {
        CPULongTermPowerLimit = defaults.CPULongTermPowerLimit,
        CPUShortTermPowerLimit = defaults.CPUShortTermPowerLimit,
        CPUPeakPowerLimit = defaults.CPUPeakPowerLimit,
        CPUCrossLoadingPowerLimit = defaults.CPUCrossLoadingPowerLimit,
        CPUPL1Tau = defaults.CPUPL1Tau,
        APUsPPTPowerLimit = defaults.APUsPPTPowerLimit,
        CPUTemperatureLimit = defaults.CPUTemperatureLimit,
        GPUPowerBoost = defaults.GPUPowerBoost,
        GPUConfigurableTGP = defaults.GPUConfigurableTGP,
        GPUTemperatureLimit = defaults.GPUTemperatureLimit,
        GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline =
            defaults.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
        GPUToCPUDynamicBoost = defaults.GPUToCPUDynamicBoost,
        FanTable = defaults.FanTable is { } table
            ? table.GetTable().Select(v => (int)v).ToArray()
            : null,
        FanFullSpeed = defaults.FanFullSpeed,
    };

    internal static GodModeState ParseState(JsonElement root)
    {
        var activePresetId = ReadGuid(GetProp(root, "ActivePresetId", "activePresetId"))
            ?? throw new BridgeErrorException(-32602, "ActivePresetId is required.");

        var presetsElement = GetProp(root, "Presets", "presets");
        if (presetsElement.ValueKind != JsonValueKind.Object)
            throw new BridgeErrorException(-32602, "Presets object is required.");

        var presets = new Dictionary<Guid, GodModePreset>();
        foreach (var property in presetsElement.EnumerateObject())
        {
            if (!Guid.TryParse(property.Name, out var presetId))
                throw new BridgeErrorException(-32602, $"Preset key '{property.Name}' is not a valid GUID.");
            presets[presetId] = ParsePreset(property.Value);
        }

        if (presets.Count == 0)
            throw new BridgeErrorException(-32602, "At least one preset is required.");
        if (!presets.ContainsKey(activePresetId))
            throw new BridgeErrorException(-32602, $"Active preset {activePresetId} was not found.");

        return new GodModeState
        {
            ActivePresetId = activePresetId,
            Presets = new ReadOnlyDictionary<Guid, GodModePreset>(presets),
        };
    }

    private static GodModePreset ParsePreset(JsonElement obj)
    {
        FanTableInfo? fanTableInfo = null;
        var fanTableElement = GetProp(obj, "FanTable", "fanTable");
        if (fanTableElement.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            var speeds = ReadFanSpeeds(fanTableElement)
                ?? throw new BridgeErrorException(-32602, "FanTable must be a 10-step array or FSS0-FSS9 object with integer speeds 0-10.");
            fanTableInfo = new FanTableInfo([], new FanTable(speeds));
        }

        return new GodModePreset
        {
            Name = ReadString(GetProp(obj, "Name", "name")) ?? string.Empty,
            PowerPlanGuid = ReadGuid(GetProp(obj, "PowerPlanGuid", "powerPlanGuid")),
            PowerMode = ReadEnum<WindowsPowerMode>(GetProp(obj, "PowerMode", "powerMode")),
            SourcePowerMode = ReadEnum<PowerModeState>(GetProp(obj, "SourcePowerMode", "sourcePowerMode")),
            CPULongTermPowerLimit = ParseStepper(GetProp(obj, "CPULongTermPowerLimit", "cpuLongTermPowerLimit")),
            CPUShortTermPowerLimit = ParseStepper(GetProp(obj, "CPUShortTermPowerLimit", "cpuShortTermPowerLimit")),
            CPUPeakPowerLimit = ParseStepper(GetProp(obj, "CPUPeakPowerLimit", "cpuPeakPowerLimit")),
            CPUCrossLoadingPowerLimit = ParseStepper(GetProp(obj, "CPUCrossLoadingPowerLimit", "cpuCrossLoadingPowerLimit")),
            CPUPL1Tau = ParseStepper(GetProp(obj, "CPUPL1Tau", "cpuPL1Tau")),
            APUsPPTPowerLimit = ParseStepper(GetProp(obj, "APUsPPTPowerLimit", "apUsPPTPowerLimit")),
            CPUTemperatureLimit = ParseStepper(GetProp(obj, "CPUTemperatureLimit", "cpuTemperatureLimit")),
            GPUPowerBoost = ParseStepper(GetProp(obj, "GPUPowerBoost", "gpuPowerBoost")),
            GPUConfigurableTGP = ParseStepper(GetProp(obj, "GPUConfigurableTGP", "gpuConfigurableTGP")),
            GPUTemperatureLimit = ParseStepper(GetProp(obj, "GPUTemperatureLimit", "gpuTemperatureLimit")),
            GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline =
                ParseStepper(GetProp(obj, "GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline",
                    "gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline")),
            GPUToCPUDynamicBoost = ParseStepper(GetProp(obj, "GPUToCPUDynamicBoost", "gpuToCPUDynamicBoost")),
            FanTableInfo = fanTableInfo,
            FanFullSpeed = ReadBool(GetProp(obj, "FanFullSpeed", "fanFullSpeed")),
            MinValueOffset = ReadInt(GetProp(obj, "MinValueOffset", "minValueOffset")),
            MaxValueOffset = ReadInt(GetProp(obj, "MaxValueOffset", "maxValueOffset")),
        };
    }

    private static StepperValue? ParseStepper(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null or not JsonValueKind.Object)
            return null;
        var value = ReadInt(GetProp(element, "Value", "value"));
        if (value is null)
            return null;
        var steps = ReadIntArray(GetProp(element, "Steps", "steps")) ?? [];
        return new StepperValue(
            value.Value,
            ReadInt(GetProp(element, "Min", "min")) ?? 0,
            ReadInt(GetProp(element, "Max", "max")) ?? 0,
            ReadInt(GetProp(element, "Step", "step")) ?? 1,
            steps,
            ReadInt(GetProp(element, "DefaultValue", "defaultValue")));
    }

    private static ushort[]? ReadFanSpeeds(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var values = ReadIntArray(element);
            if (values is null || values.Length != 10 || values.Any(v => v is < 0 or > 10))
                return null;
            return values.Select(v => (ushort)v).ToArray();
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var speeds = new ushort[10];
        for (var i = 0; i < 10; i++)
        {
            var speed = ReadInt(GetProp(element, $"FSS{i}", $"fss{i}"));
            if (speed is null || speed.Value is < 0 or > 10)
                return null;
            speeds[i] = (ushort)speed.Value;
        }
        return speeds;
    }

    private static JsonElement GetProp(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return default;
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var value))
                return value;
        }
        foreach (var property in obj.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
        }
        return default;
    }

    private static string? ReadString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static int? ReadInt(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;

    private static bool? ReadBool(JsonElement element) =>
        element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : null;

    private static Guid? ReadGuid(JsonElement element)
    {
        var text = ReadString(element);
        return text is not null && Guid.TryParse(text, out var guid) ? guid : null;
    }

    private static TEnum? ReadEnum<TEnum>(JsonElement element) where TEnum : struct, Enum
    {
        var text = ReadString(element);
        return text is not null && Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) ? value : null;
    }

    private static int[]? ReadIntArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return null;
        var list = new List<int>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value))
                return null;
            list.Add(value);
        }
        return list.ToArray();
    }
}
