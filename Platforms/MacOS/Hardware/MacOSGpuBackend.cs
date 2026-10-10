using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.MacOS.Hardware;

/// <summary>
/// macOS implementation of <see cref="IGpuBackend"/>.
/// Stub implementation as GPU management is limited on macOS.
/// </summary>
public sealed class MacOSGpuBackend : IGpuBackend
{
    /// <inheritdoc />
    public bool IsAvailable => false; // GPU management not supported on macOS

    /// <inheritdoc />
    public string? GetGpuName() => null;

    /// <inheritdoc />
    public int? GetUsagePercent() => null;

    /// <inheritdoc />
    public int? GetTemperatureCelsius() => null;

    /// <inheritdoc />
    public int? GetCurrentClockMhz() => null;

    /// <inheritdoc />
    public int? GetBoostClockMhz() => null;

    /// <inheritdoc />
    public int? GetMemoryUsedMb() => null;

    /// <inheritdoc />
    public int? GetMemoryTotalMb() => null;
}
