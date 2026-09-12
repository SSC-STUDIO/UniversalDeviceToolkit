using UniversalDeviceToolkit.NetworkProxy.Host;
using UniversalDeviceToolkit.NetworkProxy.Ipc;

namespace UniversalDeviceToolkit.NetworkProxy;

/// <summary>
/// Isolated network-acceleration worker. Loopback HTTP/CONNECT proxy + named-pipe IPC.
/// Does not auto-enable proxy/hosts/certs; the GUI must send an explicit Start.
/// Inspired by Watt Toolkit behavior; independent implementation (non-GPL).
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Prefer UDT_NETWORK_PROXY_TOKEN env (cleared after read); --token remains for back-compat.
        var sessionToken = NetworkProxyIpcServer.ResolveSessionToken(args);
        var pipeName = NetworkProxyIpcServer.ResolvePipeName(args);
        var listenPort = NetworkProxyIpcServer.ResolveListenPort(args);

        // Real loopback proxy (CONNECT tunnel, no MITM). Stub remains available for unit tests.
        await using INetworkProxyHost host = new LocalHttpProxyHost(listenPort);
        await using var ipc = new NetworkProxyIpcServer(pipeName, sessionToken, host);

        Console.WriteLine(
            $"UDT NetworkProxy worker ready. pipe={pipeName} bind=127.0.0.1/::1 port={listenPort} (idle until Start)");

        try
        {
            await ipc.RunAsync(cts.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            // Worker failures must never bubble into the GUI process.
            Console.Error.WriteLine($"NetworkProxy fatal: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
        finally
        {
            try { await host.StopAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"NetworkProxy: host stop on exit: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
