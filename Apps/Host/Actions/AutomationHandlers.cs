using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Serialization;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.Automation.Utils;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Automation bridge: enable state, $type-preserving pipeline read/write, manual
/// pipeline runs and the list of step types supported on this machine.
/// </summary>
public static class AutomationHandlers
{
    private static JsonSerializerOptions? _options;

    /// <summary>
    /// LltJson.CreateCompactOptions() semantics (compact, enums as strings) plus
    /// the automation $type converters, with camelCase names per the protocol.
    /// Reading is case-insensitive (handled by AutomationSerialization.CreateOptions).
    /// </summary>
    private static JsonSerializerOptions Options => _options ??= CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = AutomationSerialization.CreateOptions();
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static AutomationSettings Settings => IoCContainer.Resolve<AutomationSettings>();

    private static AutomationProcessor Processor => IoCContainer.Resolve<AutomationProcessor>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("automation.getState", (request, _) => HandleGetStateAsync());
        rpc.RegisterHandler("automation.setEnabled", (request, _) => HandleSetEnabledAsync(request));
        rpc.RegisterHandler("automation.savePipelines", (request, _) => HandleSavePipelinesAsync(request));
        rpc.RegisterHandler("automation.runNow", (request, _) => HandleRunNowAsync(request));
        rpc.RegisterHandler("automation.getSupportedSteps", (request, _) => HandleGetSupportedStepsAsync());
    }

    private static async Task<BridgeResult> HandleGetStateAsync()
    {
        try
        {
            // Serialize the live AutomationSettings store verbatim so $type
            // discriminators survive the round trip unchanged.
            var store = Settings.Store;
            var payload = JsonSerializer.SerializeToElement(store, store.GetType(), Options);

            await Task.CompletedTask;
            return BridgeResult.Ok(payload);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleSetEnabledAsync(BridgeRequest request)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("enabled", out var enabledProp) ||
                enabledProp.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new BridgeErrorException(-32602, "Missing boolean parameter 'enabled'.");

            await Processor.SetEnabledAsync(enabledProp.GetBoolean()).ConfigureAwait(false);

            return BridgeResult.Ok(new { ok = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleSavePipelinesAsync(BridgeRequest request)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("pipelines", out var pipelinesProp) ||
                pipelinesProp.ValueKind != JsonValueKind.Array)
                throw new BridgeErrorException(-32602, "Missing array parameter 'pipelines'.");

            // Reuse the $type-aware converters so trigger/step payloads deserialize
            // exactly as they were emitted (unknown properties are preserved).
            var pipelines = JsonSerializer.Deserialize<List<AutomationPipeline>>(pipelinesProp.GetRawText(), Options)
                ?? throw new BridgeErrorException(-32603, "Deserialized pipelines are null.");

            await Processor.ReloadPipelinesAsync(pipelines).ConfigureAwait(false);

            if (request.Parameters.TryGetProperty("isEnabled", out var isEnabledProp) &&
                isEnabledProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                await Processor.SetEnabledAsync(isEnabledProp.GetBoolean()).ConfigureAwait(false);
            }

            return BridgeResult.Ok(new { saved = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleRunNowAsync(BridgeRequest request)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("pipelineId", out var idProp) ||
                idProp.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(idProp.GetString(), out var pipelineId))
                throw new BridgeErrorException(-32602, "Missing or invalid string parameter 'pipelineId'.");

            await Processor.RunNowAsync(pipelineId).ConfigureAwait(false);

            return BridgeResult.Ok(new { ok = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleGetSupportedStepsAsync()
    {
        try
        {
            // Mirrors AutomationPage.GetSupportedAutomationStepsAsync: same step
            // palette, hardware-filtered via IsSupportedAsync. Individual failures
            // are skipped so one unsupported feature cannot break the whole list.
            var factories = new Func<IAutomationStep>[]
            {
                () => new AlwaysOnUsbAutomationStep(default),
                () => new BatteryAutomationStep(default),
                () => new BatteryNightChargeAutomationStep(default),
                () => new DeactivateGPUAutomationStep(default),
                () => new DelayAutomationStep(default),
                () => new DisplayBrightnessAutomationStep(50),
                () => new DpiScaleAutomationStep(default),
                () => new FlipToStartAutomationStep(default),
                () => new FnLockAutomationStep(default),
                () => new GodModePresetAutomationStep(default),
                () => new HDRAutomationStep(default),
                () => new InstantBootAutomationStep(default),
                () => new MacroAutomationStep(default),
                () => new MicrophoneAutomationStep(default),
                () => new SpeakerAutomationStep(default),
                () => new NotificationAutomationStep(default),
                () => new OsdAutomationStep(default),
                () => new OneLevelWhiteKeyboardBacklightAutomationStep(default),
                () => new OverclockDiscreteGPUAutomationStep(default),
                () => new OverDriveAutomationStep(default),
                () => new PanelLogoBacklightAutomationStep(default),
                () => new PlaySoundAutomationStep(default),
                () => new PortsBacklightAutomationStep(default),
                () => new PowerModeAutomationStep(default),
                () => new QuickActionAutomationStep(default),
                () => new RefreshRateAutomationStep(default),
                () => new ResolutionAutomationStep(default),
                () => new RGBKeyboardBacklightAutomationStep(default),
                () => new RunAutomationStep(default, default, default, default),
                () => new SpectrumKeyboardBacklightBrightnessAutomationStep(0),
                () => new SpectrumKeyboardBacklightProfileAutomationStep(1),
                () => new SpectrumKeyboardBacklightImportProfileAutomationStep(default),
                () => new TouchpadLockAutomationStep(default),
                () => new TurnOffMonitorsAutomationStep(),
                () => new TurnOffWiFiAutomationStep(),
                () => new TurnOnWiFiAutomationStep(),
                () => new HybridModeAutomationStep(default),
                () => new WhiteKeyboardBacklightAutomationStep(default),
                () => new WinKeyAutomationStep(default),
                () => new ShowMainWindowAutomationStep(),
                () => new HideMainWindowAutomationStep(),
            };

            var supported = new List<string>();
            foreach (var factory in factories)
            {
                try
                {
                    var step = factory();
                    if (await step.IsSupportedAsync().ConfigureAwait(false))
                        supported.Add(GetStepDiscriminator(step.GetType()));
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("Automation step support check failed.", ex);
                }
            }

            return BridgeResult.Ok(new { steps = supported });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Mirrors the internal AutomationJsonDiscriminators.ForStep mapping:
    /// class name minus the "AutomationStep" suffix, camelCased (e.g.
    /// PowerModeAutomationStep → "powerMode").
    /// </summary>
    private static string GetStepDiscriminator(Type stepType)
    {
        const string suffix = "AutomationStep";
        var name = stepType.Name;
        if (name.EndsWith(suffix, StringComparison.Ordinal))
            name = name[..^suffix.Length];
        return string.IsNullOrEmpty(name) ? stepType.Name : JsonNamingPolicy.CamelCase.ConvertName(name);
    }
}
