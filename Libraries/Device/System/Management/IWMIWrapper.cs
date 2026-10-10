using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.System.Management;

/// <summary>
/// Abstraction interface for WMI (Windows Management Instrumentation) operations.
/// Enables testability by allowing mock implementations.
/// </summary>
public interface IWMIWrapper : IDisposable
{
    /// <summary>
    /// Execute a WMI query and return the result as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the result to. Must have a parameterless constructor.</typeparam>
    /// <param name="query">The WMI query to execute (e.g., "SELECT * FROM Win32_Battery").</param>
    /// <returns>The query result, or default value if no result or error.</returns>
    Task<T?> QueryAsync<T>(string query) where T : new();

    /// <summary>
    /// Subscribe to WMI event notifications (synchronous).
    /// Blocks the caller while starting the watcher — prefer <see cref="SubscribeAsync"/> from UI/async code.
    /// </summary>
    /// <param name="query">The WMI event query (e.g., "SELECT * FROM __InstanceModificationEvent WITHIN 5 WHERE TargetInstance ISA 'Win32_Battery'").</param>
    /// <param name="callback">The callback to invoke when an event occurs.</param>
    /// <returns>A disposable subscription that can be used to unsubscribe.</returns>
    IDisposable Subscribe(string query, Action<object> callback);

    /// <summary>
    /// Subscribe to WMI event notifications asynchronously without blocking the caller.
    /// Preferred for UI and other async call sites.
    /// </summary>
    Task<IDisposable> SubscribeAsync(string query, Action<object> callback);

    /// <summary>
    /// Check if WMI is available on the current system.
    /// </summary>
    /// <returns>True if WMI is available, false otherwise.</returns>
    Task<bool> IsAvailableAsync();
}
