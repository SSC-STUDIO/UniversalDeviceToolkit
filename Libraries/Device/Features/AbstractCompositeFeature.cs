using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace UniversalDeviceToolkit.Lib.Features;

public abstract class AbstractCompositeFeature<T>(params IFeature<T>[] features) : IFeature<T> where T : struct
{
    private readonly AsyncLock _lock = new();

    private bool _resolved;
    private IFeature<T>? _feature;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var feature = await ResolveInternalAsync(cancellationToken).ConfigureAwait(false);
        if (feature is null)
            return false;
        return await feature.IsSupportedAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var feature = await ResolveInternalAsync(cancellationToken).ConfigureAwait(false)
                      ?? throw ExceptionHelper.NoSupportedFeature(GetType().Name);
        return await feature.GetAllStatesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var feature = await ResolveInternalAsync(cancellationToken).ConfigureAwait(false)
                      ?? throw ExceptionHelper.NoSupportedFeature(GetType().Name);
        return await feature.GetStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetStateAsync(T state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var feature = await ResolveInternalAsync(cancellationToken).ConfigureAwait(false)
                      ?? throw ExceptionHelper.NoSupportedFeature(GetType().Name);
        await feature.SetStateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    public void InvalidateResolution()
    {
        using (_lock.Lock())
        {
            _resolved = false;
            _feature = null;

            foreach (var feature in features)
                feature.InvalidateResolution();
        }
    }

    protected virtual async Task<IFeature<T>?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        foreach (var feature in features)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await feature.IsSupportedAsync(cancellationToken).ConfigureAwait(false))
                continue;

            return feature;
        }

        return null;
    }

    private async Task<IFeature<T>?> ResolveInternalAsync(CancellationToken cancellationToken)
    {
        using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_resolved)
                return _feature;

            _feature = await ResolveAsync(cancellationToken).ConfigureAwait(false);
            _resolved = true;
            return _feature;
        }
    }
}
