using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace UniversalDeviceToolkit.Lib.Controllers;

public partial class WindowsPowerModeController(ApplicationSettings settings, IMainThreadDispatcher mainThreadDispatcher) : IDisposable
{
    private const string POWER_SCHEMES_HIVE = "HKEY_LOCAL_MACHINE";
    private const string POWER_SCHEMES_SUBKEY = "SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes";
    private const string ACTIVE_OVERLAY_AC_POWER_SCHEME_KEY = "ActiveOverlayAcPowerScheme";
    private const string ACTIVE_OVERLAY_DC_POWER_SCHEME_KEY = "ActiveOverlayDcPowerScheme";

    private static readonly Guid BestPowerEfficiency = Guid.Parse("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid BestPerformance = Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238");

    private readonly ThrottleLastDispatcher _dispatcher = new(TimeSpan.FromSeconds(2), nameof(WindowsPowerModeController));

    public async Task SetPowerModeAsync(PowerModeState powerModeState)
    {
        if (settings.Store.PowerModeMappingMode is not PowerModeMappingMode.WindowsPowerMode)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring Windows power mode (mode={settings.Store.PowerModeMappingMode})");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting Windows power mode: {powerModeState}");

        Log.Instance.Info($"Setting Windows power mode: {powerModeState}");

        var powerMode = settings.Store.PowerModes.GetValueOrDefault(powerModeState, WindowsPowerMode.Balanced);
        var powerModeGuid = GuidForWindowsPowerMode(powerMode);

        if (Power.IsBatterySaverEnabled())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery saver enabled - skipping");
            return;
        }

        await _dispatcher.DispatchAsync(() =>
        {
            ActivateDefaultPowerPlanIfNeeded();

            mainThreadDispatcher.Dispatch(() =>
            {
                PowerSetActiveOverlayScheme(powerModeGuid);
            });

            try
            {
                UpdateRegistry(powerModeGuid);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Warning($"Failed to update registry", ex);
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Windows power mode set to {powerMode}");

        Log.Instance.Info($"Windows power mode set to {powerMode} [controller={nameof(WindowsPowerModeController)}]");
    }

    private static void UpdateRegistry(Guid guid)
    {
        Registry.SetValue(POWER_SCHEMES_HIVE, POWER_SCHEMES_SUBKEY, ACTIVE_OVERLAY_AC_POWER_SCHEME_KEY, guid, true);
        Registry.SetValue(POWER_SCHEMES_HIVE, POWER_SCHEMES_SUBKEY, ACTIVE_OVERLAY_DC_POWER_SCHEME_KEY, guid, true);
    }

    private static Guid GuidForWindowsPowerMode(WindowsPowerMode windowsPowerMode) => windowsPowerMode switch
    {
        WindowsPowerMode.BestPowerEfficiency => BestPowerEfficiency,
        WindowsPowerMode.BestPerformance => BestPerformance,
        _ => Guid.Empty
};

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    _dispatcher?.Dispose();
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"WindowsPowerModeController disposal error", ex);
                }
            }
            _disposed = true;
        }
    }

    private static unsafe void ActivateDefaultPowerPlanIfNeeded()
    {
        if (PInvoke.PowerGetActiveScheme(null, out var guid) != WIN32_ERROR.ERROR_SUCCESS)
            PInvokeExtensions.ThrowIfWin32Error("PowerGetActiveScheme");

        if (PowerConstants.DefaultPowerPlan == *guid)
            return;

        if (PInvoke.PowerSetActiveScheme(null, PowerConstants.DefaultPowerPlan) != WIN32_ERROR.ERROR_SUCCESS)
            PInvokeExtensions.ThrowIfWin32Error("PowerSetActiveScheme");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Default power plan activated");
    }

    [LibraryImport("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
    private static partial uint PowerSetActiveOverlayScheme(Guid guid);
}
