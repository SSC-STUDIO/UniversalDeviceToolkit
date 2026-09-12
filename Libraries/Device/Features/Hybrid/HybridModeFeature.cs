using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

public class HybridModeFeature(
    IGSyncFeature gSyncFeature,
    IIGPUModeFeature igpuModeFeature,
    IDGPUNotify dgpuNotify,
    ICompatibilityService compatibilityService,
    IDelayProvider? delayProvider = null) : IFeature<HybridModeState>, IDisposable
{
    private readonly IDelayProvider _delayProvider = delayProvider ?? new DefaultDelayProvider();
    private CancellationTokenSource? _ensureDGPUEjectedIfNeededCts = new();
    private int _disposed;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mi = await compatibilityService.GetMachineInformationAsync().ConfigureAwait(false);
        return mi.Properties.SupportsGSync || mi.Properties.SupportsIGPUMode;
    }

    public async Task<HybridModeState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mi = await compatibilityService.GetMachineInformationAsync().ConfigureAwait(false);

        return (mi.Properties.SupportsGSync, mi.Properties.SupportsIGPUMode) switch
        {
            (true, true) => [HybridModeState.On, HybridModeState.OnIGPUOnly, HybridModeState.OnAuto, HybridModeState.Off],
            (false, true) => [HybridModeState.On, HybridModeState.OnIGPUOnly, HybridModeState.OnAuto],
            (true, false) => [HybridModeState.On, HybridModeState.Off],
            _ => []
        };
    }

    public async Task<HybridModeState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var gSyncSupported = await gSyncFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false);
        var igpuModeSupported = await igpuModeFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false);

        var gSync = GSyncState.Off;
        var igpuMode = IGPUModeState.Default;

        if (gSyncSupported)
            gSync = await gSyncFeature.GetStateAsync(cancellationToken).ConfigureAwait(false);

        if (igpuModeSupported)
            igpuMode = await igpuModeFeature.GetStateAsync(cancellationToken).ConfigureAwait(false);

        var state = Pack(gSync, igpuMode);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {state} [gSync={gSync}, igpuMode={igpuMode}]");

        return state;
    }

    public async Task SetStateAsync(HybridModeState state, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Serialize CTS swap so concurrent SetStateAsync cannot double-dispose.
        var newCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _ensureDGPUEjectedIfNeededCts, newCts);
        if (Volatile.Read(ref _disposed) != 0)
        {
            Interlocked.CompareExchange(ref _ensureDGPUEjectedIfNeededCts, null, newCts);
            newCts.Dispose();
            if (previousCts is not null)
            {
                try
                {
                    await previousCts.CancelAsync().ConfigureAwait(false);
                    previousCts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            throw new ObjectDisposedException(nameof(HybridModeFeature));
        }

        if (previousCts is not null)
        {
            try
            {
                await previousCts.CancelAsync().ConfigureAwait(false);
                previousCts.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Another caller already disposed it.
            }
        }

        var (gSync, igpuMode) = Unpack(state);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [gSync={gSync}, igpuMode={igpuMode}]");

        var gSyncSupported = await gSyncFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false);
        var igpuModeSupported = await igpuModeFeature.IsSupportedAsync(cancellationToken).ConfigureAwait(false);

        var gSyncChanged = false;
        var igpuModeChanged = false;

        if (gSyncSupported && await gSyncFeature.GetStateAsync(cancellationToken).ConfigureAwait(false) != gSync)
        {
            await gSyncFeature.SetStateAsync(gSync, cancellationToken).ConfigureAwait(false);
            gSyncChanged = true;
        }

        if (igpuModeSupported && await igpuModeFeature.GetStateAsync(cancellationToken).ConfigureAwait(false) != igpuMode)
        {
            try
            {
                await igpuModeFeature.SetStateAsync(igpuMode, cancellationToken).ConfigureAwait(false);
                igpuModeChanged = true;
            }
            catch (IGPUModeChangeException ex)
            {
                // GSync may already have been applied; still fail the hybrid set so callers
                // do not treat a partial GPU mode change as success.
                Log.Instance.Warning(
                    $"iGPU mode change failed during hybrid set (gSyncChanged={gSyncChanged}). Hybrid mode may be partial.",
                    ex);
                throw;
            }
            finally
            {
                if (!gSyncChanged && igpuMode is IGPUModeState.Default or IGPUModeState.Auto)
                    await dgpuNotify.NotifyLaterIfNeededAsync().ConfigureAwait(false);
            }
        }

        // iGPU-only must actively eject the discrete GPU; NotifyLater is only for Default/Auto.
        if (igpuModeChanged && igpuMode == IGPUModeState.IGPUOnly)
            await EnsureDGPUEjectedIfNeededAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State set to {state} [gSync={gSync}, igpuMode={igpuMode}]");
    }

    public void InvalidateResolution()
    {
        gSyncFeature.InvalidateResolution();
        igpuModeFeature.InvalidateResolution();
        dgpuNotify.InvalidateResolution();
    }

    public async Task EnsureDGPUEjectedIfNeededAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        if (!await igpuModeFeature.IsSupportedAsync().ConfigureAwait(false) || !await dgpuNotify.IsSupportedAsync().ConfigureAwait(false))
            return;

        var cts = Volatile.Read(ref _ensureDGPUEjectedIfNeededCts);
        CancellationToken token;
        try
        {
            token = cts?.Token ?? CancellationToken.None;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                const int MAX_RETRIES = 5;
                const int DELAY = 5 * 1000;

                var retry = 1;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Will make sure that dGPU is ejected. [maxRetries={MAX_RETRIES}, delay={DELAY}ms]");

                while (retry <= MAX_RETRIES)
                {
                    await _delayProvider.Delay(TimeSpan.FromMilliseconds(DELAY), token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Cancelled, aborting...");
                        break;
                    }

                    if (await igpuModeFeature.GetStateAsync().ConfigureAwait(false) != IGPUModeState.IGPUOnly)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Not in iGPU-only mode, aborting...");
                        break;
                    }

                    if (!await dgpuNotify.IsDGPUAvailableAsync().ConfigureAwait(false))
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"dGPU already unavailable, aborting...");
                        break;
                    }

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Notifying dGPU... [retry={retry}, maxRetries={MAX_RETRIES}]");

                    await dgpuNotify.NotifyAsync(false).ConfigureAwait(false);

                    retry++;
                }
            }
            catch (OperationCanceledException)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cancelled, aborting...");
            }
            catch (Exception ex)
            {
                Log.Instance.Error($"Failed to ensure dGPU is ejected", ex);
            }
        }, token).Forget("ensure dGPU ejected if needed");
    }

    private static (GSyncState, IGPUModeState) Unpack(HybridModeState state) => state switch
    {
        HybridModeState.On => (GSyncState.Off, IGPUModeState.Default),
        HybridModeState.OnIGPUOnly => (GSyncState.Off, IGPUModeState.IGPUOnly),
        HybridModeState.OnAuto => (GSyncState.Off, IGPUModeState.Auto),
        HybridModeState.Off => (GSyncState.On, IGPUModeState.Default),
        _ => throw ExceptionHelper.InvalidState(),
    };

    private static HybridModeState Pack(GSyncState state1, IGPUModeState state2) => (state1, state2) switch
    {
        (GSyncState.Off, IGPUModeState.Default) => HybridModeState.On,
        (GSyncState.Off, IGPUModeState.IGPUOnly) => HybridModeState.OnIGPUOnly,
        (GSyncState.Off, IGPUModeState.Auto) => HybridModeState.OnAuto,
        (GSyncState.On, _) => HybridModeState.Off,
        _ => throw ExceptionHelper.InvalidState(),
    };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var cts = Interlocked.Exchange(ref _ensureDGPUEjectedIfNeededCts, null);
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts.Dispose();
    }
}
