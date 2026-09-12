using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Abstract interface for compatibility checking operations
/// </summary>
public interface ICompatibilityService
{
    /// <summary>
    /// Get machine information including all hardware capabilities
    /// </summary>
    Task<MachineInformation> GetMachineInformationAsync();

    /// <summary>
    /// Check if the machine has a supported hardware-control device profile
    /// </summary>
    bool IsSupportedDevice(MachineInformation machineInformation);

    /// <summary>
    /// Legacy alias for hardware-control device support
    /// </summary>
    bool IsSupportedLegionMachine(MachineInformation machineInformation);

    /// <summary>
    /// Check basic compatibility (WMI availability)
    /// </summary>
    Task<bool> CheckBasicCompatibilityAsync();

    /// <summary>
    /// Check full compatibility with machine information
    /// </summary>
    Task<(bool isCompatible, MachineInformation machineInformation)> IsCompatibleAsync();
}
