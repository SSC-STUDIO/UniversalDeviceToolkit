using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public class SensorsControllerV5(GPUController gpuController) : AbstractSensorsController(gpuController)
{
    private const int CPU_SENSOR_ID = 1;
    private const int GPU_SENSOR_ID = 5;
    private const int PCH_SENSOR_ID = 4;
    private const int CPU_FAN_ID = 1;
    private const int GPU_FAN_ID = 2;
    private const int PCH_FAN_ID = 4;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var result = await WMI.LenovoFanTableData.ExistsAsync(CPU_SENSOR_ID, CPU_FAN_ID).ConfigureAwait(false);
            result &= await WMI.LenovoFanTableData.ExistsAsync(GPU_SENSOR_ID, GPU_FAN_ID).ConfigureAwait(false);

            if (result)
                result = await CanReadSensorSnapshotAsync().ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Error checking support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override async Task<int> GetCpuCurrentTemperatureAsync()
    {
        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.CpuCurrentTemperature).ConfigureAwait(false);
        return value < 1 ? -1 : value;
    }

    protected override async Task<int> GetGpuCurrentTemperatureAsync()
    {
        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.GpuCurrentTemperature).ConfigureAwait(false);
        return value < 1 ? -1 : value;
    }
    protected override async Task<int> GetPchCurrentTemperatureAsync()
    {
        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.PchCurrentTemperature).ConfigureAwait(false);
        return value < 1 ? -1 : value;
    }

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadFanSpeedAsync("CPU", CapabilityFanSource(CapabilityID.CpuCurrentFanSpeed), GamezoneCpuFanSource(), FanMethodSource(CPU_FAN_ID, 0));

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadFanSpeedAsync("GPU", CapabilityFanSource(CapabilityID.GpuCurrentFanSpeed), GamezoneGpuFanSource(), FanMethodSource(GPU_FAN_ID, 1));

    protected override Task<int> GetPchCurrentFanSpeedAsync() =>
        ReadFanSpeedAsync("PCH", CapabilityFanSource(CapabilityID.PchCurrentFanSpeed), FanMethodSource(PCH_FAN_ID));

    protected override Task<int> GetCpuMaxFanSpeedAsync() => WMI.LenovoFanMethod.GetCurrentFanMaxSpeedAsync(CPU_SENSOR_ID, CPU_FAN_ID);

    protected override Task<int> GetGpuMaxFanSpeedAsync() => WMI.LenovoFanMethod.GetCurrentFanMaxSpeedAsync(GPU_SENSOR_ID, GPU_FAN_ID);
    protected override Task<int> GetPchMaxFanSpeedAsync() => WMI.LenovoFanMethod.GetCurrentFanMaxSpeedAsync(PCH_SENSOR_ID, PCH_FAN_ID);
}
