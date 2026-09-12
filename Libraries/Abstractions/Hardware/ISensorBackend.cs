namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Represents a single sensor reading with name, category, value, and unit.
/// </summary>
/// <param name="Name">The sensor display name (e.g. "CPU Package Temperature").</param>
/// <param name="Category">The sensor category (e.g. "Temperature", "Fan", "Voltage").</param>
/// <param name="Value">The numeric reading value.</param>
/// <param name="Unit">The unit of measurement (e.g. "°C", "RPM", "V").</param>
public record SensorReading(string Name, string Category, double Value, string Unit);

/// <summary>
/// Abstraction for reading hardware sensor data from the underlying platform.
/// Implementations may use LibreHardwareMonitor, lm-sensors, or other sensor APIs.
/// </summary>
public interface ISensorBackend
{
    /// <summary>
    /// Gets a value indicating whether the sensor backend is available and functional.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns all current sensor readings.
    /// </summary>
    /// <returns>A read-only list of sensor readings.</returns>
    IReadOnlyList<SensorReading> GetReadings();
}
