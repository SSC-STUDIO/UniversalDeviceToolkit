using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Concrete implementation of compatibility service that delegates to static Compatibility class
/// </summary>
public class CompatibilityService : ICompatibilityService
{
    public Task<MachineInformation> GetMachineInformationAsync()
    {
        return Compatibility.GetMachineInformationAsync();
    }

    public bool IsSupportedDevice(MachineInformation machineInformation)
    {
        return Compatibility.IsSupportedDevice(machineInformation);
    }

    public bool IsSupportedLegionMachine(MachineInformation machineInformation)
    {
        return Compatibility.IsSupportedLegionMachine(machineInformation);
    }

    public Task<bool> CheckBasicCompatibilityAsync()
    {
        return Compatibility.CheckBasicCompatibilityAsync();
    }

    public Task<(bool isCompatible, MachineInformation machineInformation)> IsCompatibleAsync()
    {
        return Compatibility.IsCompatibleAsync();
    }
}
