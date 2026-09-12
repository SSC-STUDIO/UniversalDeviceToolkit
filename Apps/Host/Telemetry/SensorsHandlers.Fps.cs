using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Sensor bridge: LibreHardwareMonitor snapshot + subscription, vendor fallback,
/// FPS monitoring and sensor-related settings.
/// </summary>
public static partial class SensorsHandlers
{

    private static readonly object FpsLock = new();

    private static int _fpsSubscriberCount;

    private static FpsSensorController? _subscribedFpsController;

    private static BridgeRpcServer? _fpsRpc;

    private static void PauseFpsForBackground()
    {
        lock (FpsLock)
        {
            if (_subscribedFpsController is { } controller)
            {
                controller.FpsDataUpdated -= OnFpsDataUpdated;
                controller.StopMonitoring();
            }
        }
    }

    private static void ResumeFpsAfterBackground()
    {
        FpsSensorController? controller;
        lock (FpsLock)
        {
            if (_fpsSubscriberCount <= 0 || _subscribedFpsController is null)
                return;
            controller = _subscribedFpsController;
            controller.FpsDataUpdated -= OnFpsDataUpdated;
            controller.FpsDataUpdated += OnFpsDataUpdated;
        }

        _ = controller.StartMonitoringAsync();
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
            var controller = GetFpsController();
            var blacklist = ParseFpsBlacklist(request.Parameters);
            var shouldStart = false;

            lock (FpsLock)
            {
                if (_fpsSubscriberCount == 0)
                {
                    _subscribedFpsController = controller;
                    _fpsRpc = rpc;
                    TryApplyFpsBlacklist(controller, blacklist);
                    controller.FpsDataUpdated -= OnFpsDataUpdated;
                    controller.FpsDataUpdated += OnFpsDataUpdated;
                    shouldStart = true;
                }
            }

            if (shouldStart)
            {
                try
                {
                    await controller.StartMonitoringAsync().ConfigureAwait(false);
                }
                catch
                {
                    lock (FpsLock)
                    {
                        controller.FpsDataUpdated -= OnFpsDataUpdated;
                        _subscribedFpsController = null;
                        _fpsRpc = null;
                    }

                    throw;
                }
            }

            lock (FpsLock)
                _fpsSubscriberCount++;

            return BridgeResult.Ok(new { monitoring = true });
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

    private static Task<BridgeResult> HandleUnsubscribeFpsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var monitoring = false;

            lock (FpsLock)
            {
                _fpsSubscriberCount = Math.Max(0, _fpsSubscriberCount - 1);
                monitoring = _fpsSubscriberCount > 0;
                if (!monitoring && _subscribedFpsController is { } controller)
                {
                    controller.FpsDataUpdated -= OnFpsDataUpdated;
                    controller.StopMonitoring();
                    _subscribedFpsController = null;
                    _fpsRpc = null;
                }
            }

            return Task.FromResult(BridgeResult.Ok(new { monitoring }));
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

    private static void OnFpsDataUpdated(object? sender, FpsSensorController.FpsData data)
    {
        if (!HostUiActivity.IsActive || _fpsRpc is null)
            return;

        try
        {
            _fpsRpc.Publish("sensors.fpsUpdated", MapFpsData(data));
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

    private static string[]? ParseFpsBlacklist(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("blacklist", out var blacklistProp) || blacklistProp.ValueKind != JsonValueKind.Array)
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
