namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Abstraction for marshalling work onto the platform UI thread.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Executes the specified action on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    Task RunOnUIThreadAsync(Action action);

    /// <summary>
    /// Gets a value indicating whether the caller is currently on the UI thread.
    /// </summary>
    bool IsUIThread { get; }
}
