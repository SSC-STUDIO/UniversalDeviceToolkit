using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Bridges the WPF SettingsApplicationBehaviorControl software disabler cards
/// (Lenovo Vantage / Legion Zone / Lenovo Hotkeys Fn keys): status probe with
/// the supported-Legion-machine flag and enable/disable toggling.
/// </summary>
public static class SoftwareDisablerHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("software.getStatus", (request, _) => HandleGetStatusAsync(request));
        rpc.RegisterHandler("software.setEnabled", (request, _) => HandleSetEnabledAsync(request));
    }

    private static AbstractSoftwareDisabler? GetDisabler(string app) => app switch
    {
        "vantage" => IoCContainer.Resolve<VantageDisabler>(),
        "legionZone" => IoCContainer.Resolve<LegionZoneDisabler>(),
        "fnKeys" => IoCContainer.Resolve<FnKeysDisabler>(),
        _ => null,
    };

    private static async Task<BridgeResult> HandleGetStatusAsync(BridgeRequest request)
    {
        try
        {
            var app = ReadApp(request);
            var disabler = GetDisabler(app);
            if (disabler is null)
                return BridgeResult.Error(-32602, $"Unknown software app '{app}'.");

            var status = await disabler.GetStatusAsync().ConfigureAwait(false);
            var machineInformation = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

            return BridgeResult.Ok(new
            {
                status = status.ToString(),
                isLegionMachine = Compatibility.IsSupportedLegionMachine(machineInformation),
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
            var app = ReadApp(request);
            var disabler = GetDisabler(app);
            if (disabler is null)
                return BridgeResult.Error(-32602, $"Unknown software app '{app}'.");

            if (!request.Parameters.TryGetProperty("enabled", out var enabledProp) ||
                enabledProp.ValueKind != JsonValueKind.True && enabledProp.ValueKind != JsonValueKind.False)
                return BridgeResult.Error(-32602, "Missing boolean parameter 'enabled'.");

            var enabled = enabledProp.GetBoolean();
            if (enabled)
                await disabler.EnableAsync().ConfigureAwait(false);
            else
                await disabler.DisableAsync().ConfigureAwait(false);

            var status = await disabler.GetStatusAsync().ConfigureAwait(false);
            if (enabled && status != SoftwareStatus.Enabled)
            {
                return BridgeResult.Error(
                    -32603,
                    $"Failed to enable '{app}'; current status is {status}.");
            }

            if (!enabled && status == SoftwareStatus.Enabled)
            {
                return BridgeResult.Error(
                    -32603,
                    $"Failed to disable '{app}'; current status is {status}.");
            }

            return BridgeResult.Ok(new { ok = true, status = status.ToString() });
        }
        catch (SoftwareDisablerException ex) when (IsPrivilegeFailure(ex))
        {
            return BridgeResult.Error(
                BridgeErrorCodes.ElevationRequired,
                $"software.setEnabled requires elevation. {ex.Message}");
        }
        catch (SoftwareDisablerException ex)
        {
            return BridgeResult.Error(-32603, ex.Message);
        }
        catch (Exception ex) when (IsPrivilegeFailure(ex))
        {
            return BridgeResult.Error(
                BridgeErrorCodes.ElevationRequired,
                $"software.setEnabled requires elevation. {ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsPrivilegeFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException)
                return true;

            if (current is Win32Exception win32 && win32.NativeErrorCode is 5 or 1314)
                return true;

            if (current is COMException com)
                return com.ErrorCode is unchecked((int)0x80070005) or unchecked((int)0x80070522);

            // OpenSCManager / OpenService with SC_MANAGER_ALL_ACCESS fail as ExternalException
            // when the bridge host is not elevated.
            if (current is ExternalException)
                return true;
        }

        return false;
    }

    private static string ReadApp(BridgeRequest request)
    {
        if (!request.Parameters.TryGetProperty("app", out var appProp) ||
            appProp.ValueKind != JsonValueKind.String)
            throw new BridgeErrorException(-32602, "Missing string parameter 'app'.");
        return appProp.GetString()!;
    }
}
