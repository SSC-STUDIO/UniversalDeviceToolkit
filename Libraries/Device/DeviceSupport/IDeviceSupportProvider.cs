using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.DeviceSupport;

public interface IDeviceSupportProvider
{
    string Id { get; }

    Task<DeviceSupportCatalog> GetCatalogAsync(CancellationToken token = default);

    DeviceFeatureAvailability Evaluate(MachineInformation machineInformation, DeviceSupportCatalog? catalog = null);
}
