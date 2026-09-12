using System;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Tracks whether any Electron UI surface is visible. Sensor producers pause
/// while the app is tray-only so LibreHardwareMonitor / vendor WMI are not
/// polled into unused renderers. When no hardware-sensor automation needs
/// cached snapshots, Host also closes the LHM Computer graph.
/// </summary>
internal static class HostUiActivity
{
    private static readonly object Gate = new();
    private static bool _active = true;

    public static event Action<bool>? Changed;

    public static bool IsActive
    {
        get
        {
            lock (Gate)
                return _active;
        }
    }

    public static void SetActive(bool active)
    {
        bool changed;
        lock (Gate)
        {
            changed = _active != active;
            _active = active;
        }

        if (changed)
        {
            Changed?.Invoke(active);
            if (!active)
            {
                TrimMemory();
            }
        }
    }

    public static void TrimMemory()
    {
        try
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
#if WINDOWS
            if (OperatingSystem.IsWindows())
            {
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                proc.MinWorkingSet = (nint)(-1);
                proc.MaxWorkingSet = (nint)(-1);
            }
#endif
        }
        catch
        {
            // Best effort working-set trim.
        }
    }

    internal static void ResetForTests()
    {
        lock (Gate)
            _active = true;
        Changed = null;
    }
}
