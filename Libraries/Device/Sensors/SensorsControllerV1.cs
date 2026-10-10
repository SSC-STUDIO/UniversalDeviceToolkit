using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public class SensorsControllerV1(GPUController gpuController) : AbstractSensorsController(gpuController)
{
    private const int CPU_SENSOR_ID = 3;
    private const int GPU_SENSOR_ID = 4;
    private const int CPU_FAN_ID = 0;
    private const int GPU_FAN_ID = 1;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

            if (!Compatibility.IsSupportedLegionMachine(mi))
                return false;

            var result = await WMI.LenovoFanTableData.ExistsAsync(0, CPU_FAN_ID).ConfigureAwait(false);
            result &= await WMI.LenovoFanTableData.ExistsAsync(0, GPU_FAN_ID).ConfigureAwait(false);

            if (result)
                result = await CanReadSensorSnapshotAsync().ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-v1-supported", "SensorsControllerV1 support probe failed.", ex);
            return false;
        }
    }

    protected override async Task<int> GetCpuCurrentTemperatureAsync()
    {
        var t = await WMI.LenovoFanMethod.FanGetCurrentSensorTemperatureAsync(CPU_SENSOR_ID).ConfigureAwait(false);
        if (t < 1)
            return -1;
        return t;
    }

    protected override async Task<int> GetGpuCurrentTemperatureAsync()
    {
        var t = await WMI.LenovoFanMethod.FanGetCurrentSensorTemperatureAsync(GPU_SENSOR_ID).ConfigureAwait(false);
        if (t < 1)
            return -1;
        return t;
    }

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadFanSpeedAsync("CPU", FanMethodSource(CPU_FAN_ID), GamezoneCpuFanSource(), CapabilityFanSource(CapabilityID.CpuCurrentFanSpeed));

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadFanSpeedAsync("GPU", FanMethodSource(GPU_FAN_ID), GamezoneGpuFanSource(), CapabilityFanSource(CapabilityID.GpuCurrentFanSpeed));
    protected override Task<int> GetCpuMaxFanSpeedAsync() => WMI.LenovoFanMethod.GetDefaultFanMaxSpeedAsync(0, CPU_FAN_ID);

    protected override Task<int> GetGpuMaxFanSpeedAsync() => WMI.LenovoFanMethod.GetDefaultFanMaxSpeedAsync(0, GPU_FAN_ID);
}
