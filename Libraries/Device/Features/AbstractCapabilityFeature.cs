using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public abstract class AbstractCapabilityFeature<T>(CapabilityID capabilityID)
    : IFeature<T> where T : struct, Enum, IComparable, IConvertible
{
    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

            if (!Compatibility.IsSupportedLegionMachine(mi))
                return false;

            return mi.Features.Source == MachineInformation.FeatureData.SourceType.CapabilityData && mi.Features[capabilityID];
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"feature-cap-supported-{GetType().Name}-{capabilityID}",
                $"Capability feature support probe failed for {GetType().Name} ({capabilityID}).",
                ex);
            return false;
        }
    }

    public Task<T[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<T>());
    }

    public async Task<T> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state... [feature={GetType().Name}]");

        // GetFeatureValueAsync never throws (returns -1 when unavailable).
        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(capabilityID).ConfigureAwait(false);
        if (value < 0)
            throw ExceptionHelper.WmiFeatureUnavailable(capabilityID, new InvalidOperationException("GetFeatureValue unavailable"));

        cancellationToken.ThrowIfCancellationRequested();

        var result = (T)Enum.ToObject(typeof(T), value);
        if (!Enum.IsDefined(result))
            throw ExceptionHelper.UndefinedValueReceived(result);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result} [feature={GetType().Name}]");

        return result;
    }

    public async Task SetStateAsync(T state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        await WMI.LenovoOtherMethod.SetFeatureValueAsync(capabilityID, Convert.ToInt32(state)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state} [feature={GetType().Name}]");
    }

    public virtual void InvalidateResolution()
    {
    }
}
