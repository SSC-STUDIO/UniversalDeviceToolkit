using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Features.Tongfang;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Hardware sensors for Tongfang / Uniwill / MECHREVO / Hasee barebones:
/// reads CPU/GPU temperatures and fan speeds directly from EC RAM.
/// Temperature addresses: CPU 0x07 (fallback 0x68), GPU 0xCD (fallback 0x80).
/// Fan tachometer counters: CPU 0x08-0x09 (16-bit), GPU 0x0A-0x0B (16-bit).
/// Self-disables on non-Tongfang/MECHREVO machines or when EC channel is unavailable.
/// </summary>
public class TongfangSensorsController(GPUController gpuController, IEcChannel ec) : GenericSensorsController(gpuController)
{
    private const byte CpuTempAddressPrimary = 0x07;
    private const byte CpuTempAddressSecondary = 0x68;
    private const byte GpuTempAddressPrimary = 0xCD;
    private const byte GpuTempAddressSecondary = 0x80;

    private const byte CpuFanHigh = 0x08;
    private const byte GpuFanHigh = 0x0A;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!ec.IsAvailable || !await TongfangPowerModeFeature.IsTongfangMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking Tongfang sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        ReadTempAsync(CpuTempAddressPrimary, CpuTempAddressSecondary, () => base.GetCpuCurrentTemperatureAsync());

    protected override Task<int> GetGpuCurrentTemperatureAsync() =>
        ReadTempAsync(GpuTempAddressPrimary, GpuTempAddressSecondary, () => base.GetGpuCurrentTemperatureAsync());

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadRpmAsync(CpuFanHigh, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadRpmAsync(GpuFanHigh, () => base.GetGpuCurrentFanSpeedAsync());

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
                Log.Instance.Trace($"Tongfang EC temp read failed; using fallback.", ex);
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
                var raw = (high << 8) | low;
                if (raw > 0)
                {
                    // If reading is directly in RPM range (e.g. 500..8000), use direct RPM
                    if (raw is >= 300 and <= 12000)
                        return raw;

                    // Otherwise if tachometer pulse period counter, convert via 480000 / raw
                    if (raw is > 0 and < 50000)
                        return 480000 / raw;
                }
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Tongfang EC fan read failed; using fallback. [address=0x{highAddress:X2}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }
}
