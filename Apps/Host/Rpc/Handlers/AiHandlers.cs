using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Legion AI-mode bridge (WPF BalanceModeSettingsWindow toggle): reports whether
/// the machine supports AI mode and flips the persisted AI-engine flag, then
/// restarts AIController so the change takes effect immediately. The controller
/// itself owns the background start/stop logic (power-mode/game conditions).
/// </summary>
public static class AiHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("ai.getStatus", (_, _) => HandleGetStatusAsync());
        rpc.RegisterHandler("ai.setEnabled", (request, _) => HandleSetEnabledAsync(request));
    }

    private static async Task<BridgeResult> HandleGetStatusAsync()
    {
        try
        {
            var controller = IoCContainer.Resolve<AIController>();
            var supported = await IsSupportedAsync().ConfigureAwait(false);

            return BridgeResult.Ok(new
            {
                supported,
                enabled = controller.IsAIModeEnabled,
            });
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
                (enabledProp.ValueKind != JsonValueKind.True && enabledProp.ValueKind != JsonValueKind.False))
                throw new BridgeErrorException(-32602, "Missing or invalid boolean parameter 'enabled'.");

            if (!await IsSupportedAsync().ConfigureAwait(false))
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.FeatureNotSupported,
                    "AI mode is not supported on this device.");
            }

            var controller = IoCContainer.Resolve<AIController>();
            var enabled = enabledProp.GetBoolean();

            if (controller.IsAIModeEnabled != enabled)
            {
                controller.IsAIModeEnabled = enabled;
                if (enabled)
                    await controller.StartIfNeededAsync().ConfigureAwait(false);
                else
                    await controller.StopAsync().ConfigureAwait(false);
            }

            if (controller.IsAIModeEnabled != enabled)
            {
                return BridgeResult.Error(
                    -32603,
                    $"AI mode preference was not persisted (enabled={controller.IsAIModeEnabled}).");
            }

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

    /// <summary>
    /// Mirrors AIController.IsSupportedAsync (private there): a supported Legion
    /// machine whose properties advertise AI mode.
    /// </summary>
    private static async Task<bool> IsSupportedAsync()
    {
        var machineInformation = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return Compatibility.IsSupportedLegionMachine(machineInformation)
            && machineInformation.Properties.SupportsAIMode;
    }
}
