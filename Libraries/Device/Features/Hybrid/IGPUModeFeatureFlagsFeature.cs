using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

public class IGPUModeFeatureFlagsFeature : IFeature<IGPUModeState>
{
    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Features.Source == MachineInformation.FeatureData.SourceType.Flags
                   && mi.Features[CapabilityID.IGPUMode];
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("feature-igpu-flags-supported", "IGPUModeFeatureFlagsFeature support probe failed.", ex);
            return false;
        }
    }

    public Task<IGPUModeState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<IGPUModeState>());
    }

    public async Task<IGPUModeState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var flags = await WMI.LenovoOtherMethod.GetDeviceCurrentSupportFeatureAsync().ConfigureAwait(false);

        var result = IGPUModeState.Default;

        if (flags.IsBitSet(0))
            result = IGPUModeState.IGPUOnly;
        if (flags.IsBitSet(1))
            result = IGPUModeState.Auto;


        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result}");

        return result;
    }

    public async Task SetStateAsync(IGPUModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}...");

        if (!Enum.IsDefined(state))
            throw ExceptionHelper.InvalidState();

        var result = await WMI.LenovoOtherMethod.SetDeviceCurrentSupportFeatureAsync(1, (int)state).ConfigureAwait(false);
        if (result < 1)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Set state to {state}, but dGPU check failed. [status={result}]");

            throw new IGPUModeChangeException(state);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }

    public void InvalidateResolution()
    {
    }
}
