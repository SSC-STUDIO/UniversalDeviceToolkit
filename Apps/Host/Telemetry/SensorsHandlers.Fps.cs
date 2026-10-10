using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

public static partial class SensorsHandlers
{
    private static readonly FpsSubscriptionManager FpsSubscribers = new(() => HostUiActivity.IsActive);
    private static FpsSensorController? _subscribedFpsController;
    private static volatile BridgeRpcServer? _fpsRpc;

    private static async Task SynchronizeFpsActivityAsync()
    {
        try
        {
            await FpsSubscribers.SynchronizeActivityAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"background FPS synchronization failed: {ex.Message}", ex);
        }
    }

    private static async Task StartSubscribedFpsAsync(BridgeRpcServer rpc, string[]? blacklist)
    {
        var controller = GetFpsController();
        _subscribedFpsController = controller;
        _fpsRpc = rpc;
        TryApplyFpsBlacklist(controller, blacklist);
        controller.FpsDataUpdated -= OnFpsDataUpdated;
        controller.FpsDataUpdated += OnFpsDataUpdated;
        try
        {
            await controller.StartMonitoringAsync().ConfigureAwait(false);
        }
        catch
        {
            try { StopSubscribedFps(); }
            catch (Exception ex) { Log.Instance.Trace($"FPS startup cleanup failed: {ex.Message}", ex); }
            throw;
        }
    }

    private static void StopSubscribedFps()
    {
        if (_subscribedFpsController is not { } controller) return;
        controller.FpsDataUpdated -= OnFpsDataUpdated;
        // Keep the controller on failure so the serialized next operation can
        // retry its stop or safely restart the same controller.
        controller.StopMonitoring();
        _subscribedFpsController = null;
        _fpsRpc = null;
    }

    private static FpsSensorController GetFpsController()
        => IoCContainer.Resolve<FpsSensorController>();

    // ── snapshot assembly ───────────────────────────────────────────────────

    private static Task<BridgeResult> HandleGetFpsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = GetFpsController().GetCurrentFpsData();
            return Task.FromResult(BridgeResult.Ok(MapFpsData(data)));
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

    private static async Task<BridgeResult> HandleSubscribeFpsAsync(BridgeRequest request, BridgeRpcServer rpc, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!FpsSubscriptionManager.TryReadSubscriberId(request.Parameters, out var subscriberId))
                return BridgeResult.Error(-32602, "subscriberId must be a non-empty string of at most 128 characters without whitespace or control characters.");
            var blacklist = ParseFpsBlacklist(request.Parameters);
            var monitoring = await FpsSubscribers.SubscribeAsync(subscriberId,
                () => StartSubscribedFpsAsync(rpc, blacklist), StopSubscribedFps, cancellationToken).ConfigureAwait(false);
            return BridgeResult.Ok(new { monitoring });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleUnsubscribeFpsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!FpsSubscriptionManager.TryReadSubscriberId(request.Parameters, out var subscriberId))
                return BridgeResult.Error(-32602, "subscriberId must be a non-empty string of at most 128 characters without whitespace or control characters.");
            var monitoring = await FpsSubscribers.UnsubscribeAsync(subscriberId, cancellationToken).ConfigureAwait(false);
            return BridgeResult.Ok(new { monitoring });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void OnFpsDataUpdated(object? sender, FpsSensorController.FpsData data)
    {
        var rpc = _fpsRpc;
        if (!HostUiActivity.IsActive || rpc is null)
            return;

        try
        {
            rpc.Publish("sensors.fpsUpdated", MapFpsData(data));
        }
        catch (Exception ex)
        {
            if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"sensors.fpsUpdated publish failed: {ex.Message}", ex);
        }
    }

    private static object MapFpsData(FpsSensorController.FpsData data)
    {
        return new
        {
            process = (string?)null,
            fps = ParseFps(data.Fps),
            lowFps = ParseFps(data.LowFps),
            frameTimeMs = ParseFps(data.FrameTime),
        };
    }

    private static double? ParseFps(string value)
        => double.TryParse(value, out var parsed) && parsed >= 0 ? parsed : null;

    // ── helpers ─────────────────────────────────────────────────────────────

    internal static string[]? ParseFpsBlacklist(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("blacklist", out var blacklistProp) || blacklistProp.ValueKind != JsonValueKind.Array)
            return null;

        var entries = blacklistProp.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return entries.Length > 0 ? entries : null;
    }

    /// <summary>
    /// Applies the subscription-time blacklist to the FPS controller. Lib's
    /// FpsSensorController only exposes the blacklist read-only (Blacklist),
    /// so the backing field is written directly; if its shape ever changes the
    /// application is skipped and monitoring keeps the previous behavior.
    /// </summary>

    private static void TryApplyFpsBlacklist(FpsSensorController controller, string[]? blacklist)
    {
        if (blacklist is null)
            return;

        try
        {
            var field = typeof(FpsSensorController).GetField("_blacklist", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field is null || field.FieldType != typeof(List<string>))
                return;

            field.SetValue(controller, new List<string>(blacklist));
        }
        catch (Exception ex)
        {
            if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"Failed to apply FPS blacklist: {ex.Message}", ex);
        }
    }
}
