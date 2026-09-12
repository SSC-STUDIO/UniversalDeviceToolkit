using System;
using System.Text.Json;
using System.Threading.Tasks;
#if WINDOWS
using UniversalDeviceToolkit.Lib.System;
#endif

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Cross-platform UI activity channel. Electron reports tray / minimize state
/// here so Host sensor loops can pause. On Windows the same call also applies
/// EcoQoS to the Electron process.
/// </summary>
internal static class UiActivityHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("app.setUiActive", (request, _) => HandleSetUiActiveAsync(request));
    }

    internal static Task<BridgeResult> HandleSetUiActiveAsync(BridgeRequest request)
    {
        try
        {
            var active = request.Parameters.ValueKind == JsonValueKind.Object
                && request.Parameters.TryGetProperty("active", out var activeProp)
                && activeProp.ValueKind == JsonValueKind.True;

            var pid = 0;
            if (request.Parameters.ValueKind == JsonValueKind.Object
                && request.Parameters.TryGetProperty("pid", out var pidProp)
                && pidProp.ValueKind == JsonValueKind.Number
                && pidProp.TryGetInt32(out var parsedPid))
            {
                pid = parsedPid;
            }

            HostUiActivity.SetActive(active);

            var applied = false;
#if WINDOWS
            // Only the Electron UI process is throttled. The Host stays at
            // Normal so WH_KEYBOARD_LL / automation keep their latency.
            applied = pid > 0 && ProcessScheduling.TrySetBackgroundEfficiency(pid, background: !active);
#else
            _ = pid;
#endif
            return Task.FromResult(BridgeResult.Ok(new { ok = true, applied, active }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }
}
