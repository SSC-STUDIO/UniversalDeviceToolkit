using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>
/// Versioned snapshot / restore for system proxy, UDT-marked hosts block, and PAC path.
/// Restore writes back only UDT-owned mutations and consumes the snapshot on success.
/// </summary>
public sealed class NetworkStateRecoveryService : INetworkStateRecoveryService
{
    internal const string UdtPacFileName = "udt-network-acceleration.pac";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _snapshotDirectory;
    private readonly Func<string> _readHosts;
    private readonly Action<string> _writeHosts;
    private readonly Func<SystemProxySnapshot?> _readSystemProxy;
    private readonly Action<SystemProxySnapshot?> _writeSystemProxy;

    public NetworkStateRecoveryService()
        : this(
            Folders.AppData,
            ReadHostsFile,
            WriteHostsFile,
            ReadSystemProxyFromRegistry,
            WriteSystemProxyToRegistry)
    {
    }

    /// <summary>Testable constructor with injectable IO.</summary>
    public NetworkStateRecoveryService(
        string snapshotDirectory,
        Func<string> readHosts,
        Action<string> writeHosts,
        Func<SystemProxySnapshot?> readSystemProxy,
        Action<SystemProxySnapshot?> writeSystemProxy)
    {
        _snapshotDirectory = snapshotDirectory ?? throw new ArgumentNullException(nameof(snapshotDirectory));
        _readHosts = readHosts ?? throw new ArgumentNullException(nameof(readHosts));
        _writeHosts = writeHosts ?? throw new ArgumentNullException(nameof(writeHosts));
        _readSystemProxy = readSystemProxy ?? throw new ArgumentNullException(nameof(readSystemProxy));
        _writeSystemProxy = writeSystemProxy ?? throw new ArgumentNullException(nameof(writeSystemProxy));
    }

    public string SnapshotPath => Path.Combine(_snapshotDirectory, NetworkAccelerationDefaults.SnapshotFileName);

