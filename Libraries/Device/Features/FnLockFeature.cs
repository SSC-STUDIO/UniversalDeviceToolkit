using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public class FnLockFeature() : AbstractDriverFeature<FnLockState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_SETTINGS)
{
    protected override uint GetInBufferValue() => 0x2;

    protected override Task<uint[]> ToInternalAsync(FnLockState state, CancellationToken cancellationToken = default)
    {
        var lockOn = state switch
        {
            FnLockState.On => true,
            FnLockState.Off => false,
            _ => throw ExceptionHelper.InvalidState(),
        };

        var value = lockOn ? new uint[] { 0xE } : [0xF];
        return Task.FromResult(value);
    }

    protected override Task<FnLockState> FromInternalAsync(uint state, CancellationToken cancellationToken = default)
    {
        var value = state.GetNthBit(10) ? FnLockState.On : FnLockState.Off;
        return Task.FromResult(value);
    }
}
