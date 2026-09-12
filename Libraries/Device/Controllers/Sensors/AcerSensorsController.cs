using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for Acer Predator/Nitro machines: generic snapshot plus WMID Gaming
/// sys-info readings (method 5, command 0x0001, sensor ids CPU temp 0x01, CPU
/// fan 0x02, GPU fan 0x06, GPU temp 0x0A; value at (gmOutput &gt;&gt; 8) &amp; 0xFFFF,
/// °C / RPM). Self-disables on non-Acer machines or when the interface is absent.
/// </summary>
public class AcerSensorsController(GPUController gpuController, IAcerWmi wmi) : GenericSensorsController(gpuController)
{
    private const string GetGamingSysInfo = "GetGamingSysInfo";

    private const uint ReadCommand = 0x0001;
    private const uint CpuTempSensor = 0x01;
    private const uint CpuFanSensor = 0x02;
    private const uint GpuFanSensor = 0x06;
    private const uint GpuTempSensor = 0x0A;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!wmi.IsAvailable || !await IsAcerMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking Acer sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        ReadWmiSensorAsync(CpuTempSensor, () => base.GetCpuCurrentTemperatureAsync());

    protected override Task<int> GetGpuCurrentTemperatureAsync() =>
        ReadWmiSensorAsync(GpuTempSensor, () => base.GetGpuCurrentTemperatureAsync());

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadWmiSensorAsync(CpuFanSensor, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadWmiSensorAsync(GpuFanSensor, () => base.GetGpuCurrentFanSpeedAsync());

    private async Task<int> ReadWmiSensorAsync(uint sensorId, Func<Task<int>> fallback)
    {
        try
        {
            var (ok, output) = wmi.Execute(GetGamingSysInfo, ReadCommand | (sensorId << 8));
            if (ok)
            {
                var value = (int)((output >> 8) & 0xFFFF);
                if (value > 0)
                    return value;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Acer WMI sensor read failed; using fallback. [sensor=0x{sensorId:X2}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private static async Task<bool> IsAcerMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Acer", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Predator", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Nitro", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for Acer sensor detection.", ex);
            return false;
        }
    }
}
