using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;

public class DGPUNotify(DGPUGamezoneNotify gamezoneNotify, DGPUCapabilityNotify capabilityNotify, DGPUFeatureFlagsNotify featureFlagsNotify) : IDGPUNotify
{
    private readonly AsyncLock _lock = new();

    private bool _resolved;
    private IDGPUNotify? _dgpuNotify;

    private DGPUCapabilityNotify CapabilityNotify => capabilityNotify;
    private DGPUFeatureFlagsNotify FeatureFlagsNotify => featureFlagsNotify;
    private DGPUGamezoneNotify GamezoneNotify => gamezoneNotify;

    private bool _experimentalGPUWorkingMode;

    public bool ExperimentalGPUWorkingMode
    {
        get => _experimentalGPUWorkingMode;
        set
        {
            if (_experimentalGPUWorkingMode == value)
                return;

            _experimentalGPUWorkingMode = value;
            InvalidateResolution();
        }
    }

    public event EventHandler<bool>? Notified
    {
        add
        {
            CapabilityNotify.Notified += value;
            FeatureFlagsNotify.Notified += value;
            GamezoneNotify.Notified += value;
        }
        remove
        {
            CapabilityNotify.Notified -= value;
            FeatureFlagsNotify.Notified -= value;
            GamezoneNotify.Notified -= value;
        }
    }

    public async Task<bool> IsSupportedAsync()
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false);
        if (dgpuNotify is null)
            return false;
        return await dgpuNotify.IsSupportedAsync().ConfigureAwait(false);
    }

    public async Task<bool> IsDGPUAvailableAsync()
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false) ?? throw ExceptionHelper.NoSupportedFeature(GetType().Name);
        return await dgpuNotify.IsDGPUAvailableAsync().ConfigureAwait(false);
    }

    public async Task NotifyAsync(bool publish = true)
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false) ?? throw ExceptionHelper.NoSupportedFeature(GetType().Name);
        await dgpuNotify.NotifyAsync(publish).ConfigureAwait(false);
    }

    public async Task NotifyLaterIfNeededAsync()
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false) ?? throw ExceptionHelper.NoSupportedFeature(GetType().Name);
        await dgpuNotify.NotifyLaterIfNeededAsync().ConfigureAwait(false);
    }

    private async Task<IDGPUNotify?> ResolveAsync()
    {
        if (ExperimentalGPUWorkingMode)
        {
            if (await CapabilityNotify.IsSupportedAsync().ConfigureAwait(false))
                return CapabilityNotify;

            if (await FeatureFlagsNotify.IsSupportedAsync().ConfigureAwait(false))
                return FeatureFlagsNotify;

            return null;
        }

        if (await GamezoneNotify.IsSupportedAsync().ConfigureAwait(false))
            return GamezoneNotify;

        if (await CapabilityNotify.IsSupportedAsync().ConfigureAwait(false))
            return CapabilityNotify;

        if (await FeatureFlagsNotify.IsSupportedAsync().ConfigureAwait(false))
            return FeatureFlagsNotify;

        return null;
    }

    public void InvalidateResolution()
    {
        using (_lock.Lock())
        {
            _resolved = false;
            _dgpuNotify = null;
        }
    }

    internal IDGPUNotify? ResolvedBackendForTests => _dgpuNotify;

    private async Task<IDGPUNotify?> ResolveInternalAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_resolved)
                return _dgpuNotify;

            _dgpuNotify = await ResolveAsync().ConfigureAwait(false);
            _resolved = true;
            return _dgpuNotify;
        }
    }
}
