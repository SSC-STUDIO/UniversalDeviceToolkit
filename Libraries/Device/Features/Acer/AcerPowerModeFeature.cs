using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Acer;

/// <summary>
/// Acer Predator/Nitro thermal profiles over the WMID Gaming interface
/// (protocol mirrors the mainline acer-wmi driver): platform profile via
/// misc setting 0x0B through Get/SetGamingMiscSetting — write
/// 0x0B | (profile &lt;&lt; 8), read value at (gmOutput &gt;&gt; 8) &amp; 0xFF, values
/// Quiet 0x00 / Balanced 0x01 / Performance 0x04 (Turbo 0x05 and Eco 0x06 map
/// onto the nearest UI state on read). Writes verified by read-back.
/// Self-disables on non-Acer machines or when the interface is absent.
/// </summary>
public class AcerPowerModeFeature(IAcerWmi wmi) : IFeature<PowerModeState>
{
    private const string GetGamingMiscSetting = "GetGamingMiscSetting";
    private const string SetGamingMiscSetting = "SetGamingMiscSetting";
    private const string GetGamingSysInfo = "GetGamingSysInfo";

    private const uint PlatformProfileSetting = 0x0B;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!wmi.IsAvailable || !await IsAcerMachineAsync().ConfigureAwait(false))
            return false;

        return await ProbeAsync().ConfigureAwait(false);
    }

    public Task<PowerModeState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new[]
        {
            PowerModeState.Quiet,
            PowerModeState.Balance,
            PowerModeState.Performance,
        });
    }

    public async Task<PowerModeState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var (ok, output) = wmi.Execute(GetGamingMiscSetting, PlatformProfileSetting);
        if (!ok)
            throw ExceptionHelper.PowerModeNotSupported();

        return ToState((int)((output >> 8) & 0xFF));
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var (ok, _) = wmi.Execute(SetGamingMiscSetting, PlatformProfileSetting | ((uint)ToRaw(state) << 8));
        if (ok)
        {
            var (readOk, readBack) = wmi.Execute(GetGamingMiscSetting, PlatformProfileSetting);
            if (readOk && ToState((int)((readBack >> 8) & 0xFF)) == state)
                return;
        }

        throw ExceptionHelper.PowerModeNotSupported();
    }

    public void InvalidateResolution()
    {
    }

    private Task<bool> ProbeAsync()
    {
        // Same rule as the kernel's hwmon registration: sys-info command 0 must
        // succeed and report at least one supported sensor (bits 39:24).
        var (ok, output) = wmi.Execute(GetGamingSysInfo, 0x0000);
        var supportedMask = (output >> 24) & 0xFFFF;
        return Task.FromResult(ok && supportedMask != 0);
    }

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private static PowerModeState ToState(int profile) => profile switch
    {
        0x00 or 0x06 => PowerModeState.Quiet,     // quiet / eco
        0x04 or 0x05 => PowerModeState.Performance, // performance / turbo
        _ => PowerModeState.Balance,
    };

    private static int ToRaw(PowerModeState state) => state switch
    {
        PowerModeState.Quiet => 0x00,
        PowerModeState.Performance => 0x04,
        _ => 0x01,
    };

    private static async Task<bool> IsAcerMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Acer", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Predator", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Nitro", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
