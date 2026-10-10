using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public abstract class AbstractDriverFeature<T>(Func<SafeFileHandle> driverHandleHandle, uint controlCode, IDelayProvider? delayProvider = null) : IFeature<T> where T : struct, Enum, IComparable
{
    private readonly IDelayProvider _delayProvider = delayProvider ?? new DefaultDelayProvider();
    protected readonly uint ControlCode = controlCode;
    protected readonly Func<SafeFileHandle> DriverHandle = driverHandleHandle;

    protected T LastState;

    public virtual async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

            if (!Compatibility.IsSupportedLegionMachine(mi))
                return false;

            _ = await GetStateAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"feature-driver-supported-{GetType().Name}",
                $"Driver feature support probe failed for {GetType().Name}.",
                ex);
            return false;
        }
    }

    public Task<T[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<T>());
    }

    public virtual async Task<T> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state... [feature={GetType().Name}]");

        var outBuffer = await SendCodeAsync(DriverHandle(), ControlCode, GetInBufferValue(), cancellationToken).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Buffer value: {outBuffer} [feature={GetType().Name}]");

        var state = await FromInternalAsync(outBuffer, cancellationToken).ConfigureAwait(false);
        LastState = state;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {state} [feature={GetType().Name}]");

        return state;
    }

    public virtual async Task SetStateAsync(T state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        var codes = await ToInternalAsync(state, cancellationToken).ConfigureAwait(false);
        foreach (var code in codes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendCodeAsync(DriverHandle(), ControlCode, code, cancellationToken).ConfigureAwait(false);
        }

        await VerifyStateSetAsync(state, cancellationToken).ConfigureAwait(false);

        // Only commit LastState after verify succeeds so failed transitions keep the prior mode
        // for correct multi-step ToInternal sequences (e.g. battery RapidCharge → Conservation).
        LastState = state;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State set to {state} [feature={GetType().Name}]");
    }

    public virtual void InvalidateResolution()
    {
    }

    protected abstract Task<T> FromInternalAsync(uint state, CancellationToken cancellationToken = default);

    protected abstract uint GetInBufferValue();

    protected abstract Task<uint[]> ToInternalAsync(T state, CancellationToken cancellationToken = default);

    protected Task<uint> SendCodeAsync(SafeFileHandle handle, uint controlCode, uint inBuffer, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (PInvokeExtensions.DeviceIoControl(handle, controlCode, inBuffer, out uint outBuffer))
            return outBuffer;

        var error = Marshal.GetLastWin32Error();

        Log.Instance.Warning($"DeviceIoControl returned 0, last error: {error} [feature={GetType().Name}]");

        throw new InvalidOperationException(string.Format(Resource.Exception_DeviceIoControlError, error));
    }, cancellationToken);

    private async Task VerifyStateSetAsync(T state, CancellationToken cancellationToken)
    {
        var retries = 0;

        while (retries < 10)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.Equals(await GetStateAsync(cancellationToken).ConfigureAwait(false)))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Verify state {state} set succeeded. [feature={GetType().Name}]");

                return;
            }

            retries++;

            await _delayProvider.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
        }

        Log.Instance.Warning($"Verify state {state} set failed. [feature={GetType().Name}]");

        throw new InvalidOperationException(string.Format(Resource.Exception_FailedVerifyState, GetType().Name, state));
    }
}
