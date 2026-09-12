using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public class OneLevelWhiteKeyboardBacklightFeature() : AbstractDriverFeature<OneLevelWhiteKeyboardBacklightState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_SETTINGS)
{
    public override async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var outBuffer = await SendCodeAsync(DriverHandle(), ControlCode, GetInBufferValue(), cancellationToken).ConfigureAwait(false);
            var result = ((int)outBuffer & 16) == 16;
            return result;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("feature-onelevel-kb-supported", "One-level white keyboard backlight support probe failed.", ex);
            return false;
        }
    }

    protected override uint GetInBufferValue() => 0x2;

    protected override Task<uint[]> ToInternalAsync(OneLevelWhiteKeyboardBacklightState state, CancellationToken cancellationToken = default)
    {
        var result = state switch
        {
            OneLevelWhiteKeyboardBacklightState.Off => new uint[] { 0x9 },
            OneLevelWhiteKeyboardBacklightState.On => [0x8],
            _ => throw ExceptionHelper.InvalidState(),
        };
        return Task.FromResult(result);
    }

    protected override Task<OneLevelWhiteKeyboardBacklightState> FromInternalAsync(uint state, CancellationToken cancellationToken = default)
    {
        var isOn = ((int)state & 32) == 32;
        var result = isOn ? OneLevelWhiteKeyboardBacklightState.On : OneLevelWhiteKeyboardBacklightState.Off;
        return Task.FromResult(result);
    }
}
