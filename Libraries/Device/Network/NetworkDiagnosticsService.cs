using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Network;

public sealed class NetworkDiagnosticsService : INetworkDiagnosticsService
{
    private readonly INetworkAccelerationService _acceleration;

    public NetworkDiagnosticsService(INetworkAccelerationService acceleration)
    {
        _acceleration = acceleration;
    }

    public async Task<NetworkDiagnosticsReport> RunQuickCheckAsync(CancellationToken cancellationToken = default)
    {
        var config = _acceleration.Config;
        var sb = new StringBuilder();
        sb.AppendLine($"Acceleration enabled: {config.AccelerationEnabled}");
        sb.AppendLine($"Mode: {config.Mode}");
        sb.AppendLine($"Backend ready: {_acceleration.IsBackendReady}");
        sb.AppendLine($"Running: {_acceleration.IsRunning}");
        sb.AppendLine($"Listen port: {config.ListenPort}");

        var loopbackOk = await ProbeLoopbackAsync(config.ListenPort, cancellationToken).ConfigureAwait(false);
        sb.AppendLine($"Loopback TCP {config.ListenPort}: {(loopbackOk ? "open/reachable" : "closed (expected when stopped)")}");

        try
        {
            var gateways = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .Take(3)
                .ToArray();
            sb.AppendLine(gateways.Length == 0
                ? "Gateway: (none detected)"
                : $"Gateway(s): {string.Join(", ", gateways)}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Gateway probe failed: {ex.GetType().Name}");
        }

        if (!string.IsNullOrWhiteSpace(config.DnsServer))
            sb.AppendLine($"Configured DNS: {config.DnsServer}");
        if (!string.IsNullOrWhiteSpace(config.DohUrl))
            sb.AppendLine($"Configured DoH: {config.DohUrl}");

        sb.AppendLine();
        sb.AppendLine(config.AccelerationEnabled
            ? "Note: acceleration only mutates system proxy/hosts while Running and user-started."
            : "Acceleration is off (default). No system proxy/hosts/PAC mutations.");

        return new NetworkDiagnosticsReport
        {
            LoopbackReachable = loopbackOk || !_acceleration.IsRunning,
            AccelerationEnabled = config.AccelerationEnabled,
            Mode = config.Mode,
            Summary = sb.ToString().TrimEnd()
        };
    }

    private static async Task<bool> ProbeLoopbackAsync(int port, CancellationToken cancellationToken)
    {
        if (port is <= 0 or > 65535)
            return false;

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(400));
            await client.ConnectAsync(global::System.Net.IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"net-diag-loopback-{port}",
                $"Loopback probe failed for port {port} (proxy may not be listening).",
                ex);
            return false;
        }
    }
}
