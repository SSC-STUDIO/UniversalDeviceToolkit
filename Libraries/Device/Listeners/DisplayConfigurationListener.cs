using System;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Listeners;

public class DisplayConfigurationListener : IListener<DisplayConfigurationListener.ChangedEventArgs>, IDisposable
{
    public class ChangedEventArgs : EventArgs
    {
        public bool? HDR { get; init; }
    }

    private bool _started;
    private bool _disposed;

    public bool? IsHDROn { get; private set; }

    public event EventHandler<ChangedEventArgs>? Changed;

    public Task StartAsync()
    {
        if (_started || _disposed)
            return Task.CompletedTask;

        IsHDROn = GetHDRStatus();

        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        _started = true;

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCore();
        return Task.CompletedTask;
    }

    private void StopCore()
    {
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        _started = false;

        IsHDROn = null;
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received.");

        InternalDisplay.SetNeedsRefresh();

        var previousIsHDROn = IsHDROn;
        IsHDROn = GetHDRStatus();
        var changed = previousIsHDROn != IsHDROn;

        Changed?.Invoke(this, new() { HDR = changed ? IsHDROn : null });
    }

    private static bool? GetHDRStatus()
    {
        try
        {
            return Displays.Get().FirstOrDefault()?.GetAdvancedColorInfo().AdvancedColorEnabled;
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Failed to get HDR status. Assuming unavailable.", ex);
            return null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            StopCore();
        }

        _disposed = true;
    }
}
