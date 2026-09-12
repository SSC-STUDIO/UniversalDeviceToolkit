using System;
using System.Management;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Management;

/// <summary>
/// Concrete implementation of IWMIWrapper that delegates to the static WMI class.
/// </summary>
public class WMIWrapper : IWMIWrapper
{
    private bool _disposed = false;

    public async Task<T?> QueryAsync<T>(string query) where T : new()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WMIWrapper));

        try
        {
            // Use the existing WMI infrastructure
            // This is a simplified implementation - in practice, you'd parse the query
            // and call the appropriate WMI methods
            var scope = "root\\cimv2";
            using var mos = new ManagementObjectSearcher(scope, query);
            var managementObjects = await mos.GetAsyncWithTimeout().ConfigureAwait(false);
            try
            {
                foreach (var mo in managementObjects)
                {
                    // Convert the first result to type T
                    // This is a placeholder - real implementation would need proper conversion
                    if (mo is ManagementObject managementObject)
                    {
                        return ConvertManagementObject<T>(managementObject);
                    }
                }

                return default;
            }
            finally
            {
                managementObjects.DisposeAll();
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI query failed: {query}", ex);

            return default;
        }
    }

    /// <summary>
    /// Sync subscribe. Blocks the calling thread while the watcher starts
    /// (<see cref="ManagementEventWatcherExtensions.StartWithTimeout"/>). Prefer
    /// <see cref="SubscribeAsync"/> from UI / async code.
    /// </summary>
    public IDisposable Subscribe(string query, Action<object> callback)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WMIWrapper));

        try
        {
            var (watcher, disposable) = CreateSubscription(query, callback);

            // Blocks caller intentionally for the sync API. Start runs on the thread pool
            // via Task.Run inside StartWithTimeout — still do not call from the UI thread.
            watcher.StartWithTimeout();

            return disposable;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI subscribe failed: {query}", ex);

            throw;
        }
    }

    /// <summary>
    /// Async subscribe. Starts the watcher with <see cref="ManagementEventWatcherExtensions.StartAsyncWithTimeout"/>
    /// and does not block the calling thread.
    /// </summary>
    public async Task<IDisposable> SubscribeAsync(string query, Action<object> callback)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WMIWrapper));

        try
        {
            var (watcher, disposable) = CreateSubscription(query, callback);

            await watcher.StartAsyncWithTimeout().ConfigureAwait(false);

            return disposable;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI subscribe async failed: {query}", ex);

            throw;
        }
    }

    public async Task<bool> IsAvailableAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WMIWrapper));

        try
        {
            // Try to create a simple WMI query to test availability
            var scope = "root\\cimv2";
            using var mos = new ManagementObjectSearcher(scope, "SELECT * FROM Win32_OperatingSystem");
            var results = await mos.GetAsyncWithTimeout().ConfigureAwait(false);
            try
            {
                return true;
            }
            finally
            {
                results.DisposeAll();
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("WMI availability check failed", ex);
            return false;
        }
    }

    private (ManagementEventWatcher Watcher, IDisposable Disposable) CreateSubscription(string query, Action<object> callback)
    {
        var scope = "root\\cimv2";
        var watcher = new ManagementEventWatcher(scope, query);

        watcher.EventArrived += (_, e) =>
        {
            try
            {
                callback(e.NewEvent);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"WMI event callback failed", ex);
            }
        };

        IDisposable disposable = new LambdaDisposable(() =>
        {
            try
            {
                watcher.Stop();
            }
            catch (ManagementException ex)
            {
                Log.Instance.TraceOnce("wmi-wrapper-watcher-stop", "WMIWrapper event watcher Stop failed during dispose.", ex);
            }
            finally
            {
                watcher.Dispose();
            }
        });

        return (watcher, disposable);
    }

    private static T ConvertManagementObject<T>(ManagementObject mo) where T : new()
    {
        // This is a simplified conversion - a real implementation would need to
        // map ManagementObject properties to the target type's properties
        var result = new T();
        var type = typeof(T);
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            try
            {
                if (mo.Properties[prop.Name] is { } managementProp && managementProp.Value != null)
                {
                    var value = Convert.ChangeType(managementProp.Value, prop.PropertyType);
                    prop.SetValue(result, value);
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to convert WMI property '{prop.Name}' for {typeof(T).Name}", ex);
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }
}
