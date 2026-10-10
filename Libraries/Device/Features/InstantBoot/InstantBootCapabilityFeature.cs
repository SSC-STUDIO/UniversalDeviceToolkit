using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.InstantBoot;

public class InstantBootCapabilityFeature : IFeature<InstantBootState>
{
    private enum State
    {
        Off,
        On
    }

    private class InstantBootAcFeature() : AbstractCapabilityFeature<State>(CapabilityID.InstantBootAc);
    private class InstantBootUsbPowerDeliveryFeature() : AbstractCapabilityFeature<State>(CapabilityID.InstantBootUsbPowerDelivery);

    private readonly InstantBootAcFeature _ac = new();
    private readonly InstantBootUsbPowerDeliveryFeature _usbPowerDelivery = new();

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _ac.IsSupportedAsync(cancellationToken).ConfigureAwait(false) && await _usbPowerDelivery.IsSupportedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<InstantBootState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<InstantBootState>());
    }

    public async Task<InstantBootState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var ac = await _ac.GetStateAsync(cancellationToken).ConfigureAwait(false);
        var usbPowerDelivery = await _usbPowerDelivery.GetStateAsync(cancellationToken).ConfigureAwait(false);

        var result = (ac, usbPowerDelivery) switch
        {
            (State.On, State.On) => InstantBootState.AcAdapterAndUsbPowerDelivery,
            (State.On, State.Off) => InstantBootState.AcAdapter,
            (State.Off, State.On) => InstantBootState.UsbPowerDelivery,
            _ => InstantBootState.Off
        };

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result}");

        return result;
    }

    public async Task SetStateAsync(InstantBootState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}...");

        var (ac, usbPowerDelivery) = state switch
        {
            InstantBootState.AcAdapterAndUsbPowerDelivery => (State.On, State.On),
            InstantBootState.AcAdapter => (State.On, State.Off),
            InstantBootState.UsbPowerDelivery => (State.Off, State.On),
            InstantBootState.Off => (State.Off, State.Off),
            _ => throw ExceptionHelper.InvalidState()
        };

        await _ac.SetStateAsync(ac, cancellationToken).ConfigureAwait(false);
        await _usbPowerDelivery.SetStateAsync(usbPowerDelivery, cancellationToken).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }

    public void InvalidateResolution()
    {
    }
}