    public async Task<NetworkStateSnapshot?> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SnapshotPath))
            return null;

        await using var stream = File.OpenRead(SnapshotPath);
        return await JsonSerializer.DeserializeAsync<NetworkStateSnapshot>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SaveSnapshotAsync(NetworkStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveSnapshotCore(snapshot);
        return Task.CompletedTask;
    }

    public async Task<NetworkStateSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var hosts = _readHosts();
        HostsMarkedBlock.TryExtract(hosts, out var block);
        var proxy = _readSystemProxy();

        string? pacPath = null;
        string? pacContents = null;
        if (TryResolveLocalPacPath(proxy?.AutoConfigUrl, out var resolved) && File.Exists(resolved))
        {
            pacPath = resolved;
            if (!IsUdtPacFile(resolved))
            {
                try
                {
                    pacContents = File.ReadAllText(resolved);
                }
                catch (Exception ex)
                {
                    Log.Instance.TraceOnce(
                        "network-snapshot-pac-read",
                        "Failed to capture original PAC file contents.",
                        ex);
                }
            }
        }

        var snapshot = new NetworkStateSnapshot
        {
            SchemaVersion = NetworkAccelerationDefaults.SnapshotSchemaVersion,
            Phase = NetworkSnapshotPhase.Pending,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            SystemProxy = proxy,
            HostsMarkedBlock = block,
            PacFilePath = pacPath,
            PacFileContents = pacContents
        };

        await SaveSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    public bool TryMarkPhase(NetworkSnapshotPhase phase, out string report, int? listenPort = null)
    {
        try
        {
            if (!TryReadSnapshotFile(out var snapshot, out var readReport) || snapshot is null)
            {
                report = string.IsNullOrEmpty(readReport)
                    ? "snapshot: none (cannot mark phase)."
                    : readReport;
                return false;
            }

            if (!IsSupportedSchemaVersion(snapshot.SchemaVersion))
            {
                report = $"snapshot: unsupported schema {snapshot.SchemaVersion} (cannot mark phase).";
                return false;
            }

            snapshot.Phase = phase;
            if (phase == NetworkSnapshotPhase.Applied)
            {
                var current = _readSystemProxy();
                snapshot.AppliedListenPort = listenPort ?? snapshot.AppliedListenPort;
                snapshot.AppliedProxyServer = current?.Server;
                snapshot.AppliedAutoConfigUrl = current?.AutoConfigUrl;
            }

            SaveSnapshotCore(snapshot);
            report = $"snapshot: phase {phase}.";
            return true;
        }
        catch (Exception ex)
        {
            report = $"snapshot: mark phase failed ({ex.GetType().Name}: {ex.Message}).";
            return false;
        }
    }

    public bool TryConsumeSnapshot(out string report)
    {
        try
        {
            if (!File.Exists(SnapshotPath))
            {
                report = "snapshot: already consumed.";
                return true;
            }

            File.Delete(SnapshotPath);
            report = "snapshot: consumed.";
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                if (TryReadSnapshotFile(out var snapshot, out _) && snapshot is not null)
                {
                    snapshot.Phase = NetworkSnapshotPhase.Restored;
                    SaveSnapshotCore(snapshot);
                }
            }
            catch (Exception markEx)
            {
                report =
                    $"snapshot: consume failed ({ex.GetType().Name}: {ex.Message}); mark restored failed ({markEx.GetType().Name}: {markEx.Message}).";
                return false;
            }

            report = $"snapshot: consume failed ({ex.GetType().Name}: {ex.Message}); marked restored.";
            return false;
        }
    }

    public bool TryRestoreFromSnapshot(out string report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Network state recovery report");
        sb.AppendLine("----------------------------------------");

        NetworkStateSnapshot? snapshot;
        try
        {
            if (!TryReadSnapshotFile(out snapshot, out var readReport))
            {
                sb.AppendLine(readReport);
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("Result: PARTIAL");
                report = sb.ToString();
                return false;
            }

            if (snapshot is null)
            {
                sb.AppendLine(string.IsNullOrEmpty(readReport)
                    ? "snapshot: none (idempotent no-op)."
                    : readReport);
                sb.AppendLine("----------------------------------------");
                sb.AppendLine("Result: OK");
                report = sb.ToString();
                return true;
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"snapshot: failed to load ({ex.GetType().Name}: {ex.Message}).");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("Result: PARTIAL");
            report = sb.ToString();
            return false;
        }

        if (!IsSupportedSchemaVersion(snapshot.SchemaVersion))
        {
            sb.AppendLine($"snapshot: unsupported schema {snapshot.SchemaVersion} (left untouched).");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("Result: PARTIAL");
            report = sb.ToString();
            return false;
        }

        if (snapshot.Phase == NetworkSnapshotPhase.Restored)
        {
            var consumed = TryConsumeSnapshot(out var consumeReport);
            sb.AppendLine("snapshot: already restored (no re-apply).");
            sb.AppendLine(consumeReport);
            sb.AppendLine("----------------------------------------");
            sb.AppendLine(consumed ? "Result: OK" : "Result: PARTIAL");
            report = sb.ToString();
            return consumed;
        }

        var success = true;
        var proxyWasUdtOwned = false;

        try
        {
            var current = _readSystemProxy();
            proxyWasUdtOwned = IsUdtOwnedProxy(current, snapshot);
            if (!proxyWasUdtOwned)
            {
                sb.AppendLine("system proxy: skipped (current is not UDT-owned).");
            }
            else if (snapshot.SystemProxy is null)
            {
                _writeSystemProxy(new SystemProxySnapshot { Enabled = false });
                sb.AppendLine("system proxy: cleared UDT-owned proxy.");
            }
            else
            {
                _writeSystemProxy(snapshot.SystemProxy);
                sb.AppendLine("system proxy: restored from snapshot.");
            }
        }
        catch (Exception ex)
        {
            success = false;
            sb.AppendLine($"system proxy: failure ({ex.GetType().Name}: {ex.Message}).");
        }

        try
        {
            var hosts = _readHosts();
            if (snapshot.HostsMarkedBlock is null)
            {
                var removed = HostsMarkedBlock.Remove(hosts);
                if (!string.Equals(removed, hosts, StringComparison.Ordinal))
                {
                    _writeHosts(removed);
                    sb.AppendLine("hosts: UDT marked block removed.");
                }
                else
                {
                    sb.AppendLine("hosts: no UDT marked block present.");
                }
            }
            else
            {
                var lines = snapshot.HostsMarkedBlock.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                var updated = HostsMarkedBlock.Upsert(hosts, lines);
                _writeHosts(updated);
                sb.AppendLine("hosts: UDT marked block restored from snapshot.");
            }
        }
        catch (Exception ex)
        {
            success = false;
            sb.AppendLine($"hosts: failure ({ex.GetType().Name}: {ex.Message}).");
        }

        if (proxyWasUdtOwned &&
            !string.IsNullOrWhiteSpace(snapshot.PacFilePath) &&
            !string.IsNullOrWhiteSpace(snapshot.PacFileContents) &&
            !IsUdtPacFile(snapshot.PacFilePath))
        {
            try
            {
                var pacPath = snapshot.PacFilePath;
                var directory = Path.GetDirectoryName(pacPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(pacPath, snapshot.PacFileContents);
                sb.AppendLine($"pac: restored file at {pacPath}.");
            }
            catch (Exception ex)
            {
                success = false;
                sb.AppendLine($"pac: failure ({ex.GetType().Name}: {ex.Message}).");
            }
        }
        else
        {
            sb.AppendLine("pac: skipped (no original PAC file, UDT PAC, or not UDT-owned).");
        }

        if (success)
        {
            if (TryConsumeSnapshot(out var consumeReport))
            {
                sb.AppendLine(consumeReport);
            }
            else
            {
                TryMarkPhase(NetworkSnapshotPhase.Restored, out var markReport);
                sb.AppendLine(consumeReport);
                sb.AppendLine(markReport);
            }
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine(success ? "Result: OK" : "Result: PARTIAL");
        report = sb.ToString();
        return success;
    }

    internal static bool IsSupportedSchemaVersion(int version) =>
        version is 0 or NetworkAccelerationDefaults.SnapshotSchemaVersion;

    internal static bool IsUdtOwnedProxy(SystemProxySnapshot? current, NetworkStateSnapshot? snapshot = null)
    {
        if (current is null)
            return false;

        if (!string.IsNullOrWhiteSpace(current.AutoConfigUrl) &&
            current.AutoConfigUrl.Contains(UdtPacFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(snapshot?.AppliedAutoConfigUrl) &&
            string.Equals(current.AutoConfigUrl, snapshot.AppliedAutoConfigUrl, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(snapshot?.AppliedProxyServer) &&
            string.Equals(current.Server, snapshot.AppliedProxyServer, StringComparison.OrdinalIgnoreCase))
            return true;

        var port = snapshot?.AppliedListenPort ?? NetworkAccelerationDefaults.DefaultListenPort;
        return IsUdtLoopbackPort(current.Server, port);
    }

    internal static bool IsUdtLoopbackPort(string? server, int port)
    {
        if (string.IsNullOrWhiteSpace(server) || port is <= 0 or > 65535)
            return false;

        var portToken = ":" + port.ToString(CultureInfo.InvariantCulture);
        if (!server.Contains(portToken, StringComparison.Ordinal))
            return false;

        return server.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || server.Contains("[::1]", StringComparison.OrdinalIgnoreCase)
               || server.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsUdtPacFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (string.Equals(Path.GetFileName(path), UdtPacFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            return string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(SystemProxyApplicator.PacFilePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static bool TryResolveLocalPacPath(string? autoConfigUrl, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(autoConfigUrl))
            return false;

        var value = autoConfigUrl.Trim();
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                path = uri.LocalPath;
                return path.Length > 0;
            }

            return false;
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return false;

        try
        {
            path = Path.GetFullPath(value);
            return true;
        }
        catch (Exception)
        {
            path = string.Empty;
            return false;
        }
    }

    private bool TryReadSnapshotFile(out NetworkStateSnapshot? snapshot, out string report)
    {
        snapshot = null;
        report = string.Empty;

        if (!File.Exists(SnapshotPath))
        {
            report = "snapshot: none (idempotent no-op).";
            return true;
        }

        var json = File.ReadAllText(SnapshotPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            report = "snapshot: empty file (idempotent no-op).";
            return true;
        }

        try
        {
            snapshot = JsonSerializer.Deserialize<NetworkStateSnapshot>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            report = $"snapshot: failed to load ({ex.GetType().Name}: {ex.Message}).";
            return false;
        }

        if (snapshot is null)
        {
            report = "snapshot: null after deserialize (idempotent no-op).";
            return true;
        }

        return true;
    }

    private void SaveSnapshotCore(NetworkStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion <= 0)
            snapshot.SchemaVersion = NetworkAccelerationDefaults.SnapshotSchemaVersion;

        Directory.CreateDirectory(_snapshotDirectory);
        var tempPath = SnapshotPath + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, snapshot, JsonOptions);
                stream.Flush();
            }

            File.Move(tempPath, SnapshotPath, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception cleanupEx)
            {
                Log.Instance.TraceOnce(
                    "network-snapshot-tmp-cleanup",
                    "Failed to delete leftover snapshot temp file.",
                    cleanupEx);
            }

            throw;
        }
    }

    private static string ReadHostsFile()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void WriteHostsFile(string content)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        File.WriteAllText(path, content);
    }

    private static SystemProxySnapshot? ReadSystemProxyFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key is null)
                return null;

            return new SystemProxySnapshot
            {
                Enabled = Convert.ToInt32(key.GetValue("ProxyEnable", 0)) != 0,
                Server = key.GetValue("ProxyServer") as string,
                Override = key.GetValue("ProxyOverride") as string,
                AutoConfigUrl = key.GetValue("AutoConfigURL") as string
            };
        }
        catch (Exception ex)
        {
            Log.Instance.WarningOnce(
                "system-proxy-read",
                "Failed to read system proxy settings from registry.",
                ex);
            return null;
        }
    }

    private static void WriteSystemProxyToRegistry(SystemProxySnapshot? snapshot)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
        if (snapshot is null)
        {
            key.SetValue("ProxyEnable", 0);
            SystemProxyApplicator.NotifyWinInetChanged();
            return;
        }

        key.SetValue("ProxyEnable", snapshot.Enabled ? 1 : 0);
        if (snapshot.Server is not null)
            key.SetValue("ProxyServer", snapshot.Server);
        if (snapshot.Override is not null)
            key.SetValue("ProxyOverride", snapshot.Override);
        if (snapshot.AutoConfigUrl is not null)
            key.SetValue("AutoConfigURL", snapshot.AutoConfigUrl);
        else if (key.GetValue("AutoConfigURL") is not null && string.IsNullOrEmpty(snapshot.AutoConfigUrl))
            key.DeleteValue("AutoConfigURL", throwOnMissingValue: false);

        SystemProxyApplicator.NotifyWinInetChanged();
    }
}
