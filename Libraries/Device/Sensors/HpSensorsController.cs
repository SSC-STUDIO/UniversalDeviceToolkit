using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

/// <summary>
/// Sensors for HP OMEN/Victus machines: generic (vendor-neutral) snapshot plus
/// HP WMI BIOS readings — fan levels via command 0x2D (values ×100 = RPM) and
/// the BIOS thermal sensor via command 0x23 (°C). Self-disables on non-HP
/// machines or when the BIOS interface is absent.
/// </summary>
public class HpSensorsController(GPUController gpuController, IHpWmiBios bios) : GenericSensorsController(gpuController)
{
    private const uint CmdFanLevel = 0x2D;
    private const uint CmdBiosTemperature = 0x23;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (!bios.IsAvailable || !await IsHpMachineAsync().ConfigureAwait(false))
                return false;

            return await base.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking HP sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadWmiFanSpeedAsync(cpu: true, () => base.GetCpuCurrentFanSpeedAsync());

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadWmiFanSpeedAsync(cpu: false, () => base.GetGpuCurrentFanSpeedAsync());

    protected override async Task<int> GetCpuCurrentTemperatureAsync()
    {
        var (returnCode, data) = bios.Execute(CmdBiosTemperature, [0x01, 0x00, 0x00, 0x00]);
        if (returnCode == 0 && data.Length > 0 && data[0] > 0)
            return data[0];

        return await base.GetCpuCurrentTemperatureAsync().ConfigureAwait(false);
    }

    private async Task<int> ReadWmiFanSpeedAsync(bool cpu, Func<Task<int>> fallback)
    {
        try
        {
            var (returnCode, data) = bios.Execute(CmdFanLevel, [0x00, 0x00, 0x00, 0x00]);
            if (returnCode == 0 && data.Length > 1)
            {
                var level = cpu ? data[0] : data[1];
                if (level > 0)
                    return level * 100; // krpm → RPM (Linux hp-wmi semantics)
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("HP WMI fan read failed; using fallback.", ex);
        }

        return await AwaitWithTimeoutAsync(fallback()).ConfigureAwait(false);
    }

    private static async Task<bool> IsHpMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("HP", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Hewlett", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to read machine information for HP sensor detection.", ex);
            return false;
        }
    }
}
