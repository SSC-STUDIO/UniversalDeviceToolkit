using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;

public class DGPUCapabilityNotify(IDelayProvider delayProvider) : AbstractDGPUNotify(delayProvider)
{
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Features.Source == MachineInformation.FeatureData.SourceType.CapabilityData
                   && mi.Features[CapabilityID.IGPUMode];
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("dgpu-cap-notify-supported", "DGPUCapabilityNotify support probe failed.", ex);
            return false;
        }
    }

    protected override Task NotifyDGPUStatusAsync(bool state) => WMI.LenovoOtherMethod.SetFeatureValueAsync(CapabilityID.GPUStatus, state ? 1 : 0);

    protected override async Task<HardwareId> GetDGPUHardwareIdAsync()
    {
        try
        {
            var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.GPUDidVid).ConfigureAwait(false);
            var vendorId = value & 0xFFFF;
            var deviceId = value >> 16;
            return new HardwareId($"{vendorId:X4}", $"{deviceId:X4}");
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("dgpu-cap-hwid", "Failed to read dGPU hardware id via capability GPUDidVid.", ex);
            return HardwareId.Empty;
        }
    }
}
