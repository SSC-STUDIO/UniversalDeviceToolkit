using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;

public interface IDGPUNotify
{
    public event EventHandler<bool>? Notified;

    Task<bool> IsSupportedAsync();
    Task<bool> IsDGPUAvailableAsync();
    Task NotifyAsync(bool publish = true);
    Task NotifyLaterIfNeededAsync();
    void InvalidateResolution();
}
