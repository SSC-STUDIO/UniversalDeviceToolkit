using System;
using System.IO;
using System.Text.Json;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Process-wide culture bridge shared by the Electron renderer and the Host.
/// </summary>
public static class LocalizationHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("localization.getCulture", async (_, _) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return BridgeResult.Ok(new { culture = LocalizationRuntime.CurrentCulture.Name });
        });

        rpc.RegisterHandler("localization.setCulture", async request =>
        {
            try
            {
                if (!TryGetCultureName(request.Parameters, out var cultureName))
                    return BridgeResult.Error(-32602, "Expected parameter: culture (string).");

                var culture = await LocalizationRuntime.SetCultureAsync(cultureName, persist: true)
                    .ConfigureAwait(false);

                if (!IsPersistedCulture(culture.Name))
                {
                    return BridgeResult.Error(
                        -32603,
                        "Failed to persist the culture preference.");
                }

                return BridgeResult.Ok(new { culture = culture.Name });
            }
            catch (Exception ex)
            {
                return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private static bool IsPersistedCulture(string expectedCultureName)
    {
        try
        {
            var path = LocalizationRuntime.LanguageFilePath;
            if (!File.Exists(path))
                return false;

            var stored = File.ReadAllText(path).Trim();
            return stored.Equals(expectedCultureName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryGetCultureName(JsonElement parameters, out string cultureName)
    {
        cultureName = string.Empty;
        if (parameters.ValueKind != JsonValueKind.Object)
            return false;

        if (!parameters.TryGetProperty("culture", out var culture) ||
            culture.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = culture.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        cultureName = value.Trim();
        return true;
    }
}
