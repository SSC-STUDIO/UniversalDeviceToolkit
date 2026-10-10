using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features.CursorPointer;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Cursor &amp; pointer bridge (absorbed custom-mouse plugin): pointer speed and
/// button swap via Win32 SPI, plus UDT cursor schemes with light/dark auto follow.
/// Runtime start mirrors the former IAppStartupPlugin.OnAppStarted hook.
/// </summary>
public static class MouseHandlers
{
    private static CursorPointerService Service => IoCContainer.Resolve<CursorPointerService>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("mouse.getState", (request, _) => HandleGetStateAsync(request));
        rpc.RegisterHandler("mouse.applyWindows", (request, _) => HandleApplyWindowsAsync(request));
        rpc.RegisterHandler("mouse.setCursorThemeMode", (request, _) => HandleSetCursorThemeModeAsync(request));
        rpc.RegisterHandler("mouse.applyCursorThemeNow", (_, _) => HandleApplyCursorThemeNowAsync());
        rpc.RegisterHandler("mouse.syncFromWindows", (_, _) => HandleSyncFromWindowsAsync());
        rpc.RegisterHandler("mouse.restoreWindowsDefault", (_, _) => HandleRestoreWindowsDefaultAsync());

        // Former OnAppStarted: auto-theme following wakes with the host, even if
        // the UI page is never opened. Failures stay inside StartRuntime.
        Service.StartRuntime();
    }

    private static Task<BridgeResult> HandleGetStateAsync(BridgeRequest request)
    {
        _ = request;
        return Task.FromResult(BridgeResult.Ok(ToPayload(Service.GetState())));
    }

    private static Task<BridgeResult> HandleApplyWindowsAsync(BridgeRequest request) =>
        RunAsync(async () =>
        {
            var speed = GetRequiredInt32(request, "speed");
            var swapButtons = GetRequiredBoolean(request, "swapButtons");
            var ok = await Service.ApplyWindowsAsync(speed, swapButtons).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok });
        });

    private static Task<BridgeResult> HandleSetCursorThemeModeAsync(BridgeRequest request) =>
        RunAsync(async () =>
        {
            var mode = GetRequiredInt32(request, "mode");
            if (!Enum.IsDefined(typeof(CursorThemeMode), mode))
                throw new BridgeErrorException(-32602, $"Unknown cursor theme mode '{mode}'.");

            var ok = await Service.SetCursorThemeModeAsync((CursorThemeMode)mode).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok });
        });

    private static Task<BridgeResult> HandleApplyCursorThemeNowAsync() =>
        RunAsync(async () =>
        {
            var ok = await Service.ApplyCursorStyleForCurrentThemeAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { ok });
        });

    private static Task<BridgeResult> HandleSyncFromWindowsAsync() =>
        RunAsync(async () =>
        {
            await Service.SyncFromWindowsAsync().ConfigureAwait(false);
            return BridgeResult.Ok(ToPayload(Service.GetState()));
        });

    private static Task<BridgeResult> HandleRestoreWindowsDefaultAsync() =>
        RunAsync(async () =>
        {
            var ok = await Service.RestoreWindowsDefaultCursorThemeAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { ok });
        });

    private static object ToPayload(CursorPointerState state) => new
    {
        pointerSpeed = state.PointerSpeed,
        swapButtons = state.SwapButtons,
        cursorThemeMode = (int)state.CursorThemeMode,
        autoThemeCursorStyle = state.AutoThemeCursorStyle,
        lastAppliedTheme = state.LastAppliedTheme,
    };

    private static async Task<BridgeResult> RunAsync(Func<Task<BridgeResult>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
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

    private static int GetRequiredInt32(BridgeRequest request, string name)
    {
        if (request.Parameters.ValueKind != System.Text.Json.JsonValueKind.Object
            || !request.Parameters.TryGetProperty(name, out var property)
            || property.ValueKind != System.Text.Json.JsonValueKind.Number
            || !property.TryGetInt32(out var value))
        {
            throw new BridgeErrorException(-32602, $"Missing integer '{name}' parameter.");
        }

        return value;
    }

    private static bool GetRequiredBoolean(BridgeRequest request, string name)
    {
        if (request.Parameters.ValueKind != System.Text.Json.JsonValueKind.Object
            || !request.Parameters.TryGetProperty(name, out var property)
            || property.ValueKind is not System.Text.Json.JsonValueKind.True and not System.Text.Json.JsonValueKind.False)
        {
            throw new BridgeErrorException(-32602, $"Missing boolean '{name}' parameter.");
        }

        return property.GetBoolean();
    }
}
