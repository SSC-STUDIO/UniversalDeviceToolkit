#if !WINDOWS
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Lib;

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// JSON-backed automation pipeline store for portable hosts. Vendor step
/// execution (EC, WMI, lighting) is not implemented; the store still round-trips
/// so the Electron automation page can persist pipelines.
/// </summary>
internal static class PortableStoreHandlers
{
    private const string AutomationSection = "udt.automation";
    private const string AutomationKey = "state";

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("automation.getState", HandleGetStateAsync);
        rpc.RegisterHandler("automation.setEnabled", HandleSetEnabledAsync);
        rpc.RegisterHandler("automation.savePipelines", HandleSavePipelinesAsync);
        rpc.RegisterHandler("automation.runNow", HandleRunNowAsync);
        rpc.RegisterHandler("automation.getSupportedSteps", HandleGetSupportedStepsAsync);
    }

    private static Task<BridgeResult> HandleGetStateAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing());

        return Task.FromResult(BridgeResult.Ok(ReadState(store)));
    }

    private static Task<BridgeResult> HandleSetEnabledAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing());

        if (request.Parameters.ValueKind != JsonValueKind.Object ||
            !request.Parameters.TryGetProperty("enabled", out var enabledProp) ||
            enabledProp.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing boolean parameter 'enabled'."));
        }

        var state = ReadState(store);
        state["isEnabled"] = enabledProp.GetBoolean();
        if (!TryWriteState(store, state))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, "Failed to persist automation state."));

        return Task.FromResult(BridgeResult.Ok(new { ok = true }));
    }

    private static Task<BridgeResult> HandleSavePipelinesAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing());

        if (request.Parameters.ValueKind != JsonValueKind.Object ||
            !request.Parameters.TryGetProperty("pipelines", out var pipelinesProp) ||
            pipelinesProp.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing array parameter 'pipelines'."));
        }

        JsonArray pipelines;
        try
        {
            pipelines = JsonNode.Parse(pipelinesProp.GetRawText()) as JsonArray ?? new JsonArray();
        }
        catch (JsonException ex)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Invalid pipelines: {ex.Message}"));
        }

        var state = ReadState(store);
        state["pipelines"] = pipelines;
        if (request.Parameters.TryGetProperty("isEnabled", out var enabledProp) &&
            enabledProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            state["isEnabled"] = enabledProp.GetBoolean();
        }

        if (!TryWriteState(store, state))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, "Failed to persist automation pipelines."));

        return Task.FromResult(BridgeResult.Ok(new { saved = true }));
    }

    private static Task<BridgeResult> HandleRunNowAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(BridgeResult.Error(
            BridgeErrorCodes.PlatformNotSupported,
            "Running automation pipelines is not implemented on this platform. Pipelines can still be saved."));
    }

    private static Task<BridgeResult> HandleGetSupportedStepsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var steps = new[] { "delay", "run", "notification", "osd", "showMainWindow", "hideMainWindow" };
        if (PortableFeatureSupport.IsSupported("powerMode"))
            steps = [.. steps, "powerMode"];
        if (PortableFeatureSupport.IsSupported("battery"))
            steps = [.. steps, "battery"];
        return Task.FromResult(BridgeResult.Ok(new { steps }));
    }

    private static JsonObject ReadState(IConfigurationStore store)
    {
        var json = store.GetValue(AutomationSection, AutomationKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonNode.Parse(json) is JsonObject parsed)
                {
                    parsed["isEnabled"] ??= false;
                    parsed["pipelines"] ??= new JsonArray();
                    return parsed;
                }
            }
            catch (JsonException)
            {
            }
        }

        return new JsonObject
        {
            ["isEnabled"] = false,
            ["pipelines"] = new JsonArray(),
        };
    }

    private static bool TryWriteState(IConfigurationStore store, JsonObject state)
    {
        var json = state.ToJsonString();
        store.SetValue(AutomationSection, AutomationKey, json);
        return string.Equals(store.GetValue(AutomationSection, AutomationKey), json, StringComparison.Ordinal);
    }

    private static BridgeResult Missing() =>
        BridgeResult.Error(BridgeErrorCodes.PlatformNotSupported, "Configuration is not available on this platform.");
}
#endif
