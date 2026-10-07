using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Network;

internal enum NetworkProcessState
{
    Exited,
    Current,
    Alive,
    Unknown
}

internal static class NetworkProcessOwnership
{
    internal static NetworkProcessIdentity Capture(Process process, bool includeExecutablePath = false) => new()
    {
        ProcessId = process.Id,
        StartedAtUtc = process.StartTime.ToUniversalTime(),
        ExecutablePath = includeExecutablePath
            ? process.MainModule?.FileName ?? throw new InvalidOperationException("Worker executable path is unavailable.")
            : null
    };

    internal static NetworkProcessState Inspect(NetworkProcessIdentity identity)
    {
        if (identity.ProcessId <= 0 || identity.StartedAtUtc == default)
            return NetworkProcessState.Unknown;

        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            if (process.HasExited || process.StartTime.ToUniversalTime() != identity.StartedAtUtc.UtcDateTime)
                return NetworkProcessState.Exited;
            return process.Id == Environment.ProcessId ? NetworkProcessState.Current : NetworkProcessState.Alive;
        }
        catch (ArgumentException)
        {
            return NetworkProcessState.Exited;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            Log.Instance.TraceOnce("network-process-identity", "Network process identity could not be verified.", ex);
            return NetworkProcessState.Unknown;
        }
    }

    internal static bool HasUnidentifiedWorker()
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(NetworkProxyWorkerLauncher.WorkerFileName));
        try
        {
            foreach (var process in processes)
            {
                if (!process.HasExited)
                    return true;
            }
            return false;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    internal static bool TryStopOrphanedWorker(NetworkProcessIdentity identity)
    {
        if (identity.ProcessId <= 0 || identity.StartedAtUtc == default)
            return false;
        if (Inspect(identity) == NetworkProcessState.Exited)
            return true;
        if (identity.ProcessId == Environment.ProcessId || string.IsNullOrWhiteSpace(identity.ExecutablePath) ||
            !string.Equals(Path.GetFileName(identity.ExecutablePath), NetworkProxyWorkerLauncher.WorkerFileName,
                StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            if (process.HasExited || process.StartTime.ToUniversalTime() != identity.StartedAtUtc.UtcDateTime)
                return true;
            if (!string.Equals(process.MainModule?.FileName, identity.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                return false;
            process.Kill(entireProcessTree: true);
            return process.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            Log.Instance.WarningOnce("network-owned-worker-stop", "Recorded orphaned NetworkProxy worker could not be stopped.", ex);
            return false;
        }
    }
}
