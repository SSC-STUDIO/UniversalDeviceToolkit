using System;

namespace UniversalDeviceToolkit.Lib.Notifications;

/// <summary>
/// Application-level toast notifications. Business layers publish events only —
/// they must not touch MainWindow, Snackbar, or visual controls.
/// </summary>
public interface IAppNotificationService
{
    event EventHandler<AppNotificationChangedEventArgs>? Changed;

    Guid Show(AppNotificationRequest request);

    void Dismiss(Guid id);

    /// <summary>Updates progress (and optionally the message) of an existing toast in place.</summary>
    void UpdateProgress(Guid id, double percent, string? message = null);

    Guid ShowSuccess(string title, string? message = null, string? mergeKey = null);

    Guid ShowInfo(string title, string? message = null, string? mergeKey = null);

    Guid ShowWarning(string title, string? message = null, string? mergeKey = null);

    Guid ShowError(string title, string? message = null, string? mergeKey = null);
}
