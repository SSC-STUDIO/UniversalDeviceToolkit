using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;

public class DGPUGamezoneNotify(IDelayProvider delayProvider) : AbstractDGPUNotify(delayProvider)
{
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi is { Properties.SupportsIGPUMode: true };
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("dgpu-gamezone-notify-supported", "DGPUGamezoneNotify support probe failed.", ex);
            return false;
        }
    }

    protected override Task NotifyDGPUStatusAsync(bool state) => WMI.LenovoGameZoneData.NotifyDGPUStatusAsync(state ? 1 : 0);

    protected override async Task<HardwareId> GetDGPUHardwareIdAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.GetDGPUHWIdAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("dgpu-gamezone-hwid", "Failed to read dGPU hardware id via GameZone WMI.", ex);
            return HardwareId.Empty;
        }
    }
}
