using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

public class IGPUModeCapabilityFeature : IFeature<IGPUModeState>
{
    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Features.Source == MachineInformation.FeatureData.SourceType.CapabilityData
                   && mi.Features[CapabilityID.IGPUMode];
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("feature-igpu-cap-supported", "IGPUModeCapabilityFeature support probe failed.", ex);
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

        var value = await TryGetFeatureValueAsync(CapabilityID.IGPUMode).ConfigureAwait(false)
                    ?? throw CreateUnavailableException(CapabilityID.IGPUMode);
        if (value < 0)
            throw CreateUnavailableException(CapabilityID.IGPUMode);
        cancellationToken.ThrowIfCancellationRequested();
        var result = (IGPUModeState)value;
        if (!Enum.IsDefined(result))
            throw ExceptionHelper.UndefinedValueReceived(result);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result}");

        return result;
    }

    public async Task SetStateAsync(IGPUModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}...");

        await WMI.LenovoOtherMethod.SetFeatureValueAsync(CapabilityID.IGPUMode, (int)state).ConfigureAwait(false);
        // Success is strictly positive change status; 0 = failed, negative/unavailable = failed.
        var changeStatus = await TryGetFeatureValueAsync(CapabilityID.IGPUModeChangeStatus).ConfigureAwait(false);
        if (changeStatus is null or < 1)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Set state to {state}, but dGPU check failed. [status={changeStatus}]");

            throw new IGPUModeChangeException(state);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }

    public void InvalidateResolution()
    {
    }

    private static async Task<int?> TryGetFeatureValueAsync(CapabilityID capabilityId)
    {
        try
        {
            return await WMI.LenovoOtherMethod.GetFeatureValueAsync(capabilityId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ManagementException or InvalidOperationException)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GetFeatureValue({capabilityId}) is unavailable.", ex);

            return null;
        }
    }

    private static InvalidOperationException CreateUnavailableException(CapabilityID capabilityId) =>
        new($"WMI feature value is unavailable for {capabilityId}.");
}
