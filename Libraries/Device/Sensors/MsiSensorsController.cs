using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for MSI machines: generic snapshot plus EC readings — CPU/GPU temps
/// (0x68 / 0x80, direct °C) and fan tachometer counters (0xC8–0xCB, 16-bit
/// big-endian; RPM = 480000 / count, count 0 = stopped). Self-disables on
/// non-MSI machines or when no EC channel is present.
/// </summary>
public class MsiSensorsController(GPUController gpuController, IEcChannel ec) : GenericSensorsController(gpuController)
{
    private const byte CpuTempAddress = 0x68;
    private const byte GpuTempAddress = 0x80;
    private const byte CpuFanCounterHigh = 0xC8;
    private const byte GpuFanCounterHigh = 0xCA;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!ec.IsAvailable || !await IsMsiMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking MSI sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        ReadTempAsync(CpuTempAddress, () => base.GetCpuCurrentTemperatureAsync());

    protected override Task<int> GetGpuCurrentTemperatureAsync() =>
        ReadTempAsync(GpuTempAddress, () => base.GetGpuCurrentTemperatureAsync());

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadRpmAsync(CpuFanCounterHigh, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadRpmAsync(GpuFanCounterHigh, () => base.GetGpuCurrentFanSpeedAsync());

    private async Task<int> ReadTempAsync(byte address, Func<Task<int>> fallback)
    {
        try
        {
            if (ec.TryRead(address, out var value) && value > 0)
                return value;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"MSI EC temp read failed; using fallback. [address=0x{address:X2}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private async Task<int> ReadRpmAsync(byte counterHighAddress, Func<Task<int>> fallback)
    {
        try
        {
            if (ec.TryRead(counterHighAddress, out var high) &&
                ec.TryRead((byte)(counterHighAddress + 1), out var low))
            {
                var count = (high << 8) | low;
                if (count > 0)
                    return 480000 / count; // kernel & MControlCenter formula
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"MSI EC fan read failed; using fallback. [address=0x{counterHighAddress:X2}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private static async Task<bool> IsMsiMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("MSI", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for MSI sensor detection.", ex);
            return false;
        }
    }
}
