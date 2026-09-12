using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Dell;

/// <summary>
/// Alienware / Dell G-Series thermal profiles over the AWCC WMAX interface
/// (protocol mirrors the mainline alienware-wmi driver):
///   select via Thermal_Control op 0x01, read back via Thermal_Information
///   op 0x0B. Profile ids differ per thermal table: USTT Balanced 0xA0 /
///   Performance 0xA4 / Quiet 0xA3; Legacy 0x97 / 0x99 / 0x96. Writes try USTT
///   first and fall back to Legacy, verified by read-back. Self-disables on
///   non-Dell machines or when no AWCC interface answers.
/// </summary>
public class AlienwarePowerModeFeature(IAlienwareWmi wmi) : IFeature<PowerModeState>
{
    private const string ThermalInformation = "Thermal_Information";
    private const string ThermalControl = "Thermal_Control";

    private const byte OpActivateProfile = 0x01;
    private const byte OpSystemDescription = 0x02;
    private const byte OpGetCurrentProfile = 0x0B;

    private const int MaxResourceCount = 16;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!wmi.IsAvailable || !await IsDellMachineAsync().ConfigureAwait(false))
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

        var current = wmi.Execute(ThermalInformation, OpGetCurrentProfile);
        if (current < 0)
            throw ExceptionHelper.PowerModeNotSupported();

        return ToState(current);
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        // USTT table first, Legacy table as fallback (a model has exactly one).
        foreach (var profile in ToProfileIds(state))
        {
            if (wmi.Execute(ThermalControl, OpActivateProfile, profile) < 0)
                continue;

            var current = wmi.Execute(ThermalInformation, OpGetCurrentProfile);
            if (current == profile || (current >= 0 && ToState(current) == state))
                return;
        }

        throw ExceptionHelper.PowerModeNotSupported();
    }

    public void InvalidateResolution()
    {
    }

    private Task<bool> ProbeAsync()
    {
        // System description: byte0 = fan count, byte1 = temp count,
        // byte3 = profile count; malformed = unsupported (kernel rule).
        var description = wmi.Execute(ThermalInformation, OpSystemDescription);
        if (description < 0)
            return Task.FromResult(false);

        var fanCount = description & 0xFF;
        var profileCount = (description >> 24) & 0xFF;
        return Task.FromResult(fanCount is >= 1 and <= MaxResourceCount &&
                               profileCount is >= 1 and <= MaxResourceCount);
    }

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private static PowerModeState ToState(int profile) => profile switch
    {
        0x96 or 0xA3 => PowerModeState.Quiet,
        0x99 or 0xA4 or 0xAB => PowerModeState.Performance,
        0x98 or 0xA1 => PowerModeState.Performance, // balanced-performance
        _ => PowerModeState.Balance,
    };

    private static byte[] ToProfileIds(PowerModeState state) => state switch
    {
        PowerModeState.Quiet => [0xA3, 0x96],
        PowerModeState.Performance => [0xA4, 0x99],
        _ => [0xA0, 0x97],
    };

    private static async Task<bool> IsDellMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("Dell", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Alienware", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
