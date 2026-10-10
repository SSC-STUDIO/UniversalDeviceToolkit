// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

namespace UniversalDeviceToolkit.Abstractions.PackageDownloader;

/// <summary>
/// Cross-platform information about a driver package.
/// </summary>
public readonly struct DriverInfo
{
    private readonly string? _deviceId;
    private readonly string? _hardwareId;

    public DriverInfo(string? deviceId, string? hardwareId, Version? version, DateTime? date)
    {
        _deviceId = deviceId;
        _hardwareId = hardwareId;
        Version = version;
        Date = date;
    }

    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    public string DeviceId => _deviceId ?? string.Empty;

    /// <summary>
    /// Gets the hardware identifier.
    /// </summary>
    public string HardwareId => _hardwareId ?? string.Empty;

    /// <summary>
    /// Gets the optional driver version.
    /// </summary>
    public Version? Version { get; }

    /// <summary>
    /// Gets the optional driver date.
    /// </summary>
    public DateTime? Date { get; }
}
