namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Abstraction for querying GPU hardware information and telemetry.
/// Implementations may use NVAPI, AMD ADL, sysfs, or other platform-specific APIs.
/// </summary>
public interface IGpuBackend
{
    /// <summary>
    /// Gets a value indicating whether the GPU backend is available and functional.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the display name of the primary GPU, or <see langword="null"/> if unavailable.
    /// </summary>
    string? GetGpuName();

    /// <summary>
    /// Gets the current GPU core usage as a percentage (0–100), or <see langword="null"/> if unavailable.
    /// </summary>
    int? GetUsagePercent();

    /// <summary>
    /// Gets the current GPU core temperature in degrees Celsius, or <see langword="null"/> if unavailable.
    /// </summary>
    int? GetTemperatureCelsius();

    /// <summary>
    /// Gets the current GPU core clock speed in MHz, or <see langword="null"/> if unavailable.
    /// </summary>
    int? GetCurrentClockMhz();

    /// <summary>
    /// Gets the GPU boost/max clock speed in MHz, or <see langword="null"/> if unavailable.
    /// </summary>
    int? GetBoostClockMhz();

    /// <summary>
    /// Gets the amount of GPU memory currently in use in megabytes, or <see langword="null"/> if unavailable.
    /// </summary>
    int? GetMemoryUsedMb();

    /// <summary>
    /// Gets the total GPU memory in megabytes, or <see langword="null"/> if unavailable.
    /// </summary>
    int? GetMemoryTotalMb();
}
