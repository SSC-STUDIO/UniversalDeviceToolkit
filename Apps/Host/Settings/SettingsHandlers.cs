using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.GameDetection;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Shared.Serialization;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Settings;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Generic settings bridge: whole-scope get/set with optional dotted-path get,
/// explicit save and reload. Scopes map 1:1 to AbstractSettings JSON files.
/// </summary>
public static class SettingsHandlers
{
    private sealed record ScopeEntry(string Key, Type StoreType, Func<object> Resolve, Action<object> Synchronize);

    private static readonly Dictionary<string, ScopeEntry> Scopes = new(StringComparer.Ordinal);

    private static JsonSerializerOptions? _options;

    private static JsonSerializerOptions Options => _options ??= LltJson.CreateCompactOptions();

    public static void Register(BridgeRpcServer rpc)
    {
        RegisterScope("application", () => IoCContainer.Resolve<ApplicationSettings>());
        RegisterScope("osd", () => IoCContainer.Resolve<OsdSettings>());
        RegisterScope("hardwareSensors", () => IoCContainer.Resolve<HardwareSensorSettings>());
        RegisterScope("balanceMode", () => IoCContainer.Resolve<BalanceModeSettings>());
        RegisterScope("godMode", () => IoCContainer.Resolve<GodModeSettings>());
        RegisterScope("gpuOverclock", () => IoCContainer.Resolve<GPUOverclockSettings>());
        RegisterScope("integrations", () => IoCContainer.Resolve<IntegrationsSettings>());
        RegisterScope("lampArray", () => IoCContainer.Resolve<LampArraySettings>());
        RegisterScope("fanCurves", () => IoCContainer.Resolve<FanCurveSettings>());
        RegisterScope("packageDownloader", () => IoCContainer.Resolve<PackageDownloaderSettings>());
        RegisterScope("rgbKeyboard", () => IoCContainer.Resolve<RGBKeyboardSettings>());
        RegisterScope("spectrumKeyboard", () => IoCContainer.Resolve<SpectrumKeyboardSettings>());
        RegisterScope("sunriseSunset", () => IoCContainer.Resolve<SunriseSunsetSettings>());
        RegisterScope("updateCheck", () => IoCContainer.Resolve<UpdateCheckSettings>());
        RegisterScope("networkAcceleration", () => IoCContainer.Resolve<NetworkAccelerationSettings>());
        RegisterScope("batteryHealthAlerts", () => IoCContainer.Resolve<BatteryHealthAlertSettings>());
        RegisterScope("dashboard", () => IoCContainer.Resolve<HostDashboardSettings>());
        RegisterScope("gameBoost", () => IoCContainer.Resolve<GameBoostSettings>());

        rpc.RegisterHandler("settings.getAll", (request, _) => HandleGetAllAsync(request, rpc));
        rpc.RegisterHandler("settings.get", (request, _) => HandleGetAsync(request));
        rpc.RegisterHandler("settings.set", (request, _) => HandleSetAsync(request, rpc));
        rpc.RegisterHandler("settings.save", (request, _) => HandleSaveAsync(request, rpc));
        rpc.RegisterHandler("settings.reload", (request, _) => HandleReloadAsync(request));
    }

