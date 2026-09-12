using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers;

public interface IGPUProcessManager
{
    Task KillGPUProcessesAsync(IEnumerable<Process> processes);
}

public class GPUProcessManager : IGPUProcessManager
{
    private static readonly TimeSpan GracefulShutdownTimeout = TimeSpan.FromSeconds(5);

    public async Task KillGPUProcessesAsync(IEnumerable<Process> processes)
    {
        if (processes is null)
            return;

        foreach (var process in processes)
        {
            var processId = 0;
            var processName = "";
            try { processId = process.Id; processName = process.ProcessName; }
            catch
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Failed to access process properties");
            }

            try
            {
                if (!process.HasExited)
                {
                    // Step 1: Attempt graceful shutdown via WM_CLOSE.
                    // CloseMainWindow returns false when the process has no
                    // main window (console apps, background services, etc.).
                    // In that case there is nothing to wait for — skip the
                    // graceful-timeout and force-kill immediately to avoid
                    // wasting up to GracefulShutdownTimeout per headless process.
                    var closePosted = process.CloseMainWindow();

                    if (closePosted)
                    {
                        // Step 2: Wait for graceful exit (synchronous wait is
                        // acceptable here since this runs on a background thread,
                        // not the UI thread)
                        if (process.WaitForExit((int)GracefulShutdownTimeout.TotalMilliseconds))
                            continue;
                    }

                    // Step 3: Force kill the process tree if graceful shutdown
                    // timed out or could not be attempted (no main window)
                    process.Kill(true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Couldn't kill process. [pid={processId}, name={processName}]", ex);
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't dispose process. [pid={processId}, name={processName}]", ex);
                }
            }
        }
    }
}
