using System;
using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Shared defaults for the network-acceleration feature and worker process.</summary>
public static class NetworkAccelerationDefaults
{
    public const int DefaultListenPort = 34123;
    public const string DefaultPipeName = "udt-network-proxy";
    public const string HostsBeginMarker = "# BEGIN UDT-NETWORK-ACCELERATION";
    public const string HostsEndMarker = "# END UDT-NETWORK-ACCELERATION";
    public const string SnapshotFileName = "network_state_snapshot.json";
    public const string SettingsFileName = "network_acceleration.json";

    /// <summary>Current on-disk snapshot schema. Unversioned legacy files deserialize as 0 and remain readable.</summary>
    public const int SnapshotSchemaVersion = 1;
}

/// <summary>Lifecycle of a network-state snapshot used for crash/stop recovery.</summary>
public enum NetworkSnapshotPhase
{
    /// <summary>Captured before a system mutation; apply has not committed.</summary>
    Pending = 0,

    /// <summary>UDT owns the live system proxy / PAC / hosts mutation.</summary>
    Applied = 1,

    /// <summary>Original state restored; the snapshot file should be consumed.</summary>
    Restored = 2
}

/// <summary>Persisted / runtime configuration for network acceleration.</summary>
public sealed class NetworkAccelerationConfig
{
    /// <summary>Master switch. Must remain false by default — never auto-start on launch.</summary>
    public bool AccelerationEnabled { get; set; }

    public NetworkAccelerationMode Mode { get; set; } = NetworkAccelerationMode.Off;

    public int ListenPort { get; set; } = NetworkAccelerationDefaults.DefaultListenPort;

    public List<NetworkDomainGroup> DomainGroups { get; set; } = [];

    public string? DnsServer { get; set; }

    public string? DohUrl { get; set; }

    public string? CertificateFingerprintSha256 { get; set; }

    public NetworkRecoverySnapshotMetadata? LastRecoverySnapshot { get; set; }

    /// <summary>When true, the WPF navigation entry is shown. Acceleration still defaults off.</summary>
    public bool ShowInNavigation { get; set; } = true;

    public static NetworkAccelerationConfig CreateDefault() => new()
    {
        AccelerationEnabled = false,
        Mode = NetworkAccelerationMode.Off,
        ListenPort = NetworkAccelerationDefaults.DefaultListenPort,
        DomainGroups = BuiltinDomainGroups.CreateDefaults(),
        ShowInNavigation = true
    };
}

public sealed class NetworkDomainGroup
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }

    /// <summary>
    /// User pin/favorite (Watt Toolkit-style). Favorites sort first and can be batch-started.
    /// </summary>
    public bool IsFavorite { get; set; }

    public List<string> Domains { get; set; } = [];

    /// <summary>Structured sub-items for tree-style CDN configuration (Watt Toolkit reference).</summary>
    public List<NetworkDomainSubItem> SubItems { get; set; } = [];

    /// <summary>Icon resource key for brand logos (e.g. "SteamLogo", "GitHubLogo").</summary>
    public string? IconKey { get; set; }

    /// <summary>Short description shown below the group name.</summary>
    public string? Description { get; set; }
}

/// <summary>Individual domain entry within a group, with optional beta flag.</summary>
public sealed class NetworkDomainSubItem
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool IsBeta { get; set; }
}

/// <summary>Metadata describing the last successful recovery / snapshot restore.</summary>
public sealed class NetworkRecoverySnapshotMetadata
{
    public DateTimeOffset? CapturedAtUtc { get; set; }
    public string? SnapshotPath { get; set; }
    public bool HadSystemProxy { get; set; }
    public bool HadHostsBlock { get; set; }
    public bool HadPacPath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>On-disk snapshot used by <see cref="INetworkStateRecoveryService"/> restore.</summary>
public sealed class NetworkStateSnapshot
{
    public int SchemaVersion { get; set; }

    public NetworkSnapshotPhase Phase { get; set; } = NetworkSnapshotPhase.Pending;

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SystemProxySnapshot? SystemProxy { get; set; }

    public string? HostsMarkedBlock { get; set; }

    public string? PacFilePath { get; set; }

    public string? PacFileContents { get; set; }

    /// <summary>Listen port recorded when the mutation was marked <see cref="NetworkSnapshotPhase.Applied"/>.</summary>
    public int? AppliedListenPort { get; set; }

    /// <summary>System proxy server string written by UDT, used to recognize UDT-owned state.</summary>
    public string? AppliedProxyServer { get; set; }

    /// <summary>PAC / AutoConfig URL written by UDT, used to recognize UDT-owned state.</summary>
    public string? AppliedAutoConfigUrl { get; set; }
}

public sealed class SystemProxySnapshot
{
    public bool Enabled { get; set; }
    public string? Server { get; set; }
    public string? Override { get; set; }
    public string? AutoConfigUrl { get; set; }
}
