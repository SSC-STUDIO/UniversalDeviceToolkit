using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>
/// Orchestrates the NetworkProxy worker, system proxy / PAC / Hosts apply, and stop/restore.
/// Default remains OFF — never auto-starts on application launch.
/// </summary>
public sealed class NetworkAccelerationService : INetworkAccelerationService, IAsyncDisposable
{
    private readonly NetworkAccelerationSettings _settings;
    private readonly INetworkStateRecoveryService _recovery;
    private readonly NetworkProxyWorkerLauncher _launcher;
    private readonly object _gate = new();
    private bool _isRunning;
    private bool _appliedSystemMutation;

    public NetworkAccelerationService(
        NetworkAccelerationSettings settings,
        INetworkStateRecoveryService recovery)
    {
        _settings = settings;
        _recovery = recovery;
        _launcher = new NetworkProxyWorkerLauncher();
    }

    public NetworkAccelerationConfig Config => _settings.Store;

    public bool IsBackendReady => NetworkProxyWorkerLauncher.IsWorkerAvailable();

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning && _launcher.IsWorkerAlive;
        }
    }

    public string StatusText
    {
        get
        {
            if (!Config.AccelerationEnabled || Config.Mode == NetworkAccelerationMode.Off)
                return Resource.NetworkAcceleration_Status_Off;

            if (!IsBackendReady)
                return Resource.NetworkAcceleration_Status_WorkerMissing;

            if (Config.Mode == NetworkAccelerationMode.DiagnosticsOnly)
                return Resource.NetworkAcceleration_Status_DiagnosticsOnly;

            return IsRunning
                ? string.Format(Resource.NetworkAcceleration_Status_Running, Config.Mode, Config.ListenPort)
                : string.Format(Resource.NetworkAcceleration_Status_Stopped, Config.Mode);
        }
    }

    public async Task<NetworkProxyTrafficSnapshot?> GetTrafficSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsRunning || !_launcher.IsWorkerAlive)
            return null;

        try
        {
            var result = await _launcher.CreateClient()
                .StatusAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success || result.Data is null)
                return null;

            return new NetworkProxyTrafficSnapshot
            {
                BytesUploaded = ReadInt64(result.Data, "bytesUploaded"),
                BytesDownloaded = ReadInt64(result.Data, "bytesDownloaded"),
                ActiveConnections = (int)Math.Clamp(ReadInt64(result.Data, "activeConnections"), 0, int.MaxValue),
                TotalConnections = Math.Max(0, ReadInt64(result.Data, "totalConnections")),
                HealthStatus = ReadString(result.Data, "health", "unknown")
            };
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "network-accel-traffic-status",
                "Network acceleration traffic status is temporarily unavailable.",
                ex);
            return null;
        }

        static long ReadInt64(IReadOnlyDictionary<string, string> data, string key)
            => data.TryGetValue(key, out var raw) &&
               long.TryParse(raw, global::System.Globalization.NumberStyles.Integer,
                    global::System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                 : 0;

        static string ReadString(IReadOnlyDictionary<string, string> data, string key, string fallback)
            => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
    }

    public async Task<NetworkProxyRuntimeSnapshot?> GetRuntimeSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsRunning || !_launcher.IsWorkerAlive)
            return null;

        try
        {
            var client = _launcher.CreateClient();
            var status = await client.StatusAsync(cancellationToken).ConfigureAwait(false);
            if (!status.Success || status.Data is null)
                return null;

            var traffic = new NetworkProxyTrafficSnapshot
            {
                BytesUploaded = ReadInt64(status.Data, "bytesUploaded"),
                BytesDownloaded = ReadInt64(status.Data, "bytesDownloaded"),
                ActiveConnections = (int)Math.Clamp(ReadInt64(status.Data, "activeConnections"), 0, int.MaxValue),
                TotalConnections = Math.Max(0, ReadInt64(status.Data, "totalConnections")),
                HealthStatus = ReadString(status.Data, "health", "unknown")
            };

            var connections = await ReadSnapshotsAsync<NetworkProxyConnectionSnapshot>(
                () => client.ConnectionsAsync(cancellationToken), cancellationToken).ConfigureAwait(false);
            var destinations = await ReadSnapshotsAsync<NetworkProxyDestinationSnapshot>(
                () => client.DestinationsAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

            return new NetworkProxyRuntimeSnapshot
            {
                Traffic = traffic,
                HealthStatus = traffic.HealthStatus,
                Connections = connections,
                Destinations = destinations
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "network-accel-runtime-status",
                "Network acceleration runtime details are temporarily unavailable.",
                ex);
            return null;
        }

        static long ReadInt64(IReadOnlyDictionary<string, string> data, string key)
            => data.TryGetValue(key, out var raw) &&
               long.TryParse(raw, global::System.Globalization.NumberStyles.Integer,
                    global::System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;

        static string ReadString(IReadOnlyDictionary<string, string> data, string key, string fallback)
            => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;

        static async Task<IReadOnlyList<T>> ReadSnapshotsAsync<T>(
            Func<Task<NetworkProxyIpcResult>> request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await request().ConfigureAwait(false);
                if (!result.Success || result.Data is null ||
                    !result.Data.TryGetValue("items", out var itemsJson) ||
                    string.IsNullOrWhiteSpace(itemsJson))
                {
                    return Array.Empty<T>();
                }

                return JsonSerializer.Deserialize<IReadOnlyList<T>>(itemsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? Array.Empty<T>();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Array.Empty<T>();
            }
            catch
            {
                return Array.Empty<T>();
            }
        }
    }

    public Task ReloadConfigAsync(CancellationToken cancellationToken = default)
    {
        _settings.InvalidateCache();
        _ = _settings.Store;
        return Task.CompletedTask;
    }

    public Task SaveConfigAsync(CancellationToken cancellationToken = default)
    {
        if (!Config.AccelerationEnabled)
            Config.Mode = NetworkAccelerationMode.Off;

        if (Config.ListenPort is <= 0 or > 65535)
            Config.ListenPort = NetworkAccelerationDefaults.DefaultListenPort;

        _settings.SynchronizeStore();
        return Task.CompletedTask;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Config.AccelerationEnabled)
                return false;

            if (Config.Mode is NetworkAccelerationMode.Off)
                return false;

            if (Config.Mode == NetworkAccelerationMode.DiagnosticsOnly)
            {
                lock (_gate)
                    _isRunning = false;
                return true;
            }

            // Hosts→127.0.0.1 without a local TLS origin breaks HTTPS. Refuse until redesigned.
            if (Config.Mode == NetworkAccelerationMode.Hosts)
            {
                Log.Instance.Warning(
                    "NetworkAcceleration Hosts mode Start refused: mapping domains to 127.0.0.1 is disabled until a local TLS origin exists. Use SystemProxy (PAC) or DiagnosticsOnly. Hosts file helpers remain for future use.");
                lock (_gate)
                    _isRunning = false;
                return false;
            }

            if (!IsBackendReady)
                return false;

            // SystemProxy: require enabled domains up front — never CreateLoopbackProxy as silent fallback.
            if (Config.Mode == NetworkAccelerationMode.SystemProxy)
            {
                var preDomains = CollectEnabledDomains();
                if (!CanApplySystemProxy(preDomains))
                {
                    Log.Instance.Warning(
                        "NetworkAcceleration SystemProxy Start refused: no enabled domains. Enable at least one domain group; refusing silent full-loopback proxy.");
                    lock (_gate)
                        _isRunning = false;
                    return false;
                }
            }

            // Heal any leftover snapshot before capturing a new baseline.
            if (File.Exists(_recovery.SnapshotPath) &&
                !_recovery.TryRestoreFromSnapshot(out var healReport))
            {
                Log.Instance.Warning(
                    "NetworkAcceleration Start refused: previous snapshot could not be restored. " + healReport);
                lock (_gate)
                    _isRunning = false;
                return false;
            }

            // Capture pre-mutation state for crash/stop recovery.
            var snapshot = await _recovery.CaptureSnapshotAsync(cancellationToken).ConfigureAwait(false);
            Config.LastRecoverySnapshot = new NetworkRecoverySnapshotMetadata
            {
                CapturedAtUtc = snapshot.CapturedAtUtc,
                SnapshotPath = _recovery.SnapshotPath,
                HadSystemProxy = snapshot.SystemProxy is not null,
                HadHostsBlock = !string.IsNullOrEmpty(snapshot.HostsMarkedBlock),
                HadPacPath = !string.IsNullOrEmpty(snapshot.PacFilePath),
                Notes = "Captured before StartAsync"
            };
            await SaveConfigAsync(cancellationToken).ConfigureAwait(false);

            await _launcher.EnsureStartedAsync(Config.ListenPort, cancellationToken).ConfigureAwait(false);

            var client = _launcher.CreateClient();
            var startResult = await client.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!startResult.Success)
            {
                await _launcher.StopAsync(cancellationToken).ConfigureAwait(false);
                TryHealSnapshotAfterFailedStart();
                lock (_gate)
                    _isRunning = false;
                return false;
            }

            var domains = CollectEnabledDomains();

            // Re-check after worker start before mutating system proxy.
            if (Config.Mode == NetworkAccelerationMode.SystemProxy)
            {
                if (!CanApplySystemProxy(domains))
                {
                    Log.Instance.Warning(
                        "NetworkAcceleration SystemProxy Start aborted after worker start: no enabled domains; worker stopped, system proxy not mutated.");
                    await _launcher.StopAsync(cancellationToken).ConfigureAwait(false);
                    TryHealSnapshotAfterFailedStart();
                    lock (_gate)
                        _isRunning = false;
                    return false;
                }
            }

            // Defense-in-depth: push enabled domains so the worker rejects non-allowlisted hosts.
            // Empty list = deny-all on the host (fail closed; no open loopback forwarder).
            var rulesResult = await client.SetRulesAsync(domains, cancellationToken).ConfigureAwait(false);
            if (!rulesResult.Success)
            {
                Log.Instance.Warning(
                    $"NetworkAcceleration SetRules failed after Start; stopping worker. {rulesResult.Message}");
                try { await client.StopAsync(cancellationToken).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    Log.Instance.TraceOnce(
                        "network-accel-rules-stop",
                        "Stop after SetRules failure failed.",
                        ex);
                }

                await _launcher.StopAsync(cancellationToken).ConfigureAwait(false);
                TryHealSnapshotAfterFailedStart();
                lock (_gate)
                    _isRunning = false;
                return false;
            }

            if (!TryApplySystemMutationOrRollback(
                    _recovery,
                    () => ApplySystemSideEffects(Config.ListenPort),
                    Config.ListenPort,
                    ref _appliedSystemMutation,
                    out var applyReport))
            {
                Log.Instance.Warning(
                    "NetworkAcceleration system mutation failed and was rolled back. " + applyReport);
                await _launcher.StopAsync(cancellationToken).ConfigureAwait(false);
                lock (_gate)
                    _isRunning = false;
                return false;
            }

            lock (_gate)
                _isRunning = true;
            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.Warning("NetworkAcceleration StartAsync failed.", ex);
            try { await _launcher.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception stopEx)
            {
                Log.Instance.TraceOnce(
                    "network-accel-start-stop",
                    "Stop after StartAsync failure failed.",
                    stopEx);
            }

            TryHealSnapshotAfterFailedStart();
            lock (_gate)
                _isRunning = false;
            return false;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_launcher.IsWorkerAlive)
            {
                try
                {
                    var client = _launcher.CreateClient();
                    await client.StopAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Instance.WarningOnce(
                        "network-accel-stop-ipc",
                        "Network acceleration IPC stop failed; falling back to process kill.",
                        ex);
                }
            }

            await _launcher.StopAsync(cancellationToken).ConfigureAwait(false);

            if (_appliedSystemMutation)
            {
                var restored = _recovery.TryRestoreFromSnapshot(out var restoreReport);
                if (restored)
                {
                    _appliedSystemMutation = false;
                }
                else
                {
                    Log.Instance.Warning(
                        "NetworkAcceleration StopAsync restore failed; applied flag retained for retry. " +
                        restoreReport);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Warning("NetworkAcceleration StopAsync failed.", ex);
        }
        finally
        {
            lock (_gate)
                _isRunning = false;
        }
    }

    private bool ApplySystemSideEffects(int port)
    {
        switch (Config.Mode)
        {
            case NetworkAccelerationMode.SystemProxy:
            {
                var domains = CollectEnabledDomains();
                // Selective PAC only — never CreateLoopbackProxy when the domain list is empty.
                if (!CanApplySystemProxy(domains))
                {
                    Log.Instance.Warning(
                        "SystemProxy apply refused: no enabled domains (full loopback proxy is not used).");
                    return false;
                }

                SystemProxyApplicator.Apply(SystemProxyApplicator.CreatePacProxy(port, domains.ToArray()));
                return true;
            }
            case NetworkAccelerationMode.Hosts:
            {
                // Defense-in-depth: StartAsync refuses Hosts mode. Do not map domains to 127.0.0.1.
                Log.Instance.Warning(
                    "Hosts mode system apply is disabled until a local TLS origin exists; hosts file not modified.");
                return false;
            }
            default:
                return false;
        }
    }

    private void TryHealSnapshotAfterFailedStart()
    {
        if (_recovery.TryRestoreFromSnapshot(out var report))
        {
            _appliedSystemMutation = false;
            return;
        }

        Log.Instance.Warning(
            "NetworkAcceleration Start failure left an unrestored snapshot. " + report);
    }

    /// <summary>
    /// Applies a system mutation. On throw, restores the snapshot and only
    /// clears <paramref name="appliedSystemMutation"/> when restore succeeds.
    /// </summary>
    internal static bool TryApplySystemMutationOrRollback(
        INetworkStateRecoveryService recovery,
        Func<bool> apply,
        int listenPort,
        ref bool appliedSystemMutation,
        out string report)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(apply);

        try
        {
            var mutated = apply();
            if (!mutated)
            {
                var unusedPendingHandled = recovery.TryRestoreFromSnapshot(out report);
                appliedSystemMutation = false;
                if (!unusedPendingHandled)
                    report = "apply: no mutation; pending snapshot not consumed. " + report;
                return true;
            }

            if (!recovery.TryMarkPhase(NetworkSnapshotPhase.Applied, out var markReport, listenPort))
            {
                appliedSystemMutation = true;
                report = markReport;
                return true;
            }

            appliedSystemMutation = true;
            report = markReport;
            return true;
        }
        catch (Exception ex)
        {
            var restored = recovery.TryRestoreFromSnapshot(out var restoreReport);
            appliedSystemMutation = !restored;
            report = $"apply: failure ({ex.GetType().Name}: {ex.Message}). {restoreReport}";
            return false;
        }
    }

    private List<string> CollectEnabledDomains()
        => CollectEnabledDomains(Config.DomainGroups);

    internal static List<string> CollectEnabledDomains(IEnumerable<NetworkDomainGroup>? groups)
    {
        return (groups ?? [])
            .Where(g => g is not null && g.Enabled)
            .SelectMany(g => (g.Domains ?? [])
                .Concat((g.SubItems ?? [])
                    .Where(s => s is not null && s.Enabled)
                    .Select(s => s.Domain)))
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Pure gate: system proxy / PAC may be applied only when at least one non-empty domain is present.
    /// Empty lists must not fall back to a full loopback system proxy.
    /// </summary>
    internal static bool CanApplySystemProxy(IEnumerable<string>? domains)
    {
        if (domains is null)
            return false;

        foreach (var d in domains)
        {
            if (!string.IsNullOrWhiteSpace(d))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Pure gate for Start eligibility by mode (no worker / no system mutation).
    /// Hosts mode is always refused until a local TLS origin exists.
    /// SystemProxy requires enabled domains. DiagnosticsOnly is always allowed.
    /// </summary>
    internal static bool CanStartMode(NetworkAccelerationMode mode, IEnumerable<string>? enabledDomains) =>
        mode switch
        {
            NetworkAccelerationMode.DiagnosticsOnly => true,
            NetworkAccelerationMode.SystemProxy => CanApplySystemProxy(enabledDomains),
            // Hosts→127.0.0.1 without local origin breaks HTTPS; helpers kept for future redesign.
            NetworkAccelerationMode.Hosts => false,
            _ => false
        };

    /// <inheritdoc />
    public Task EnsureCleanSystemStateOnStartupAsync(CancellationToken cancellationToken = default)
    {
        // Never auto-start acceleration. Only heal leftover UDT mutations from a previous crash.
        try
        {
            // Ensure default domain groups exist for older configs.
            EnsureBuiltinDomainGroups();

            // Force master switch semantics: disabled config never implies running.
            if (!Config.AccelerationEnabled)
                Config.Mode = NetworkAccelerationMode.Off;

            // If a snapshot exists and current proxy still points at loopback UDT, restore.
            // TryRestoreFromSnapshot is idempotent when clean / missing.
            if (File.Exists(_recovery.SnapshotPath) || Config.LastRecoverySnapshot is not null)
            {
                _recovery.TryRestoreFromSnapshot(out var report);
                Log.Instance.Trace($"NetworkAcceleration startup recovery: {report}");
            }

            // Kill any orphaned worker processes left from a previous GUI crash.
            NetworkProxyWorkerLauncher.TryKillOrphanedWorkers();
        }
        catch (Exception ex)
        {
            Log.Instance.Warning("NetworkAcceleration EnsureCleanSystemStateOnStartupAsync failed.", ex);
        }

        return Task.CompletedTask;
    }

    private void EnsureBuiltinDomainGroups()
    {
        Config.DomainGroups ??= [];
        if (Config.DomainGroups.Count == 0)
        {
            Config.DomainGroups = BuiltinDomainGroups.CreateDefaults();
            try { _settings.SynchronizeStore(); }
            catch { /* non-fatal */ }
            return;
        }

        // Older configs: drop the removed "custom" group and backfill new public-cdn entries.
        if (MigrateDomainGroups(Config.DomainGroups))
        {
            try { _settings.SynchronizeStore(); }
            catch { /* non-fatal */ }
        }
    }

    /// <summary>
    /// Migrates persisted domain groups from older config versions.
    /// Removes the retired "custom" group and merges every built-in group's metadata,
    /// domains, and sub-items without touching the user's Enabled choices. Returns true when
    /// anything changed.
    /// </summary>
    internal static bool MigrateDomainGroups(List<NetworkDomainGroup> groups)
    {
        var changed = groups.RemoveAll(g => string.Equals(g?.Id, "custom", StringComparison.OrdinalIgnoreCase)) > 0;

        foreach (var defaultGroup in BuiltinDomainGroups.CreateDefaults())
        {
            var existing = groups.FirstOrDefault(g => string.Equals(g?.Id, defaultGroup.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                groups.Add(defaultGroup);
                changed = true;
                continue;
            }

            existing.Domains ??= [];
            existing.SubItems ??= [];

            if (string.IsNullOrWhiteSpace(existing.DisplayName))
            {
                existing.DisplayName = defaultGroup.DisplayName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.IconKey) && !string.IsNullOrWhiteSpace(defaultGroup.IconKey))
            {
                existing.IconKey = defaultGroup.IconKey;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.Description) && !string.IsNullOrWhiteSpace(defaultGroup.Description))
            {
                existing.Description = defaultGroup.Description;
                changed = true;
            }

            foreach (var domain in defaultGroup.Domains ?? [])
            {
                if (!existing.Domains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
                {
                    existing.Domains.Add(domain);
                    changed = true;
                }
            }

            foreach (var defaultSubItem in defaultGroup.SubItems ?? [])
            {
                var existingSubItem = existing.SubItems.FirstOrDefault(s =>
                    string.Equals(s?.Id, defaultSubItem.Id, StringComparison.OrdinalIgnoreCase));
                if (existingSubItem is null)
                {
                    existing.SubItems.Add(CloneSubItem(defaultSubItem));
                    changed = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existingSubItem.DisplayName))
                {
                    existingSubItem.DisplayName = defaultSubItem.DisplayName;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existingSubItem.Domain))
                {
                    existingSubItem.Domain = defaultSubItem.Domain;
                    changed = true;
                }
            }
        }

        return changed;

        static NetworkDomainSubItem CloneSubItem(NetworkDomainSubItem source) => new()
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Domain = source.Domain,
            Enabled = source.Enabled,
            IsBeta = source.IsBeta
        };
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
