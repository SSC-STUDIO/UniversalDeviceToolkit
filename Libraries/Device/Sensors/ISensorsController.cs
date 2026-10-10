using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensor controller interface for obtaining CPU/GPU temperature, frequency, fan speed, and other sensor data.
/// </summary>
/// <remarks>
/// Implementation is provided by SensorsControllerV1, V2, or V3 depending on hardware version.
/// Data acquisition communicates with Lenovo hardware via WMI interface.
/// </remarks>
public interface ISensorsController : IDisposable
{
    /// <summary>
    /// Checks whether the current device supports sensor monitoring.
    /// </summary>
    /// <returns>Returns true if the device supports sensor monitoring, false otherwise.</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// Prepares the sensor controller and initializes necessary resources.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PrepareAsync();

    /// <summary>
    /// Gets sensor data.
    /// </summary>
    /// <param name="detailed">Whether to obtain detailed data (including GPU power consumption, etc.).</param>
    /// <returns>A SensorsData object containing temperature, frequency, fan speed, and other information.</returns>
    Task<SensorsData> GetDataAsync(bool detailed = false);

    /// <summary>
    /// Gets CPU and GPU fan speeds.
    /// </summary>
    /// <returns>A tuple containing CPU fan speed and GPU fan speed.</returns>
    Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync();
}
