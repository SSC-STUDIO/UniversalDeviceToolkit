using System;

namespace UniversalDeviceToolkit.Lib.Notifications;

public enum AppNotificationSeverity
{
    Success = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Immutable request published by business code. UI host owns presentation only.
/// </summary>
public sealed class AppNotificationRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string? Message { get; init; }
    public AppNotificationSeverity Severity { get; init; } = AppNotificationSeverity.Info;
    /// <summary>Auto-close duration. Null + non-persistent uses host defaults (Success=5s).</summary>
    public TimeSpan? Duration { get; init; }
    public bool IsPersistent { get; init; }
    /// <summary>When set, short-window duplicates merge into a count badge instead of a storm.</summary>
    public string? MergeKey { get; init; }
    /// <summary>Determinate progress (0-100) shown as a bar inside the toast; null hides the bar.</summary>
    public double? ProgressPercent { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AppNotificationChangedEventArgs : EventArgs
{
    public required AppNotificationRequest Notification { get; init; }
    public bool IsDismiss { get; init; }
    public int MergeCount { get; init; } = 1;
}
