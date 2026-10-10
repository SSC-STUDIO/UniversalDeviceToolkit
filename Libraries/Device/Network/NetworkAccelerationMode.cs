namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>Acceleration operating mode. Default for new installs is <see cref="Off"/>.</summary>
public enum NetworkAccelerationMode
{
    /// <summary>No proxy, hosts, or PAC changes. Default.</summary>
    Off = 0,

    /// <summary>System HTTP(S) proxy pointing at the local worker (loopback only).</summary>
    SystemProxy = 1,

    /// <summary>
    /// Hosts-file redirection inside UDT-marked blocks only.
    /// Reserved: Start is refused and the UI omits this mode until a local TLS origin exists.
    /// </summary>
    Hosts = 2,

    /// <summary>Diagnostics and rule preview without mutating system network state.</summary>
    DiagnosticsOnly = 3
}
