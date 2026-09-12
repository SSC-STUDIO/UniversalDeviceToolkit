using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Features.Clevo;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Hardware sensors for Clevo / Sager / Schenker / Tuxedo barebones:
/// reads CPU/GPU temperatures (0x07 / 0xCD or 0x68 / 0x80) and fan RPMs (0xD0-0xD3).
/// Self-disables on non-Clevo hardware or when EC channel is unavailable.
/// </summary>
public class ClevoSensorsController(GPUController gpuController, IEcChannel ec) : GenericSensorsController(gpuController)
{
    private const byte CpuTempAddressPrimary = 0x07;
    private const byte CpuTempAddressSecondary = 0x68;
    private const byte GpuTempAddressPrimary = 0xCD;
    private const byte GpuTempAddressSecondary = 0x80;

    private const byte CpuFanRpmHigh = 0xD0;
    private const byte GpuFanRpmHigh = 0xD2;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!ec.IsAvailable || !await ClevoPowerModeFeature.IsClevoMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking Clevo sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        ReadTempAsync(CpuTempAddressPrimary, CpuTempAddressSecondary, () => base.GetCpuCurrentTemperatureAsync());

    protected override Task<int> GetGpuCurrentTemperatureAsync() =>
        ReadTempAsync(GpuTempAddressPrimary, GpuTempAddressSecondary, () => base.GetGpuCurrentTemperatureAsync());

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadRpmAsync(CpuFanRpmHigh, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadRpmAsync(GpuFanRpmHigh, () => base.GetGpuCurrentFanSpeedAsync());

    private async Task<int> ReadTempAsync(byte primaryAddress, byte secondaryAddress, Func<Task<int>> fallback)
    {
        try
        {
            if (ec.TryRead(primaryAddress, out var val1) && val1 is > 0 and < 125)
                return val1;

            if (ec.TryRead(secondaryAddress, out var val2) && val2 is > 0 and < 125)
                return val2;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Clevo EC temp read failed; using fallback.", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private async Task<int> ReadRpmAsync(byte highAddress, Func<Task<int>> fallback)
    {
        try
        {
            if (ec.TryRead(highAddress, out var high) &&
                ec.TryRead((byte)(highAddress + 1), out var low))
            {
                var rpm = (high << 8) | low;
                if (rpm is >= 200 and <= 12000)
                    return rpm;

                if (rpm is > 0 and < 50000)
                    return 480000 / rpm;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Clevo EC fan read failed; using fallback. [address=0x{highAddress:X2}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }
}
