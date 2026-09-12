using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public abstract class AbstractLenovoLightingFeature<T>(int lightingID, int controlInterface, int type) : IFeature<T> where T : struct, Enum, IComparable
{
    public bool ForceDisable { get; set; }

    public virtual async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ForceDisable)
            return false;

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

            if (!Compatibility.IsSupportedLegionMachine(mi))
                return false;

            if (mi.Properties.IsExcludedFromLenovoLighting)
                return false;

            var isSupported = await WMI.LenovoLightingData.ExistsAsync(lightingID, controlInterface, type).ConfigureAwait(false);

            if (!isSupported)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Control interface not found [feature={GetType().Name}]");
                return false;
            }

            _ = await GetStateAsync(cancellationToken).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Supported [feature={GetType().Name}]");

            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Failed to check support [feature={GetType().Name}]", ex);

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

        var (stateType, level) = await WMI.LenovoLightingMethod.GetLightingCurrentStatusAsync(lightingID).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = FromInternal(stateType, level);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result} [feature={GetType().Name}]");

        return result;
    }

    public async Task SetStateAsync(T state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        var (stateType, level) = ToInternal(state);

        await WMI.LenovoLightingMethod.SetLightingCurrentStatusAsync(lightingID, stateType, level).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state} [feature={GetType().Name}]");
    }

    public virtual void InvalidateResolution()
    {
    }

    protected abstract T FromInternal(int stateType, int level);

    protected abstract (int stateType, int level) ToInternal(T state);
}
