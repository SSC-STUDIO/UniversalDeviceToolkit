using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class ManagementEventWatcherExtensions
{
    /// <summary>
    /// Starts a WMI event watcher asynchronously with a timeout.
    /// Preferred for UI and other async call sites — never blocks the calling thread.
    /// Uses <see cref="Task.Run"/> so <see cref="ManagementEventWatcher.Start"/> does not
    /// capture the caller's synchronization context (avoids UI-thread deadlocks).
    /// </summary>
    public static async Task StartAsyncWithTimeout(this ManagementEventWatcher watcher, int timeoutMs = 2500)
    {
        var startTask = Task.Run(() => watcher.Start());
        using var cts = new CancellationTokenSource();
        var completed = await Task.WhenAny(startTask, Task.Delay(timeoutMs, cts.Token)).ConfigureAwait(false);
        if (completed != startTask)
        {
            try
            {
                watcher.Dispose();
            }
            catch (ManagementException ex)
            {
                Log.Instance.TraceOnce(
                    "wmi-watcher-dispose-timeout",
                    "WMI event watcher dispose failed after start timeout.",
                    ex);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI event watcher start timed out after {timeoutMs}ms.");

            throw new TimeoutException($"WMI event watcher start timed out after {timeoutMs}ms.");
        }

        cts.Cancel();
        await startTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a WMI event watcher with a timeout, blocking the calling thread until start
    /// completes or times out.
    /// <para>
    /// <see cref="Task.Wait(int)"/> is intentional: this is the sync API surface. Start work
    /// still runs on the thread pool via <see cref="Task.Run"/>, so this is not a classic
    /// sync-context deadlock from <c>GetAwaiter().GetResult()</c>, but it <b>does</b> block the
    /// caller for up to <paramref name="timeoutMs"/>. Do not call from the UI thread — use
    /// <see cref="StartAsyncWithTimeout"/> instead.
    /// </para>
    /// </summary>
    public static void StartWithTimeout(this ManagementEventWatcher watcher, int timeoutMs = 2500)
    {
        // Task.Run: Start() runs off the caller's sync context.
        // Wait: intentional for sync callers on non-UI threads only (see summary).
        var startTask = Task.Run(() => watcher.Start());
        if (!startTask.Wait(timeoutMs))
        {
            try { watcher.Dispose(); }
            catch (ManagementException ex)
            {
                Log.Instance.TraceOnce(
                    "wmi-watcher-dispose-timeout-sync",
                    "WMI event watcher dispose failed after sync start timeout.",
                    ex);
            }
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI event watcher start timed out after {timeoutMs}ms.");
            throw new TimeoutException($"WMI event watcher start timed out after {timeoutMs}ms.");
        }
        if (startTask.IsFaulted)
        {
            var ex = startTask.Exception?.InnerException ?? (Exception?)startTask.Exception
                ?? new InvalidOperationException("WMI event watcher start failed.");
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }
}
