using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Host.Settings;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Dashboard bridge: reads/writes the host dashboard.json (same schema as WPF)
/// using camelCase JSON to match the Electron frontend.
/// </summary>
public static class DashboardHandlers
{
    private static JsonSerializerOptions? _options;

    private static JsonSerializerOptions Options => _options ??= CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = LltJson.CreateCompactOptions();
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        return options;
    }

    private static HostDashboardSettings GetSettings() => IoCContainer.Resolve<HostDashboardSettings>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("dashboard.getConfig", (_, ct) => HandleGetConfigAsync(ct));
        rpc.RegisterHandler("dashboard.saveConfig", (request, ct) => HandleSaveConfigAsync(request, ct));
    }

    private static Task<BridgeResult> HandleGetConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var store = GetSettings().Store;
            HostDashboardSettings.Normalize(store);
            var node = JsonSerializer.SerializeToNode(store, store.GetType(), Options);
            return Task.FromResult(BridgeResult.Ok(node));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static async Task<BridgeResult> HandleSaveConfigAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!request.Parameters.TryGetProperty("config", out var configProp) ||
                configProp.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new BridgeErrorException(-32602, "Missing 'config' parameter.");
            }

            var replacement = JsonSerializer.Deserialize<HostDashboardSettings.DashboardSettingsStore>(configProp.GetRawText(), Options)
                ?? throw new BridgeErrorException(-32603, "Deserialized dashboard config is null.");
            var settings = GetSettings();
            var store = settings.Store;
            CopyProperties(replacement, store);
            HostDashboardSettings.Normalize(store);

            cancellationToken.ThrowIfCancellationRequested();
            await settings.SynchronizeStoreAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { saved = true });
        }
        catch (OperationCanceledException)
        {
            throw;
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
