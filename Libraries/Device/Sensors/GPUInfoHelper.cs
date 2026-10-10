using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

internal static class GPUInfoHelper
{
    /// <summary>
    /// Returns the GPU power consumption in watts, or -1 if unavailable.
    /// </summary>
    internal static int GetWattage(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NVAPI.GetWattage(gpu);
        }
        catch (global::System.Exception ex)
        {
            Log.Instance.TraceOnce("gpu-info-wattage", "GPUInfoHelper.GetWattage failed.", ex);
            return -1;
        }
    }

    /// <summary>
    /// Returns the GPU voltage in volts, or 0 if unavailable.
    /// </summary>
    internal static double GetVoltage(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NVAPI.GetVoltage(gpu);
        }
        catch (global::System.Exception ex)
        {
            Log.Instance.TraceOnce("gpu-info-voltage", "GPUInfoHelper.GetVoltage failed.", ex);
            return 0;
        }
    }

    /// <summary>
    /// Returns GPU wattage from power topology (fallback), or -1 if unavailable.
    /// </summary>
    internal static int GetWattageFromPowerTopology(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NVAPI.GetWattageFromPowerTopology(gpu);
        }
        catch (global::System.Exception ex)
        {
            Log.Instance.TraceOnce("gpu-info-power-topology", "GPUInfoHelper.GetWattageFromPowerTopology failed.", ex);
            return -1;
        }
    }
}
