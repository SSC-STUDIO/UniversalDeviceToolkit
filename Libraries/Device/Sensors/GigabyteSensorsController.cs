using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for Gigabyte AORUS/AERO machines: generic snapshot plus the vendor
/// GB_WMIACPI_Get readings (getCpuTemp/getGpuTemp1 °C, getRpm1/getRpm2 RPM).
/// Phase 1 is sensors-only: Gigabyte has no platform power profile — its fan
/// modes and GPU boost live behind raw WMBD calls not exposed as friendly WMI
/// classes, so power modes stay out until the write semantics are proven safe.
/// Self-disables when the vendor MOF classes are not installed.
/// </summary>
public class GigabyteSensorsController(GPUController gpuController, IGigabyteWmi wmi) : GenericSensorsController(gpuController)
{
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!wmi.IsAvailable || !await IsGigabyteMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking Gigabyte sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        ReadWmiAsync("getCpuTemp", () => base.GetCpuCurrentTemperatureAsync());

    protected override Task<int> GetGpuCurrentTemperatureAsync() =>
        ReadWmiAsync("getGpuTemp1", () => base.GetGpuCurrentTemperatureAsync());

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadWmiAsync("getRpm1", () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadWmiAsync("getRpm2", () => base.GetGpuCurrentFanSpeedAsync());

    private async Task<int> ReadWmiAsync(string methodName, Func<Task<int>> fallback)
    {
        try
        {
            var value = wmi.GetValue(methodName);
            if (value > 0)
                return value;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Gigabyte WMI read failed; using fallback. [method={methodName}]", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private static async Task<bool> IsGigabyteMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("AORUS", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("AERO", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for Gigabyte sensor detection.", ex);
            return false;
        }
    }
}
