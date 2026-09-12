using UniversalDeviceToolkit.Lib.Network;

namespace UniversalDeviceToolkit.NetworkProxy.Host;

/// <summary>Loopback HTTP/CONNECT proxy host lifecycle.</summary>
public interface INetworkProxyHost : IAsyncDisposable
{
    bool IsRunning { get; }

    int ListenPort { get; }

    string StatusSummary { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();

    /// <summary>
    /// Sets host-suffix allowlist used for CONNECT/HTTP filtering.
    /// Null or empty = deny all destinations (fail closed until rules are set).
    /// </summary>
    void SetDomainAllowlist(IReadOnlyList<string>? domains);
}

/// <summary>Optional traffic counters exposed by a proxy host implementation.</summary>
public interface INetworkProxyTrafficSource
{
    long BytesUploaded { get; }

    long BytesDownloaded { get; }

    int ActiveConnections { get; }

    long TotalConnections { get; }

    IReadOnlyList<NetworkProxyConnectionSnapshot> GetConnectionSnapshots(int maxItems = 40);

    IReadOnlyList<NetworkProxyDestinationSnapshot> GetDestinationSnapshots(int maxItems = 40);
}
