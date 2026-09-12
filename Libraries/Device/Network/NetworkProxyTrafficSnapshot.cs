using System;
using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Point-in-time traffic counters reported by the local proxy worker.</summary>
public sealed record NetworkProxyTrafficSnapshot
{
    public long BytesUploaded { get; init; }

    public long BytesDownloaded { get; init; }

    public int ActiveConnections { get; init; }

    public long TotalConnections { get; init; }

    public string HealthStatus { get; init; } = "unknown";
}

/// <summary>Combined runtime view returned by the acceleration worker.</summary>
public sealed record NetworkProxyRuntimeSnapshot
{
    public NetworkProxyTrafficSnapshot Traffic { get; init; } = new();

    public string HealthStatus { get; init; } = "unknown";

    public IReadOnlyList<NetworkProxyConnectionSnapshot> Connections { get; init; } = Array.Empty<NetworkProxyConnectionSnapshot>();

    public IReadOnlyList<NetworkProxyDestinationSnapshot> Destinations { get; init; } = Array.Empty<NetworkProxyDestinationSnapshot>();
}

/// <summary>Safe connection summary for the current session or recent connection history.</summary>
public sealed record NetworkProxyConnectionSnapshot
{
    public long Id { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }

    public string Protocol { get; init; } = "Unknown";

    public DateTime StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public long BytesUploaded { get; init; }

    public long BytesDownloaded { get; init; }

    public long? ConnectLatencyMs { get; init; }

    public string State { get; init; } = "unknown";

    public string? Error { get; init; }
}

/// <summary>Aggregated counters for one destination host and port.</summary>
public sealed record NetworkProxyDestinationSnapshot
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }

    public int ActiveConnections { get; init; }

    public long TotalConnections { get; init; }

    public long BytesUploaded { get; init; }

    public long BytesDownloaded { get; init; }

    public long? LastConnectLatencyMs { get; init; }

    public string LastState { get; init; } = "unknown";

    public DateTime LastUpdatedAtUtc { get; init; }
}
