using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for ASUS machines: reuses the generic (vendor-neutral) snapshot for
/// temperatures/clocks/usage and upgrades fan readings to the ATKACPI endpoints
/// (CPU 0x00110013, GPU 0x00110014; values are RPM/100, mirroring G-Helper and
/// Linux asus-wmi). Self-disables on non-ASUS machines or when ATK is absent.
/// </summary>
public class AsusSensorsController(GPUController gpuController, IAsusAtkDriver atk) : GenericSensorsController(gpuController)
{
    private const uint CpuFanEndpoint = 0x00110013;
    private const uint GpuFanEndpoint = 0x00110014;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!atk.IsAvailable || !await IsAsusMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking ASUS sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadAtkFanSpeedAsync(CpuFanEndpoint, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadAtkFanSpeedAsync(GpuFanEndpoint, () => base.GetGpuCurrentFanSpeedAsync());

    private async Task<int> ReadAtkFanSpeedAsync(uint endpoint, Func<Task<int>> fallback)
    {
        try
        {
            var raw = atk.DeviceGet(endpoint);
            var fan = raw & 0xFFFF;

            // G-Helper: values above 120 (×100 RPM) are invalid; a zero read on an
            // unsupported (negative) raw value is invalid too.
            if (fan > 120 || (fan == 0 && raw < 0))
                return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);

            return fan * 100;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"ATK fan read failed; using fallback. [endpoint=0x{endpoint:X8}]", ex);
            return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
        }
    }

    private static async Task<bool> IsAsusMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Vendor?.Contains("ASUS", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for ASUS sensor detection.", ex);
            return false;
        }
    }
}
