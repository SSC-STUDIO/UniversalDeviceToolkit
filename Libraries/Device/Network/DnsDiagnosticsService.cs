using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>One DNS probe outcome for a resolution channel (system / custom server / DoH).</summary>
public sealed record DnsProbeResult(
    string Channel,
    bool Success,
    string[] Addresses,
    long ElapsedMs,
    string? Error);

/// <summary>Resolves a domain through system DNS, an explicit server (UDP 53), or DoH — with timing.</summary>
public static class DnsDiagnosticsService
{
    public static async Task<DnsProbeResult> ResolveSystemAsync(string domain, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(domain, cancellationToken).ConfigureAwait(false);
            watch.Stop();
            return new DnsProbeResult("system", true, addresses.Select(a => a.ToString()).ToArray(), watch.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new DnsProbeResult("system", false, [], watch.ElapsedMilliseconds, ex.Message);
        }
    }

    public static async Task<DnsProbeResult> ResolveCustomServerAsync(string domain, string dnsServer, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            if (!IPAddress.TryParse(dnsServer.Trim(), out var serverIp))
            {
                var hostAddresses = await Dns.GetHostAddressesAsync(dnsServer.Trim(), cancellationToken).ConfigureAwait(false);
                serverIp = hostAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                           ?? throw new FormatException($"Cannot resolve DNS server '{dnsServer}'.");
            }

            var addresses = await QueryARecordsAsync(domain, serverIp, cancellationToken).ConfigureAwait(false);
            watch.Stop();
            return new DnsProbeResult("custom", true, addresses.Select(a => a.ToString()).ToArray(), watch.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new DnsProbeResult("custom", false, [], watch.ElapsedMilliseconds, ex.Message);
        }
    }

    public static async Task<DnsProbeResult> ResolveDohAsync(string domain, string dohUrl, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var baseUrl = dohUrl.Trim();
            var separator = baseUrl.Contains('?') ? '&' : '?';
            var requestUrl = $"{baseUrl}{separator}name={Uri.EscapeDataString(domain)}&type=A";

            using var handler = new HttpClientHandler { AutomaticDecompression = global::System.Net.DecompressionMethods.All };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Accept.ParseAdd("application/dns-json");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);

            var addresses = new List<string>();
            if (document.RootElement.TryGetProperty("Answer", out var answers) && answers.ValueKind == JsonValueKind.Array)
            {
                foreach (var answer in answers.EnumerateArray())
                {
                    if (answer.TryGetProperty("type", out var typeElement) && typeElement.GetInt32() == 1 &&
                        answer.TryGetProperty("data", out var dataElement) && dataElement.GetString() is { } data)
                    {
                        addresses.Add(data);
                    }
                }
            }

            watch.Stop();
            return new DnsProbeResult("doh", true, addresses.ToArray(), watch.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            watch.Stop();
            return new DnsProbeResult("doh", false, [], watch.ElapsedMilliseconds, ex.Message);
        }
    }

    /// <summary>Minimal DNS A-record query over UDP 53 (single question, parses A answers).</summary>
    private static async Task<IPAddress[]> QueryARecordsAsync(string domain, IPAddress serverIp, CancellationToken cancellationToken)
    {
        var queryId = (ushort)Random.Shared.Next(0, ushort.MaxValue);
        var packet = BuildQueryPacket(queryId, domain);

        using var udp = new UdpClient(serverIp.AddressFamily);
        udp.Client.ReceiveTimeout = 4000;
        await udp.SendAsync(packet, packet.Length, new IPEndPoint(serverIp, 53)).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

        var response = await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
        return ParseAnswers(response.Buffer, queryId);
    }

    private static byte[] BuildQueryPacket(ushort queryId, string domain)
    {
        using var stream = new global::System.IO.MemoryStream();
        WriteUInt16(stream, queryId);
        WriteUInt16(stream, 0x0100); // standard query, recursion desired
        WriteUInt16(stream, 1);      // QDCOUNT
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);

        foreach (var label in domain.Trim().TrimEnd('.').Split('.'))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        stream.WriteByte(0);
        WriteUInt16(stream, 1); // QTYPE A
        WriteUInt16(stream, 1); // QCLASS IN
        return stream.ToArray();
    }

    private static IPAddress[] ParseAnswers(byte[] buffer, ushort queryId)
    {
        if (buffer.Length < 12 || ReadUInt16(buffer, 0) != queryId)
            return [];

        var answerCount = ReadUInt16(buffer, 6);
        var offset = 12;
        offset = SkipName(buffer, offset);
        offset += 4; // QTYPE + QCLASS

        var addresses = new List<IPAddress>();
        for (var i = 0; i < answerCount && offset < buffer.Length; i++)
        {
            offset = SkipName(buffer, offset);
            if (offset + 10 > buffer.Length)
                break;

            var type = ReadUInt16(buffer, offset);
            var dataLength = ReadUInt16(buffer, offset + 8);
            offset += 10;
            if (offset + dataLength > buffer.Length)
                break;

            if (type == 1 && dataLength == 4)
            {
                var bytes = new byte[4];
                Buffer.BlockCopy(buffer, offset, bytes, 0, 4);
                addresses.Add(new IPAddress(bytes));
            }

            offset += dataLength;
        }

        return addresses.ToArray();
    }

    private static int SkipName(byte[] buffer, int offset)
    {
        while (offset < buffer.Length)
        {
            var length = buffer[offset];
            if (length == 0)
                return offset + 1;

            if ((length & 0xC0) == 0xC0)
                return offset + 2;

            offset += length + 1;
        }

        return offset;
    }

    private static void WriteUInt16(global::System.IO.MemoryStream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value & 0xFF));
    }

    private static int ReadUInt16(byte[] buffer, int offset) =>
        (buffer[offset] << 8) | buffer[offset + 1];
}
