using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Asus;

/// <summary>
/// Controls ASUS battery health charging limit (60%, 80%, or 100%) via the ATKACPI
/// WMI device endpoint 0x00120057.
/// Returns / sets integer percentage threshold (60, 80, 100).
/// Self-disables on non-ASUS machines or when ATKACPI is unavailable.
/// </summary>
public class AsusBatteryChargeLimitFeature(IAsusAtkDriver atk) : IFeature<int>
{
    private const uint DeviceIdBatteryHealth = 0x00120057;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!atk.IsAvailable || !await IsAsusMachineAsync().ConfigureAwait(false))
            return false;

        return atk.DeviceGet(DeviceIdBatteryHealth) >= 0;
    }

    public Task<int[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new[] { 60, 80, 100 });
    }

    public async Task<int> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.FeatureNotSupported();

        var val = atk.DeviceGet(DeviceIdBatteryHealth);
        if (val < 0)
            throw ExceptionHelper.FeatureNotSupported();

        return val switch
        {
            <= 60 => 60,
            <= 80 => 80,
            _ => 100
        };
    }

    public async Task SetStateAsync(int state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (60 or 80 or 100))
            throw ExceptionHelper.InvalidState();

        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.FeatureNotSupported();

        var result = atk.DeviceSet(DeviceIdBatteryHealth, state);
        if (result < 0)
            throw ExceptionHelper.FeatureNotSupported();
    }

    public void InvalidateResolution()
    {
    }

    public static async Task<bool> IsAsusMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("ASUS", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("ASUSTeK", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
