namespace UniversalDeviceToolkit.Abstractions.Lifecycle;

/// <summary>
/// Manages the application's registration as a startup/autorun program.
/// </summary>
public interface IAutorunManager
{
    /// <summary>
    /// Checks whether the application is currently registered to start at login.
    /// </summary>
    /// <returns><see langword="true"/> if autorun is enabled; otherwise <see langword="false"/>.</returns>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// Registers the application to start automatically at login.
    /// </summary>
    Task EnableAsync();

    /// <summary>
    /// Removes the application from the startup/autorun list.
    /// </summary>
    Task DisableAsync();
}
