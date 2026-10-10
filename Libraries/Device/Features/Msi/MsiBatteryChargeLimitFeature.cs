using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Msi;

/// <summary>
/// Controls MSI battery charging threshold via EC register 0xEF.
/// EC values: 0x3C = 60%, 0x50 = 80%, 0x64/0x00 = 100%.
/// Self-disables on non-MSI machines or when EC channel is unavailable.
/// </summary>
public class MsiBatteryChargeLimitFeature(IEcChannel ec) : IFeature<int>
{
    private const byte BatteryThresholdAddress = 0xEF;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ec.IsAvailable || !await IsMsiMachineAsync().ConfigureAwait(false))
            return false;

        return ec.TryRead(BatteryThresholdAddress, out var val) && val is 0x3C or 0x50 or 0x64 or 0x00;
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

        if (!ec.TryRead(BatteryThresholdAddress, out var raw))
            throw ExceptionHelper.FeatureNotSupported();

        return raw switch
        {
            <= 0x3C => 60,
            <= 0x50 => 80,
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

        byte raw = state switch
        {
            60 => 0x3C,
            80 => 0x50,
            _ => 0x64,
        };

        if (!ec.TryWrite(BatteryThresholdAddress, raw))
            throw ExceptionHelper.FeatureNotSupported();
    }

    public void InvalidateResolution()
    {
    }

    private static async Task<bool> IsMsiMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("MSI", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
