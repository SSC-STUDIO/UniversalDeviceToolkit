using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;

namespace UniversalDeviceToolkit.Lib.System;

public static class Power
{
    private static readonly TimeSpan AcAdapterFlagCacheDuration = TimeSpan.FromSeconds(5);
    private static readonly SemaphoreSlim AcAdapterFlagGate = new(1, 1);
    private static AcAdapterFlagCache? _acAdapterFlagCache;

    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return PowerAdapterStatus.Connected;

        var adapterConnected = sps.ACLineStatus == 1;
        if (!adapterConnected)
        {
            _acAdapterFlagCache = null;
            return PowerAdapterStatus.Disconnected;
        }

        try
        {
            var flags = await GetCachedAcAdapterFlagsAsync().ConfigureAwait(false);
            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace(
                    $"AC fit = {FormatFlag(flags.AcFitForOc)}, charge mode = {FormatFlag(flags.PowerChargeMode)}");
            }

            return ResolvePowerAdapterStatus(
                adapterConnected: true,
                acFitForOc: FlagMeansSufficient(flags.AcFitForOc),
                chargingNormally: FlagMeansSufficient(flags.PowerChargeMode));
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("power-ac-adapter-flags", "AC adapter flag probe failed.", ex);
            return PowerAdapterStatus.Connected;
        }
    }

    /// <summary>
    /// A connected adapter is low-wattage only when a successful probe says so.
    /// Null means the probe did not return a value and must not raise the warning.
    /// Lenovo reports sufficiency as 1 from IsACFitForOC and GetPowerChargeMode.
    /// </summary>
    internal static PowerAdapterStatus ResolvePowerAdapterStatus(
        bool adapterConnected,
        bool? acFitForOc,
        bool? chargingNormally)
    {
        if (!adapterConnected)
            return PowerAdapterStatus.Disconnected;

        var sufficient = (acFitForOc ?? true) && (chargingNormally ?? true);
        return sufficient
            ? PowerAdapterStatus.Connected
            : PowerAdapterStatus.ConnectedLowWattage;
    }

    private static bool? FlagMeansSufficient(int? flag) => flag is int value ? value == 1 : null;

    private static string FormatFlag(int? flag) => flag?.ToString() ?? "unknown";

    private static async Task<WMI.LenovoGameZoneData.AcAdapterFlagRead> GetCachedAcAdapterFlagsAsync()
    {
        var now = DateTime.UtcNow.Ticks;
        var cached = _acAdapterFlagCache;
        if (cached is not null && cached.ExpiresAtUtcTicks > now)
            return cached.Flags;

        await AcAdapterFlagGate.WaitAsync().ConfigureAwait(false);
        try
        {
            now = DateTime.UtcNow.Ticks;
            cached = _acAdapterFlagCache;
            if (cached is not null && cached.ExpiresAtUtcTicks > now)
                return cached.Flags;

            var flags = await WMI.LenovoGameZoneData.ReadAcAdapterFlagsAsync().ConfigureAwait(false);
            _acAdapterFlagCache = new AcAdapterFlagCache(
                DateTime.UtcNow.Ticks + AcAdapterFlagCacheDuration.Ticks,
                flags);
            return flags;
        }
        finally
        {
            AcAdapterFlagGate.Release();
        }
    }

    private sealed class AcAdapterFlagCache(
        long expiresAtUtcTicks,
        WMI.LenovoGameZoneData.AcAdapterFlagRead flags)
    {
        public long ExpiresAtUtcTicks { get; } = expiresAtUtcTicks;
        public WMI.LenovoGameZoneData.AcAdapterFlagRead Flags { get; } = flags;
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
}
