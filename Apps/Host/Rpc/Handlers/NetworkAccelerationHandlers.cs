using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Network acceleration bridge (P1): traffic/runtime snapshots, system-state restore
/// and NAT / DNS / IPv6 diagnostics. Diagnostic probes never throw — failures surface
/// as { error = message } payloads so the renderer keeps working.
/// </summary>
public static class NetworkAccelerationHandlers
{
    private const string DefaultStunHost = "stun.miwifi.com";
    private const string DefaultDnsDomain = "store.steampowered.com";
    private const int StunPort = 3478;

    private static INetworkAccelerationService NetworkService => IoCContainer.Resolve<INetworkAccelerationService>();

    private static INetworkStateRecoveryService RecoveryService => IoCContainer.Resolve<INetworkStateRecoveryService>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("network.getTrafficSnapshot", (_, ct) => HandleGetTrafficSnapshotAsync(ct));
        rpc.RegisterHandler("network.getRuntimeSnapshot", (_, ct) => HandleGetRuntimeSnapshotAsync(ct));
        rpc.RegisterHandler("network.restore", (_, ct) => HandleRestoreAsync(ct));
        rpc.RegisterHandler("network.detectNat", (request, ct) => HandleDetectNatAsync(request, ct));
        rpc.RegisterHandler("network.detectDns", (request, ct) => HandleDetectDnsAsync(request, ct));
        rpc.RegisterHandler("network.detectIpv6", (_, ct) => HandleDetectIpv6Async(ct));
    }

    /// <summary>Latest proxy worker counters; null when the worker is not running.</summary>
    private static async Task<BridgeResult> HandleGetTrafficSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await NetworkService.GetTrafficSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
                return BridgeResult.Ok(null);

            return BridgeResult.Ok(new
            {
                bytesUploaded = snapshot.BytesUploaded,
                bytesDownloaded = snapshot.BytesDownloaded,
                activeConnections = snapshot.ActiveConnections,
                totalConnections = snapshot.TotalConnections,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Traffic + connection/destination details; null when the worker is not running.</summary>
    private static async Task<BridgeResult> HandleGetRuntimeSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await NetworkService.GetRuntimeSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
                return BridgeResult.Ok(null);

            return BridgeResult.Ok(new
            {
                healthStatus = snapshot.HealthStatus,
                traffic = new
                {
                    bytesUploaded = snapshot.Traffic?.BytesUploaded ?? 0,
                    bytesDownloaded = snapshot.Traffic?.BytesDownloaded ?? 0,
                    activeConnections = snapshot.Traffic?.ActiveConnections ?? 0,
                    totalConnections = snapshot.Traffic?.TotalConnections ?? 0,
                },
                connections = (snapshot.Connections ?? []).Select(connection => new
                {
                    host = connection?.Host ?? string.Empty,
                    port = connection?.Port ?? 0,
                    state = connection?.State ?? "unknown",
                    connectLatencyMs = connection?.ConnectLatencyMs,
                }).ToArray(),
                destinations = (snapshot.Destinations ?? []).Select(destination => new
                {
                    host = destination?.Host ?? string.Empty,
                    port = destination?.Port ?? 0,
                    totalConnections = destination?.TotalConnections ?? 0,
                    activeConnections = destination?.ActiveConnections ?? 0,
                    lastConnectLatencyMs = destination?.LastConnectLatencyMs,
                }).ToArray(),
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops acceleration and restores system proxy/hosts from the last snapshot, then
    /// flips the mode back to Off (mirrors the WPF danger-zone restore button).
    /// </summary>
    private static async Task<BridgeResult> HandleRestoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var service = NetworkService;
            await service.StopAsync(cancellationToken).ConfigureAwait(false);
            var restored = RecoveryService.TryRestoreFromSnapshot(out var report);
            service.Config.Mode = NetworkAccelerationMode.Off;
            await service.SaveConfigAsync(cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { ok = restored, report });
        }
        catch (Exception ex)
        {
            return BridgeResult.Ok(new { ok = false, error = $"{ex.GetType().Name}: {ex.Message}" });
        }
    }

    /// <summary>STUN binding probe (NatTypeDetector): Open/NAT/UDP-blocked/Unknown.</summary>
    private static async Task<BridgeResult> HandleDetectNatAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var stunHost = ReadString(request, "stunServer", DefaultStunHost);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var result = await NatTypeDetector.CheckAsync(stunHost, StunPort, cts.Token).ConfigureAwait(false);
            return BridgeResult.Ok(new
            {
                natType = result.Type.ToString(),
                localIp = result.LocalIp,
                publicIp = result.PublicIp,
                internetAvailable = result.InternetAvailable,
                error = result.Error,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Ok(new
            {
                natType = nameof(NatType.Unknown),
                localIp = (string?)null,
                publicIp = (string?)null,
                internetAvailable = false,
                error = ex.Message,
            });
        }
    }

    /// <summary>
    /// Probes system DNS (always), the explicit server (when dnsServer is set) and DoH
    /// (when enabled); returns the fastest successful channel's latency and addresses.
    /// </summary>
    private static async Task<BridgeResult> HandleDetectDnsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var domain = ReadString(request, "domain", DefaultDnsDomain);
            var dnsServer = ReadString(request, "dnsServer", null);
            var dohEnabled = ReadBool(request, "dohEnabled", false);
            var dohUrl = ReadString(request, "dohUrl", null);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var systemResult = await DnsDiagnosticsService.ResolveSystemAsync(domain, cts.Token).ConfigureAwait(false);

            DnsProbeResult? customResult = null;
            if (!string.IsNullOrWhiteSpace(dnsServer))
            {
                try
                {
                    customResult = await DnsDiagnosticsService
                        .ResolveCustomServerAsync(domain, dnsServer, cts.Token)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Non-fatal: report the system channel below.
                }
            }

            DnsProbeResult? dohResult = null;
            if (dohEnabled && !string.IsNullOrWhiteSpace(dohUrl))
            {
                try
                {
                    dohResult = await DnsDiagnosticsService
                        .ResolveDohAsync(domain, dohUrl!, cts.Token)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Non-fatal: report the system channel below.
                }
            }

            var fastest = new[] { systemResult, customResult, dohResult }
                .Where(result => result is not null && result.Success)
                .OrderBy(result => result!.ElapsedMs)
                .FirstOrDefault();

            var outcome = fastest ?? systemResult;
            return BridgeResult.Ok(new
            {
                success = outcome.Success,
                elapsedMs = outcome.ElapsedMs,
                addresses = outcome.Addresses,
                error = outcome.Error,
                channel = outcome.Channel,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Ok(new
            {
                success = false,
                elapsedMs = 0L,
                addresses = Array.Empty<string>(),
                error = ex.Message,
                channel = (string?)null,
            });
        }
    }

    /// <summary>IPv6 address + routability probe (Ipv6Detector).</summary>
    private static async Task<BridgeResult> HandleDetectIpv6Async(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var result = await Ipv6Detector.CheckAsync(cts.Token).ConfigureAwait(false);
            return BridgeResult.Ok(new
            {
                supported = result.Supported,
                address = result.Address,
                error = result.Error,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Ok(new
            {
                supported = false,
                address = (string?)null,
                error = ex.Message,
            });
        }
    }

    private static string ReadString(BridgeRequest request, string name, string? fallback)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Object &&
            request.Parameters.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value!;
        }

        return fallback ?? string.Empty;
    }

    private static bool ReadBool(BridgeRequest request, string name, bool fallback)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Object &&
            request.Parameters.TryGetProperty(name, out var property) &&
            (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return fallback;
    }
}
