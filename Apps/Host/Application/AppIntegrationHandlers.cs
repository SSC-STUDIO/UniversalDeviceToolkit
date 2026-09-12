using System;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Notifications;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Host-side notification/OSD/update integration: forwards app notifications and
/// OSD state changes to the Electron client and exposes update check/status.
/// </summary>
public static class AppIntegrationHandlers
{
    // Cached instance so app.update.status reflects the last check performed here
    // (UpdateChecker is registered as instance-per-dependency in the IoC container).
    private static readonly UpdateChecker _updateChecker = IoCContainer.Resolve<UpdateChecker>();

    private sealed class IntegrationSubscriber
    {
        public static readonly IntegrationSubscriber Instance = new();
    }

    private static readonly IntegrationSubscriber _osdSubscriber = IntegrationSubscriber.Instance;

    private static BridgeRpcServer? _rpc;

    public static void Register(BridgeRpcServer rpc)
    {
        _rpc = rpc;

        rpc.RegisterHandler("app.update.check", (request, _) => HandleUpdateCheckAsync(request));
        rpc.RegisterHandler("app.update.status", (request, _) => HandleUpdateStatusAsync(request));

        var notifications = IoCContainer.Resolve<IAppNotificationService>();
        notifications.Changed += OnNotificationChanged;

        // MessagingCenter.Publish is synchronous — keep the handler to a single non-blocking Publish.
        MessagingCenter.Subscribe<OsdChangedMessage>(_osdSubscriber, msg =>
            _rpc?.Publish("osd.changed", new { state = msg.State.ToString() }));

        AutomationWindowVisibility.Register(action =>
        {
            var server = _rpc ?? throw new InvalidOperationException("Host RPC is not available for window visibility.");
            server.Publish(AutomationWindowVisibility.HostEventName, new { action = action.ToString() });
        });
    }

    // ── update handlers ─────────────────────────────────────────────────────

    private static async Task<BridgeResult> HandleUpdateCheckAsync(BridgeRequest request)
    {
        try
        {
            var force = ReadForce(request);
            var version = await _updateChecker.CheckAsync(force).ConfigureAwait(false);

            return BridgeResult.Ok(new
            {
                available = version is not null,
                version = version?.ToString(),
                error = _updateChecker.Disable ? _updateChecker.DisableReason : null,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleUpdateStatusAsync(BridgeRequest request)
    {
        try
        {
            await Task.CompletedTask;
            return BridgeResult.Ok(new
            {
                status = _updateChecker.Status.ToString(),
                disable = _updateChecker.Disable,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── event forwarding ────────────────────────────────────────────────────

    private static void OnNotificationChanged(object? sender, AppNotificationChangedEventArgs args)
    {
        try
        {
            var notification = args.Notification;
            _rpc?.Publish("notifications.changed", new
            {
                title = notification.Title,
                message = notification.Message,
                severity = notification.Severity.ToString(),
                isPersistent = notification.IsPersistent,
                progressPercent = notification.ProgressPercent,
            });
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to forward notification event: {ex.Message}", ex);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static bool ReadForce(BridgeRequest request)
    {
        return request.Parameters.ValueKind == JsonValueKind.Object
            && request.Parameters.TryGetProperty("force", out var prop)
            && prop.ValueKind == JsonValueKind.True;
    }
}
