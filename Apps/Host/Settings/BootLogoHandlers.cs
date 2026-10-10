using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Boot logo bridge: UEFI boot logo status, enable and disable, backed by
/// Lib/System/BootLogo.cs (mirror of the WPF BootLogoWindow).
///
/// Contract:
///   bootLogo.getStatus -> { supported, enabled, resolution: { DisplayName }, formats, filters }
///   bootLogo.enable  { filePath } -> { ok }
///   bootLogo.disable             -> { ok }
/// All methods return errors as { error: message } instead of throwing.
/// </summary>
public static class BootLogoHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("bootLogo.getStatus", (request, _) => HandleGetStatusAsync());
        rpc.RegisterHandler("bootLogo.enable", (request, _) => HandleEnableAsync(request));
        rpc.RegisterHandler("bootLogo.disable", (request, _) => HandleDisableAsync());
    }

    private static async Task<BridgeResult> HandleGetStatusAsync()
    {
        try
        {
            if (!await BootLogo.IsSupportedAsync().ConfigureAwait(false))
                return BridgeResult.Ok(new { supported = false });

            var (enabled, resolution, formats, filters) = BootLogo.GetStatus();
            return BridgeResult.Ok(new
            {
                supported = true,
                enabled,
                resolution = new { DisplayName = resolution.DisplayName },
                formats = formats.Select(f => f.ToString().ToUpperInvariant()).ToArray(),
                filters,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleEnableAsync(BridgeRequest request)
    {
        try
        {
            if (request.Parameters.ValueKind != JsonValueKind.Object ||
                !request.Parameters.TryGetProperty("filePath", out var pathProp) ||
                pathProp.ValueKind != JsonValueKind.String)
            {
                return BridgeResult.Error(-32602, "Missing string parameter 'filePath'.");
            }

            var filePath = pathProp.GetString();
            if (string.IsNullOrWhiteSpace(filePath))
                return BridgeResult.Error(-32602, "Missing string parameter 'filePath'.");

            if (!File.Exists(filePath))
                return BridgeResult.Error(-32602, "The selected boot-logo file does not exist.");

            if (!await BootLogo.IsSupportedAsync().ConfigureAwait(false))
                return BridgeResult.Error(BridgeErrorCodes.FeatureNotSupported, "Boot logo is not supported on this device.");

            await BootLogo.EnableAsync(filePath).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleDisableAsync()
    {
        try
        {
            if (!await BootLogo.IsSupportedAsync().ConfigureAwait(false))
                return BridgeResult.Error(BridgeErrorCodes.FeatureNotSupported, "Boot logo is not supported on this device.");

            await BootLogo.DisableAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
