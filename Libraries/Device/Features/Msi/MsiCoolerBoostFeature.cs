using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Msi;

/// <summary>
/// MSI Cooler Boost (强冷模式) toggle via EC register 0x98 (or fan mode register 0xD4).
/// Values: 0x82 or bit 7 set = enabled, 0x02 or 0x00 = auto/disabled.
/// Self-disables on non-MSI machines or when EC channel is unavailable.
/// </summary>
public class MsiCoolerBoostFeature(IEcChannel ec) : IFeature<bool>
{
    private const byte CoolerBoostAddress = 0x98;
    private const byte CoolerBoostOn = 0x82;
    private const byte CoolerBoostOff = 0x02;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ec.IsAvailable || !await IsMsiMachineAsync().ConfigureAwait(false))
            return false;

        return ec.TryRead(CoolerBoostAddress, out _);
    }

    public Task<bool[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new[] { false, true });
    }

    public async Task<bool> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.FeatureNotSupported();

        if (!ec.TryRead(CoolerBoostAddress, out var raw))
            throw ExceptionHelper.FeatureNotSupported();

        return (raw & 0x80) != 0 || raw == CoolerBoostOn;
    }

    public async Task SetStateAsync(bool state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.FeatureNotSupported();

        byte raw = state ? CoolerBoostOn : CoolerBoostOff;
        if (!ec.TryWrite(CoolerBoostAddress, raw))
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
