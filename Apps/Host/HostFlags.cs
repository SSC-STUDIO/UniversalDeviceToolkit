using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Command-line flags for the headless host. Mirrors the WPF app's Flags
/// surface for the options that matter headlessly; UI-owned flags are absent.
/// </summary>
public sealed class HostFlags
{
    public bool Trace { get; private init; }
    public bool SafeStart { get; private init; }
    public bool NoHardware { get; private init; }
    public bool ExperimentalGpuWorkingMode { get; private init; }
    public string? ProxyUrl { get; private init; }
    public string? ProxyUsername { get; private init; }
    public string? ProxyPassword { get; private init; }
    public bool ProxyAllowAllCerts { get; private init; }

    public static HostFlags Parse(IReadOnlyList<string> args)
    {
        var flags = new HostFlags();
        var trace = false;
        var safeStart = false;
        var noHardware = false;
        var experimentalGpuWorkingMode = false;
        string? proxyUrl = null;
        string? proxyUsername = null;
        string? proxyPassword = null;
        var proxyAllowAllCerts = false;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--trace":
                    trace = true;
                    break;
                case "--safe-start":
                    safeStart = true;
                    break;
                case "--no-hardware":
                    noHardware = true;
                    break;
                case "--experimental-gpu-working-mode":
                    experimentalGpuWorkingMode = true;
                    break;
                case "--proxy-url" when i + 1 < args.Count:
                    proxyUrl = args[++i];
                    break;
                case "--proxy-username" when i + 1 < args.Count:
                    proxyUsername = args[++i];
                    break;
                case "--proxy-password" when i + 1 < args.Count:
                    proxyPassword = args[++i];
                    break;
                case "--proxy-allow-all-certs":
                    proxyAllowAllCerts = true;
                    break;
            }
        }

        return new HostFlags
        {
            Trace = trace,
            SafeStart = safeStart,
            NoHardware = noHardware,
            ExperimentalGpuWorkingMode = experimentalGpuWorkingMode,
            ProxyUrl = proxyUrl,
            ProxyUsername = proxyUsername,
            ProxyPassword = proxyPassword,
            ProxyAllowAllCerts = proxyAllowAllCerts,
        };
    }

    public override string ToString()
        => string.Join(", ", new[]
        {
            Trace ? "--trace" : null,
            SafeStart ? "--safe-start" : null,
            NoHardware ? "--no-hardware" : null,
            ExperimentalGpuWorkingMode ? "--experimental-gpu-working-mode" : null,
            ProxyUrl is not null ? $"--proxy-url={ProxyUrl}" : null,
        }.Where(s => s is not null));
}
