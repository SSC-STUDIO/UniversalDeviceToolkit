using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using UniversalDeviceToolkit.Lib.Network;

namespace UniversalDeviceToolkit.NetworkProxy.Host;

internal static class NetworkProxyOwnerMonitor
{
    internal const string OwnerProcessIdEnvironmentVariable = NetworkProxyWorkerLauncher.OwnerProcessIdEnvironmentVariable;
    internal const string OwnerStartedAtEnvironmentVariable = NetworkProxyWorkerLauncher.OwnerStartedAtEnvironmentVariable;

    internal static NetworkProcessIdentity? ResolveOwnerIdentity()
    {
        var processId = Environment.GetEnvironmentVariable(OwnerProcessIdEnvironmentVariable);
        var startedAt = Environment.GetEnvironmentVariable(OwnerStartedAtEnvironmentVariable);
        Environment.SetEnvironmentVariable(OwnerProcessIdEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(OwnerStartedAtEnvironmentVariable, null);
        return ParseOwnerIdentity(processId, startedAt);
    }

    internal static NetworkProcessIdentity? ParseOwnerIdentity(string? processId, string? startedAt)
    {
        if (processId is null && startedAt is null)
            return null;
        if (!int.TryParse(processId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0 ||
            !long.TryParse(startedAt, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) ||
            ticks <= DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new ArgumentException("NetworkProxy owner process identity is invalid.");

        return new NetworkProcessIdentity
        {
            ProcessId = id,
            StartedAtUtc = new DateTimeOffset(ticks, TimeSpan.Zero)
        };
    }

    internal static async Task WatchAsync(NetworkProcessIdentity? owner, CancellationTokenSource lifetime)
    {
        if (owner is null)
            return;

        try
        {
            using var process = Process.GetProcessById(owner.ProcessId);
            if (process.StartTime.ToUniversalTime() != owner.StartedAtUtc.UtcDateTime)
            {
                lifetime.Cancel();
                return;
            }

            await process.WaitForExitAsync(lifetime.Token).ConfigureAwait(false);
            lifetime.Cancel();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // The worker is shutting down normally; its owner can remain alive.
        }
        catch (ArgumentException)
        {
            lifetime.Cancel();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"NetworkProxy owner monitor failed: {ex.GetType().Name}: {ex.Message}");
            lifetime.Cancel();
        }
    }
}
