using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Network;

public enum NatType
{
    Unknown = 0,
    OpenInternet = 1,
    Nat = 2,
    UdpBlocked = 3
}

/// <summary>Result of a simplified STUN binding check.</summary>
public sealed record NatCheckResult(
    NatType Type,
    string? LocalIp,
    string? PublicIp,
    bool InternetAvailable,
    string? Error);

/// <summary>
/// Simplified RFC 5389 STUN binding probe (single binding request → mapped address).
/// Full cone/restricted/symmetric classification needs multi-phase tests; this reports
/// Open / NAT / UDP blocked / Unknown, which is what the UI presents.
/// </summary>
public static class NatTypeDetector
{
    private const int StunTypeBindingRequest = 0x0001;
    private const uint MagicCookie = 0x2112A442;

    public static async Task<NatCheckResult> CheckAsync(
        string stunHost,
        int port = 3478,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stunHost))
            return new NatCheckResult(NatType.Unknown, null, null, false, "stun host empty");

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(stunHost.Trim(), cancellationToken).ConfigureAwait(false);
            var serverIp = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                           ?? addresses.FirstOrDefault();
            if (serverIp is null)
                return new NatCheckResult(NatType.Unknown, null, null, false, $"no address for {stunHost}");

            var localIp = GetPrimaryLocalIp(serverIp);
            var mapped = await QueryMappedAddressAsync(serverIp, port, localIp, cancellationToken).ConfigureAwait(false);

            if (mapped is null)
            {
                var internet = await CanReachTcp443Async(serverIp, cancellationToken).ConfigureAwait(false);
                return new NatCheckResult(NatType.UdpBlocked, localIp?.ToString(), null, internet, null);
            }

            var type = localIp is not null && mapped.Equals(localIp) ? NatType.OpenInternet : NatType.Nat;
            return new NatCheckResult(type, localIp?.ToString(), mapped.ToString(), true, null);
        }
        catch (Exception ex)
        {
            return new NatCheckResult(NatType.Unknown, null, null, false, ex.Message);
        }
    }

    private static IPAddress? GetPrimaryLocalIp(IPAddress remote)
    {
        try
        {
            using var socket = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(remote, 80);
            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IPAddress?> QueryMappedAddressAsync(
        IPAddress serverIp,
        int port,
        IPAddress? localIp,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(localIp is null ? AddressFamily.InterNetwork : localIp.AddressFamily);
        udp.Client.ReceiveTimeout = 3000;

        var transactionId = new byte[12];
        Random.Shared.NextBytes(transactionId);

        var request = new byte[20];
        WriteUInt16(request, 0, StunTypeBindingRequest);
        WriteUInt16(request, 2, 0);
        WriteUInt32(request, 4, MagicCookie);
        Buffer.BlockCopy(transactionId, 0, request, 8, 12);

        await udp.SendAsync(request, request.Length, new IPEndPoint(serverIp, port)).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            var response = await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
            return ParseMappedAddress(response.Buffer, transactionId);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static IPAddress? ParseMappedAddress(byte[] buffer, byte[] transactionId)
    {
        if (buffer.Length < 20 || !buffer.AsSpan(8, 12).SequenceEqual(transactionId))
            return null;

        var messageLength = ReadUInt16(buffer, 2);
        var offset = 20;
        var end = Math.Min(buffer.Length, 20 + messageLength);

        while (offset + 4 <= end)
        {
            var attributeType = ReadUInt16(buffer, offset);
            var attributeLength = ReadUInt16(buffer, offset + 2);
            var valueStart = offset + 4;
            if (valueStart + attributeLength > end)
                break;

            if ((attributeType == 0x0001 || attributeType == 0x0020) && attributeLength >= 8)
            {
                var family = buffer[valueStart + 1];
                if (family == 0x01)
                {
                    var portBytes = ReadUInt16(buffer, valueStart + 2);
                    _ = portBytes;
                    var addressBytes = new byte[4];
                    Buffer.BlockCopy(buffer, valueStart + 4, addressBytes, 0, 4);
                    if (attributeType == 0x0020)
                    {
                        var cookieBytes = BitConverter.GetBytes(MagicCookie);
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(cookieBytes);
                        for (var i = 0; i < 4; i++)
                            addressBytes[i] ^= cookieBytes[i];
                    }

                    return new IPAddress(addressBytes);
                }
            }

            offset = valueStart + ((attributeLength + 3) & ~3);
        }

        return null;
    }

    private static async Task<bool> CanReachTcp443Async(IPAddress host, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, 443, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteUInt16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static int ReadUInt16(byte[] buffer, int offset) =>
        (buffer[offset] << 8) | buffer[offset + 1];
}
