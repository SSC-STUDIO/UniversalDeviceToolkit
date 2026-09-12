using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features;

public class BatteryNightChargeFeature() : AbstractDriverFeature<BatteryNightChargeState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_BATTERY_NIGHT_CHARGE)
{
    protected override uint GetInBufferValue() => 0x11;

    protected override Task<uint[]> ToInternalAsync(BatteryNightChargeState state, CancellationToken cancellationToken = default)
    {
        uint[] result = state switch
        {
            BatteryNightChargeState.On => [0x80000012u],
            BatteryNightChargeState.Off => [0x12u],
            _ => throw ExceptionHelper.InvalidState()
        };
        return Task.FromResult(result);
    }

    protected override Task<BatteryNightChargeState> FromInternalAsync(uint state, CancellationToken cancellationToken = default)
    {
        if (state.GetNthBit(0))
            return Task.FromResult(state.GetNthBit(4) ? BatteryNightChargeState.On : BatteryNightChargeState.Off);

        throw ExceptionHelper.UnknownBatteryState(state, Convert.ToString(state, 2));
    }
}
