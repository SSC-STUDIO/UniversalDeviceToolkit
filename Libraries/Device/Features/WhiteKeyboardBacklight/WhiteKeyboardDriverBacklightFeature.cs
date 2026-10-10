using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight;

public class WhiteKeyboardDriverBacklightFeature()
    : AbstractDriverFeature<WhiteKeyboardBacklightState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_KEYBOARD)
{
    public override async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var outBuffer = await SendCodeAsync(DriverHandle(), ControlCode, 0x1, cancellationToken).ConfigureAwait(false);
            outBuffer >>= 1;
            return outBuffer == 0x2;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("feature-white-kb-driver-supported", "White keyboard driver backlight support probe failed.", ex);
            return false;
        }
    }

    protected override uint GetInBufferValue() => 0x22;

    protected override Task<uint[]> ToInternalAsync(WhiteKeyboardBacklightState state, CancellationToken cancellationToken = default)
    {
        var result = state switch
        {
            WhiteKeyboardBacklightState.Off => new uint[] { 0x00023 },
            WhiteKeyboardBacklightState.Low => [0x10023],
            WhiteKeyboardBacklightState.High => [0x20023],
            _ => throw ExceptionHelper.InvalidState(),
        };
        return Task.FromResult(result);
    }

    protected override Task<WhiteKeyboardBacklightState> FromInternalAsync(uint state, CancellationToken cancellationToken = default)
    {
        var result = state switch
        {
            0x1 => WhiteKeyboardBacklightState.Off,
            0x3 => WhiteKeyboardBacklightState.Low,
            0x5 => WhiteKeyboardBacklightState.High,
            _ => throw ExceptionHelper.InvalidState(),
        };
        return Task.FromResult(result);
    }
}
