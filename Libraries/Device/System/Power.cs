using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;

namespace UniversalDeviceToolkit.Lib.System;

public static class Power
{
    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return PowerAdapterStatus.Connected;

        var adapterConnected = sps.ACLineStatus == 1;
        var acFitForOc = await IsAcFitForOc().ConfigureAwait(false) ?? true;
        var chargingNormally = await IsChargingNormally().ConfigureAwait(false) ?? true;

        return (adapterConnected, acFitForOc && chargingNormally) switch
        {
            (true, false) => PowerAdapterStatus.ConnectedLowWattage,
            (true, _) => PowerAdapterStatus.Connected,
            (false, _) => PowerAdapterStatus.Disconnected,
        };
    }

    public static bool IsBatterySaverEnabled()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return false;

        return sps.SystemStatusFlag == 1;
    }

    public static async Task RestartAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Restarting...");

        await CMD.RunAsync("shutdown", "/r /t 0").ConfigureAwait(false);
    }

    private static async Task<bool?> IsAcFitForOc()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.IsACFitForOCAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("power-ac-fit-oc", "IsACFitForOC WMI probe failed.", ex);
            return null;
        }
    }

    private static async Task<bool?> IsChargingNormally()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.GetPowerChargeModeAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("power-charge-mode", "GetPowerChargeMode WMI probe failed.", ex);
            return null;
        }
    }
}
