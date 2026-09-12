using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using UniversalDeviceToolkit.Lib.Notifications;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public class NotificationAutomationStep : IAutomationStep
{
    private readonly IAppNotificationService? _notifications;

    [JsonConstructor]
    public NotificationAutomationStep(string? text)
    {
        Text = text;
    }

    public NotificationAutomationStep(string? text, IAppNotificationService notifications)
        : this(text)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        _notifications = notifications;
    }

    public string? Text { get; }

    public Task<bool> IsSupportedAsync() => Task.FromResult(ResolveNotifications() is not null);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return Task.CompletedTask;

        var text = Text.Replace("$RUN_OUTPUT$", context.LastRunOutput ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        var notifications = ResolveNotifications()
            ?? throw new InvalidOperationException("IAppNotificationService is not available; notification step cannot run.");

        notifications.ShowInfo(text);
        return Task.CompletedTask;
    }

    IAutomationStep IAutomationStep.DeepCopy() =>
        _notifications is null
            ? new NotificationAutomationStep(Text)
            : new NotificationAutomationStep(Text, _notifications);

    private IAppNotificationService? ResolveNotifications() =>
        _notifications ?? IoCContainer.TryResolve<IAppNotificationService>();
}
