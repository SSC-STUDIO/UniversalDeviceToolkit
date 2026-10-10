using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Shared startup helpers for the headless host (WPF counterpart lives in App).
/// </summary>
public static class AppHostHelpers
{
    public static async Task RunInitStepAsync(Func<Task> action, string operationName, bool logOnSuccess = true)
    {
        try
        {
            if (Log.Instance.IsTraceEnabled && logOnSuccess)
                Log.Instance.Trace($"Initializing {operationName}...");

            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Couldn't initialize {operationName}.", ex);
        }
    }
}
