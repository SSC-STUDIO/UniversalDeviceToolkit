using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Services;

/// <summary>
/// Represents a service that monitors and controls the battery discharge rate.
/// </summary>
public interface IBatteryDischargeRateMonitorService
{
    /// <summary>
    /// Starts the monitoring service if needed based on current system conditions.
    /// Stops the service if it's no longer needed.
    /// </summary>
    Task StartStopIfNeededAsync();
    
    /// <summary>
    /// Stops the battery discharge rate monitoring service.
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Gets a value indicating whether the service is currently running.
    /// </summary>
    bool IsRunning { get; }
}
