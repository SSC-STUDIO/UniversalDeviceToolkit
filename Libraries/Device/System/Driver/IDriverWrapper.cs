using System;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace UniversalDeviceToolkit.Lib.System.Driver;

/// <summary>
/// Abstraction interface for driver-level operations using DeviceIoControl.
/// Enables testability by allowing mock implementations.
/// </summary>
public interface IDriverWrapper : IDisposable
{
    /// <summary>
    /// Get or create a driver handle for the specified driver path.
    /// </summary>
    /// <param name="driverPath">The driver device path (e.g., "\\\\.\\EnergyDrv").</param>
    /// <returns>A safe file handle to the driver.</returns>
    SafeFileHandle GetHandle(string driverPath);

    /// <summary>
    /// Send a control code to the driver and receive a response.
    /// </summary>
    /// <typeparam name="TIn">The input buffer type.</typeparam>
    /// <typeparam name="TOut">The output buffer type.</typeparam>
    /// <param name="handle">The driver handle.</param>
    /// <param name="controlCode">The IOCTL control code.</param>
    /// <param name="input">The input buffer value.</param>
    /// <returns>The output buffer value from the driver.</returns>
    Task<TOut> SendCommandAsync<TIn, TOut>(SafeFileHandle handle, uint controlCode, TIn input) where TIn : struct where TOut : struct;

    /// <summary>
    /// Send a simple uint control code to the driver.
    /// </summary>
    /// <param name="handle">The driver handle.</param>
    /// <param name="controlCode">The IOCTL control code.</param>
    /// <param name="input">The input uint value.</param>
    /// <returns>The output uint value from the driver.</returns>
    Task<uint> SendCommandAsync(SafeFileHandle handle, uint controlCode, uint input);

    /// <summary>
    /// Check if the driver is available on the current system.
    /// </summary>
    /// <param name="driverPath">The driver device path to check.</param>
    /// <returns>True if the driver is available, false otherwise.</returns>
    bool IsAvailable(string driverPath);
}