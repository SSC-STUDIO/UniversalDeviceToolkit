using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for Alienware / Dell G-Series machines: generic snapshot plus AWCC
/// readings — temperatures via Thermal_Information op 0x04 (°C; sensor ids
/// 0x01 CPU, 0x06 GPU) and fan RPM via op 0x05 (fan ids 0x32 CPU, 0x33 GPU).
/// Self-disables on non-Dell machines or when no AWCC interface answers.
/// </summary>
public class AlienwareSensorsController(GPUController gpuController, IAlienwareWmi wmi) : GenericSensorsController(gpuController)
{
    private const string ThermalInformation = "Thermal_Information";

    private const byte OpGetTemperature = 0x04;
    private const byte OpGetFanRpm = 0x05;

    private const byte CpuTempSensor = 0x01;
    private const byte GpuTempSensor = 0x06;
    private const byte CpuFan = 0x32;
    private const byte GpuFan = 0x33;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!wmi.IsAvailable || !await IsDellMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking Alienware sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        ReadWmiAsync(CpuTempSensor, OpGetTemperature, () => base.GetCpuCurrentTemperatureAsync());

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadWmiAsync(CpuFan, OpGetFanRpm, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadWmiAsync(GpuFan, OpGetFanRpm, () => base.GetGpuCurrentFanSpeedAsync());

    private async Task<int> ReadWmiAsync(byte resourceId, byte operation, Func<Task<int>> fallback)
    {
        try
        {
            var value = wmi.Execute(ThermalInformation, operation, resourceId);
            if (value > 0)
                return value;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Alienware WMI read failed; using fallback. [resource=0x{resourceId:X2}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private static async Task<bool> IsDellMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Dell", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Alienware", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for Alienware sensor detection.", ex);
            return false;
        }
    }
}
