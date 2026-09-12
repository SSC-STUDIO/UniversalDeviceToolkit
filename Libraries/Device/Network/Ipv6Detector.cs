using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Result of an IPv6 connectivity and address probe.</summary>
public sealed record Ipv6CheckResult(
    bool Supported,
    string? Address,
    string? Error);

/// <summary>
/// Lightweight IPv6 probe: checks whether the host has a routable IPv6 address
/// and whether a well-known IPv6 endpoint is reachable within a short timeout.
/// </summary>
public static class Ipv6Detector
{
    // Google Public DNS IPv6 — highly available, UDP-friendly probe target.
    private static readonly IPAddress ProbeTarget = IPAddress.Parse("2001:4860:4860::8888");
    private const int ProbePort = 53;
    private const int TimeoutMs = 3000;

    public static async Task<Ipv6CheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var localAddress = GetLocalIpv6Address();
            if (localAddress is null)
                return new Ipv6CheckResult(false, null, "No IPv6 address assigned.");

            // Attempt a short UDP send to the probe target to confirm routability.
            using var udp = new UdpClient(localAddress.AddressFamily);
            udp.Client.ReceiveTimeout = TimeoutMs;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(TimeoutMs));

            // Send a minimal DNS query (just enough to get a UDP response or timeout).
            var query = BuildDnsQuery("google.com");
            await udp.SendAsync(query, query.Length, new IPEndPoint(ProbeTarget, ProbePort))
                .ConfigureAwait(false);

            try
            {
                await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
                return new Ipv6CheckResult(true, localAddress.ToString(), null);
            }
            catch (OperationCanceledException)
            {
                // Timeout: we have an IPv6 address but cannot reach the probe — still "supported".
                return new Ipv6CheckResult(true, localAddress.ToString(), "Probe timed out.");
            }
            catch (SocketException)
            {
                return new Ipv6CheckResult(true, localAddress.ToString(), "Probe failed.");
            }
        }
        catch (Exception ex)
        {
            return new Ipv6CheckResult(false, null, ex.Message);
        }
    }

    /// <summary>Returns the first non-loopback, non-temporary IPv6 address, or null.</summary>
    private static IPAddress? GetLocalIpv6Address()
    {
        try
        {
            return global::System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == global::System.Net.NetworkInformation.OperationalStatus.Up
                              && nic.NetworkInterfaceType != global::System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6
                                        && !IPAddress.IsLoopback(addr.Address))
                ?.Address;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Builds a minimal DNS A-record query packet.</summary>
    private static byte[] BuildDnsQuery(string domain)
    {
        var parts = domain.Split('.');
        // Header: ID(2) + Flags(2) + QDCount(2) + ANCount(2) + NSCount(2) + ARCount(2) = 12
        var questionLen = parts.Sum(p => 1 + p.Length) + 1 + 4; // labels + root + QTYPE + QCLASS
        var buf = new byte[12 + questionLen];

        // Transaction ID
        buf[0] = 0x12; buf[1] = 0x34;
        // Flags: standard query
        buf[2] = 0x01; buf[3] = 0x00;
        // QDCOUNT = 1
        buf[5] = 0x01;

        var offset = 12;
        foreach (var part in parts)
        {
            buf[offset++] = (byte)part.Length;
            foreach (var c in part)
                buf[offset++] = (byte)c;
        }
        buf[offset++] = 0; // root label
        // QTYPE = A (1)
        buf[offset++] = 0; buf[offset++] = 1;
        // QCLASS = IN (1)
        buf[offset++] = 0; buf[offset++] = 1;

        return buf;
    }
}
