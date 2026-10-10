using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public class HDRFeature : IFeature<HDRState>
{
    public Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Checking HDR support...");

            var display = InternalDisplay.Get();
            if (display is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Built in display not found");

                return Task.FromResult(false);
            }

            var isSupported = display.GetAdvancedColorInfo().AdvancedColorSupported;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"HDR support: {isSupported}");

            return Task.FromResult(isSupported);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Failed to check HDR support", ex);

            return Task.FromResult(false);
        }
    }

    public Task<bool> IsHdrBlockedAsync()
    {
        var display = InternalDisplay.Get() ?? throw ExceptionHelper.BuiltInDisplayNotFound();

        var result = display.GetAdvancedColorInfo().AdvancedColorForceDisabled;
        return Task.FromResult(result);
    }

    public Task<HDRState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<HDRState>());
    }

    public Task<HDRState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting current HDR state...");

        var display = InternalDisplay.Get() ?? throw ExceptionHelper.BuiltInDisplayNotFound();

        var result = display.GetAdvancedColorInfo().AdvancedColorEnabled ? HDRState.On : HDRState.Off;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"HDR is {result}");

        return Task.FromResult(result);
    }

    public async Task SetStateAsync(HDRState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentState = await GetStateAsync(cancellationToken).ConfigureAwait(false);

        if (currentState == state)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"HDR already set to {state}");
            return;
        }

        var display = InternalDisplay.Get() ?? throw ExceptionHelper.BuiltInDisplayNotFound();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting display HDR to {state}");

        display.SetAdvancedColorState(state == HDRState.On);
    }

    public void InvalidateResolution()
    {
    }
}
