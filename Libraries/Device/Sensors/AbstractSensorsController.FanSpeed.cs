using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public abstract partial class AbstractSensorsController
{
    private static bool _loggedControllerFanPipeline;

    protected Task<int> ReadFanSpeedAsync(string fanName, params FanSpeedSourceReader[] readers) =>
        ReadFanSpeedCoreAsync(fanName, readers);

    private async Task<int> ReadFanSpeedCoreAsync(string fanName, FanSpeedSourceReader[] readers)
    {
        if (!_loggedControllerFanPipeline)
        {
            _loggedControllerFanPipeline = true;
            Log.Instance.TraceOnce(
                "sensors-fan-controller",
                $"Fan RPM pipeline active for {GetType().Name}.");
        }

        var reading = await FanSpeedReadCoordinator.ReadAsync(fanName, readers).ConfigureAwait(false);
        if (reading.IsAvailable)
            return reading.Rpm;

        return await TryLibreHardwareMonitorFanOnlyAsync(fanName).ConfigureAwait(false);
    }

    protected static FanSpeedSourceReader FanMethodSource(params int[] fanIds) =>
        new(FanSpeedSource.LenovoFanMethod, async () =>
        {
            var (success, rpm) = await WMI.LenovoFanMethod.TryFanGetCurrentFanSpeedAsync(fanIds)
                .ConfigureAwait(false);
            return (success, rpm);
        });

    protected static FanSpeedSourceReader GamezoneCpuFanSource() =>
        new(FanSpeedSource.LenovoGamezone, async () =>
        {
            var (success, rpm) = await WMI.LenovoGameZoneData.TryGetCpuFanSpeedAsync()
                .ConfigureAwait(false);
            return (success, rpm);
        });

    protected static FanSpeedSourceReader GamezoneGpuFanSource() =>
        new(FanSpeedSource.LenovoGamezone, async () =>
        {
            var (success, rpm) = await WMI.LenovoGameZoneData.TryGetGpuFanSpeedAsync()
                .ConfigureAwait(false);
            return (success, rpm);
        });

    /// <summary>
    /// Capability fan RPM: only positive values are trusted; bare 0 is treated as unavailable.
    /// </summary>
    protected static FanSpeedSourceReader CapabilityFanSource(CapabilityID id) =>
        new(FanSpeedSource.LenovoCapability, async () =>
        {
            try
            {
                var value = await WMI.LenovoOtherMethod.TryGetFeatureValueAsync(id).ConfigureAwait(false);
                return value > 0 ? (true, value) : (false, -1);
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    $"sensors-fan-capability-{id}",
                    $"Capability fan read failed for {id}.",
                    ex);
                return (false, -1);
            }
        });

    private async Task<int> TryLibreHardwareMonitorFanOnlyAsync(string fanName)
    {
        try
        {
            if (IoCContainer.TryResolve<SensorsGroupController>() is not { } sensorsGroupController)
                return -1;

            if (!await sensorsGroupController.EnsureFanSensorsAvailableAsync().ConfigureAwait(false))
                return -1;

            await sensorsGroupController.UpdateAsync().ConfigureAwait(false);

            var rpm = fanName switch
            {
                "CPU" => await sensorsGroupController.GetCpuFanSpeedAsync().ConfigureAwait(false),
                "GPU" => await sensorsGroupController.GetGpuFanSpeedAsync().ConfigureAwait(false),
                _ => -1f
            };

            var normalized = NormalizeLibreHardwareMonitorMetric(rpm);
            if (normalized >= 0)
            {
                Log.Instance.TraceOnce(
                    $"fan-source-{fanName}-LibreHardwareMonitor",
                    $"{fanName} fan RPM source selected: LibreHardwareMonitor.");
            }

            return normalized;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"sensors-fan-lhm-{fanName}",
                $"{fanName} fan-only LibreHardwareMonitor fallback failed.",
                ex);
            return -1;
        }
    }
}