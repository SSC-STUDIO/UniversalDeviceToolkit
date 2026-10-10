using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System;

/// <summary>
/// Applies Windows EcoQoS / BelowNormal to another process (the Electron UI
/// shell). The Host process itself stays at Normal so keyboard hooks and
/// automation stay responsive.
/// </summary>
public static class ProcessScheduling
{
    /// <summary>
    /// When <paramref name="background"/> is true, enable execution-speed
    /// throttling (EcoQoS) and BelowNormal priority. When false, restore
    /// Quality QoS and Normal priority.
    /// </summary>
    public static bool TrySetBackgroundEfficiency(int processId, bool background)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
            return false;

        try
        {
            Apply(processId, background);
            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"ProcessScheduling failed for pid {processId}: {ex.Message}", ex);
            return false;
        }
    }

    private static unsafe void Apply(int processId, bool background)
    {
        var access = PROCESS_ACCESS_RIGHTS.PROCESS_SET_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION;
        var handle = PInvoke.OpenProcess(access, false, (uint)processId);
        if (handle.IsNull)
            throw new InvalidOperationException($"OpenProcess failed for pid {processId}.");

        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = background ? PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0u
            };

            if (!PInvoke.SetProcessInformation(
                    handle,
                    PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                    &state,
                    (uint)sizeof(PROCESS_POWER_THROTTLING_STATE)))
            {
                // EcoQoS is Windows 10 1709+; older builds still get priority below.
            }

            var priority = background
                ? PROCESS_CREATION_FLAGS.BELOW_NORMAL_PRIORITY_CLASS
                : PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS;
            _ = PInvoke.SetPriorityClass(handle, priority);

            if (background)
            {
                try
                {
                    using var targetProc = global::System.Diagnostics.Process.GetProcessById(processId);
                    targetProc.MinWorkingSet = (nint)(-1);
                    targetProc.MaxWorkingSet = (nint)(-1);
                }
                catch
                {
                    // Target process may have exited or permissions restricted.
                }
            }
        }
        finally
        {
            _ = PInvoke.CloseHandle(handle);
        }
    }
}