    private static void RegisterScope(string key, Func<object> resolve)
    {
        var settingsType = resolve().GetType();
        var storeProperty = settingsType.GetProperty("Store", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Settings type {settingsType.Name} has no Store property.");

        var synchronize = settingsType.GetMethod("SynchronizeStore", Type.EmptyTypes)
            ?? throw new InvalidOperationException($"Settings type {settingsType.Name} has no SynchronizeStore method.");

        Scopes[key] = new ScopeEntry(
            key,
            storeProperty.PropertyType,
            resolve,
            instance => synchronize.Invoke(instance, null));
    }

    private static object GetStore(string scope)
        => GetScope(scope).Resolve() is { } settings
            ? settings.GetType().GetProperty("Store")!.GetValue(settings)!
            : throw new InvalidOperationException("Settings instance is null.");

    private static ScopeEntry GetScope(string scope)
    {
        if (!Scopes.TryGetValue(scope, out var entry))
            throw new BridgeErrorException(-32602, $"Unknown settings scope: {scope}. Available: {string.Join(", ", Scopes.Keys)}");
        return entry;
    }

    private static async Task<BridgeResult> HandleGetAllAsync(BridgeRequest request, BridgeRpcServer rpc)
    {
        try
        {
            var scopes = request.Parameters.TryGetProperty("scopes", out var scopesProp) && scopesProp.ValueKind == JsonValueKind.Array
                ? scopesProp.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Cast<string>().ToList()
                : Scopes.Keys.ToList();

            var result = new Dictionary<string, JsonElement?>();
            foreach (var scope in scopes)
            {
                if (!Scopes.TryGetValue(scope, out var entry))
                    throw new BridgeErrorException(-32602, $"Unknown settings scope: {scope}");
                var json = JsonSerializer.SerializeToElement(GetStore(scope), entry.StoreType, Options);
                result[scope] = json;
            }

            await Task.CompletedTask;
            return BridgeResult.Ok(new { scopes = result });
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

    private static async Task<BridgeResult> HandleGetAsync(BridgeRequest request)
    {
        try
        {
            var scope = GetRequiredString(request, "scope");
            var entry = GetScope(scope);
            var node = JsonSerializer.SerializeToNode(GetStore(scope), entry.StoreType, Options);

            if (request.Parameters.TryGetProperty("path", out var pathProp) &&
                pathProp.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(pathProp.GetString()))
            {
                foreach (var segment in pathProp.GetString()!.Split('.'))
                {
                    if (node is null || node.GetValueKind() != JsonValueKind.Object)
                        throw new BridgeErrorException(-32602, $"Path segment '{segment}' does not exist.");
                    node = node[segment];
                }
            }

            await Task.CompletedTask;
            return BridgeResult.Ok(new { scope, value = node?.ToJsonString(Options) is { } json ? JsonNode.Parse(json) : null });
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

    private static async Task<BridgeResult> HandleSetAsync(BridgeRequest request, BridgeRpcServer rpc)
    {
        try
        {
            var scope = GetRequiredString(request, "scope");
            var entry = GetScope(scope);

            if (!request.Parameters.TryGetProperty("value", out var valueProp))
                throw new BridgeErrorException(-32602, "Missing 'value' parameter.");

            var json = valueProp.GetRawText();
            var store = GetStore(scope);
            var replacement = JsonSerializer.Deserialize(json, entry.StoreType, Options)
                ?? throw new BridgeErrorException(-32603, "Deserialized settings value is null.");

            CopyProperties(replacement, store);
            await ApplyIntegrationsLifecycleAsync(scope).ConfigureAwait(false);

            rpc.Publish("settings.changed", new { scope, reason = "set" });
            return BridgeResult.Ok(new { scope, applied = true });
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

    private static async Task<BridgeResult> HandleSaveAsync(BridgeRequest request, BridgeRpcServer rpc)
    {
        try
        {
            var scopes = request.Parameters.TryGetProperty("scopes", out var scopesProp) && scopesProp.ValueKind == JsonValueKind.Array
                ? scopesProp.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Cast<string>().ToList()
                : Scopes.Keys.ToList();

            var saved = new List<string>();
            foreach (var scope in scopes)
            {
                var entry = GetScope(scope);
                entry.Synchronize(entry.Resolve());
                saved.Add(scope);
                rpc.Publish("settings.changed", new { scope, reason = "save" });
            }

            if (saved.Contains("integrations", StringComparer.Ordinal))
                await ApplyIntegrationsLifecycleAsync("integrations").ConfigureAwait(false);

            return BridgeResult.Ok(new { saved });
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

    private static async Task<BridgeResult> HandleReloadAsync(BridgeRequest request)
    {
        try
        {
            var scope = GetRequiredString(request, "scope");
            var entry = GetScope(scope);
            var settings = entry.Resolve();
            var invalidate = settings.GetType().GetMethod("InvalidateCache", Type.EmptyTypes);
            invalidate?.Invoke(settings, null);

            await Task.CompletedTask;
            return BridgeResult.Ok(new { reloaded = true });
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

    internal static ICliHostLifecycle? LifecycleOverrideForTests { get; set; }

    /// <summary>
    /// settings.set and settings.save both call this so the CLI named pipe
    /// starts or stops as soon as the Integrations CLI toggle is applied.
    /// </summary>
    internal static async Task ApplyIntegrationsLifecycleAsync(string scope)
    {
        if (!string.Equals(scope, "integrations", StringComparison.Ordinal))
            return;

        var lifecycle = LifecycleOverrideForTests ?? IoCContainer.Resolve<ICliHostLifecycle>();
        await lifecycle.StartStopIfNeededAsync().ConfigureAwait(false);
    }

    private static string GetRequiredString(BridgeRequest request, string property)
    {
        if (!request.Parameters.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new BridgeErrorException(-32602, $"Missing string parameter '{property}'.");
        return prop.GetString()!;
    }

    private static void CopyProperties(object source, object target)
    {
        var sourceType = source.GetType();
        foreach (var property in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
                continue;
            if (property.SetMethod is not { } setter)
                continue;
            if (setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
                continue;

            var value = property.GetValue(source);
            property.SetValue(target, value);
        }
    }
}
