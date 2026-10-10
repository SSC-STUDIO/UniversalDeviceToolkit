namespace UniversalDeviceToolkit.Abstractions.Notifications;

/// <summary>
/// Defines the type/severity of a user-facing notification.
/// </summary>
public enum NotificationType
{
    /// <summary>Operation completed successfully.</summary>
    Success,

    /// <summary>Non-critical warning that the user should be aware of.</summary>
    Warning,

    /// <summary>An error occurred that requires user attention.</summary>
    Error,

    /// <summary>General informational message.</summary>
    Information
}

/// <summary>
/// Platform-agnostic abstraction for displaying user-facing notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a notification to the user.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">An optional detail message.</param>
    /// <param name="type">The notification severity type.</param>
    Task ShowAsync(string title, string? message = null, NotificationType type = NotificationType.Information);
}
