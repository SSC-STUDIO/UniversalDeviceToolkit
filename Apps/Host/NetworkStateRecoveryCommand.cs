#if WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Network;

namespace UniversalDeviceToolkit.Host;

internal static class NetworkStateRecoveryCommand
{
    internal static int? TryRun(
        IReadOnlyList<string> arguments,
        Func<(bool Success, string Report)>? restore = null,
        TextWriter? output = null)
    {
        if (!arguments.Contains("--restore-network-state", StringComparer.Ordinal))
            return null;

        output ??= Console.Out;
        try
        {
            var result = restore is null ? RestoreSnapshot() : restore();
            output.WriteLine(result.Report);
            output.Flush();
            return result.Success ? 0 : 1;
        }
        catch (Exception error)
        {
            output.WriteLine(error);
            output.Flush();
            return 1;
        }
    }

    private static (bool Success, string Report) RestoreSnapshot()
    {
        // The recovery service acquires the shared per-user network lease.
        // This path initializes neither hardware, IoC nor a proxy worker.
        var success = new NetworkStateRecoveryService().TryRestoreFromSnapshot(out var report);
        return (success, report);
    }
}
#endif
