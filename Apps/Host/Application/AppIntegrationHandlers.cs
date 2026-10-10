using System;
using System.Text.Json;
using System.Threading;
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
/// OSD state changes to the client and exposes update check/status.
/// </summary>
public static class AppIntegrationHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        ArgumentNullException.ThrowIfNull(rpc);
        var registration = new IntegrationRegistration(rpc,
            IoCContainer.Resolve<UpdateChecker>(), IoCContainer.Resolve<IAppNotificationService>());
        try
        {
            registration.Register();
            rpc.RegisterLifetimeResource(registration);
        }
        catch
        {
            registration.Dispose();
            throw;
        }
    }

    private sealed class IntegrationRegistration(
        BridgeRpcServer rpc, UpdateChecker updateChecker, IAppNotificationService notifications) : IDisposable
    {
        private readonly object _osdSubscriber = new();
        private IDisposable? _windowRegistration;
        private int _disposed;

        public void Register()
        {
            // Keep one checker per registration so status reflects this server's last check.
            rpc.RegisterHandler("app.update.check", (request, _) => HandleUpdateCheckAsync(request));
            rpc.RegisterHandler("app.update.status", (_, _) => HandleUpdateStatusAsync());
            notifications.Changed += OnNotificationChanged;
            MessagingCenter.Subscribe<OsdChangedMessage>(_osdSubscriber, message =>
                rpc.Publish("osd.changed", new { state = message.State.ToString() }));
            _windowRegistration = AutomationWindowVisibility.Register(action =>
                rpc.Publish(AutomationWindowVisibility.HostEventName, new { action = action.ToString() }));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                notifications.Changed -= OnNotificationChanged;
            }
            finally
            {
                try
                {
                    MessagingCenter.Unsubscribe<OsdChangedMessage>(_osdSubscriber);
                }
                finally
                {
                    _windowRegistration?.Dispose();
                }
            }
        }

        private async Task<BridgeResult> HandleUpdateCheckAsync(BridgeRequest request)
        {
            try
            {
                var version = await updateChecker.CheckAsync(ReadForce(request)).ConfigureAwait(false);
                return BridgeResult.Ok(new
                {
                    available = version is not null,
                    version = version?.ToString(),
                    error = updateChecker.Disable ? updateChecker.DisableReason : null,
                });
            }
            catch (Exception error)
            {
                return BridgeResult.Error(-32603, $"{error.GetType().Name}: {error.Message}");
            }
        }

        private async Task<BridgeResult> HandleUpdateStatusAsync()
        {
            try
            {
                await Task.CompletedTask;
                return BridgeResult.Ok(new
                {
                    status = updateChecker.Status.ToString(),
                    disable = updateChecker.Disable,
                });
            }
            catch (Exception error)
            {
                return BridgeResult.Error(-32603, $"{error.GetType().Name}: {error.Message}");
            }
        }

        private void OnNotificationChanged(object? sender, AppNotificationChangedEventArgs args)
        {
            try
            {
                var notification = args.Notification;
                rpc.Publish("notifications.changed", new
                {
                    title = notification.Title,
                    message = notification.Message,
                    severity = notification.Severity.ToString(),
                    isPersistent = notification.IsPersistent,
                    progressPercent = notification.ProgressPercent,
                });
            }
            catch (Exception error)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to forward notification event: {error.Message}", error);
            }
        }
    }

    private static bool ReadForce(BridgeRequest request) =>
        request.Parameters.ValueKind == JsonValueKind.Object
        && request.Parameters.TryGetProperty("force", out var property)
        && property.ValueKind == JsonValueKind.True;
}
