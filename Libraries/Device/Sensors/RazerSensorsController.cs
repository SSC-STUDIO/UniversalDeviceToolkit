using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Razer;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for Razer Blade machines: generic (vendor-neutral) snapshot plus EC
/// fan reads via the HID protocol (class 0x0D cmd 0x81, value × 100 = RPM).
/// Self-disables on non-Razer machines or when no control interface answers.
/// </summary>
public class RazerSensorsController(GPUController gpuController, IRazerHidController controller) : GenericSensorsController(gpuController)
{
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!await IsRazerMachineAsync().ConfigureAwait(false) || !controller.Probe())
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking Razer sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override async Task<int> GetCpuCurrentFanSpeedAsync() =>
        controller.GetFanRpm(RazerPacket.ZoneCpu)
        ?? await AwaitWithTimeoutAsync(base.GetCpuCurrentFanSpeedAsync()).ConfigureAwait(false);

    protected override async Task<int> GetGpuCurrentFanSpeedAsync() =>
        controller.GetFanRpm(RazerPacket.ZoneGpu)
        ?? await AwaitWithTimeoutAsync(base.GetGpuCurrentFanSpeedAsync()).ConfigureAwait(false);

    private static async Task<bool> IsRazerMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Vendor?.Contains("Razer", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for Razer sensor detection.", ex);
            return false;
        }
    }
}
