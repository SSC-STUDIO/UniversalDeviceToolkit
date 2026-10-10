using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public abstract class AbstractWmiFeature<T>(Func<Task<int>> getValue, Func<int, Task> setValue, Func<Task<int>>? isSupported = null, int offset = 0)
    : IFeature<T> where T : struct, Enum, IComparable
{
    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (isSupported is null)
                return true;

            return await isSupported().ConfigureAwait(false) > 0;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to check WMI feature support [feature={GetType().Name}]", ex);
            return false;
        }
    }

    public virtual Task<T[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<T>());
    }

    public async Task<T> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state... [feature={GetType().Name}]");

        var internalResult = await getValue().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = FromInternal(internalResult);
        if (!Enum.IsDefined(result))
            throw ExceptionHelper.UndefinedValueReceived(result);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result} [feature={GetType().Name}]");

        return result;
    }

    public virtual async Task SetStateAsync(T state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        await setValue(ToInternal(state)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state} [feature={GetType().Name}]");
    }

    public virtual void InvalidateResolution()
    {
    }

    private int ToInternal(T state) => (int)(object)state + offset;

    private T FromInternal(int state) => (T)(object)(state - offset);
}
