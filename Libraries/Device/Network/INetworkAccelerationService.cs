using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Network;

public interface INetworkAccelerationService
{
    NetworkAccelerationConfig Config { get; }

    bool IsBackendReady { get; }

    bool IsRunning { get; }

    string StatusText { get; }

    /// <summary>Returns the latest counters from the running proxy worker.</summary>
    Task<NetworkProxyTrafficSnapshot?> GetTrafficSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns traffic, connection, and destination details for the current run.</summary>
    Task<NetworkProxyRuntimeSnapshot?> GetRuntimeSnapshotAsync(CancellationToken cancellationToken = default);

    Task ReloadConfigAsync(CancellationToken cancellationToken = default);

    Task SaveConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the NetworkProxy worker and applies system proxy / Hosts only when the user
    /// has enabled acceleration. Never called automatically on app launch.
    /// </summary>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// On next launch after a crash: if UDT left system proxy/hosts dirty, restore from snapshot.
    /// Does not re-start acceleration. Safe to call always; no-op when clean.
    /// </summary>
    Task EnsureCleanSystemStateOnStartupAsync(CancellationToken cancellationToken = default);
}

public interface INetworkDiagnosticsService
{
    Task<NetworkDiagnosticsReport> RunQuickCheckAsync(CancellationToken cancellationToken = default);
}

public sealed class NetworkDiagnosticsReport
{
    public bool LoopbackReachable { get; init; } = true;
    public bool AccelerationEnabled { get; init; }
    public NetworkAccelerationMode Mode { get; init; }
    public string Summary { get; init; } = "Diagnostics placeholder (Phase 1).";
}

public interface INetworkStateRecoveryService
{
    string SnapshotPath { get; }

    Task<NetworkStateSnapshot?> LoadSnapshotAsync(CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(NetworkStateSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores system proxy / UDT hosts block / PAC path from the last snapshot.
    /// Only UDT-owned proxy state is written back. On full success the snapshot is consumed.
    /// Idempotent when no snapshot exists (returns success with a skipped report).
    /// </summary>
    bool TryRestoreFromSnapshot(out string report);

    /// <summary>Captures current state into a versioned pending snapshot file.</summary>
    Task<NetworkStateSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a lifecycle phase onto the current snapshot.
    /// When marking <see cref="NetworkSnapshotPhase.Applied"/>, records UDT-owned fingerprints.
    /// </summary>
    bool TryMarkPhase(NetworkSnapshotPhase phase, out string report, int? listenPort = null);

    /// <summary>Deletes the snapshot file after a successful restore. Missing file is success.</summary>
    bool TryConsumeSnapshot(out string report);
}
