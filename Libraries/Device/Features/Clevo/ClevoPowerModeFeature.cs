using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Clevo;

/// <summary>
/// Clevo / Sager / Schenker / Tuxedo performance modes over the ACPI EC port I/O.
/// EC Register 0xD8 (or 0xD5 on select platforms):
///   0x01 = Quiet, 0x02 = Power Saving (maps to Quiet)
///   0x00 = Entertainment (maps to Balance)
///   0x03 = Performance
/// Probing is read-only; writes are verified by read-back.
/// Self-disables on non-Clevo hardware or when EC channel is unavailable.
/// </summary>
public class ClevoPowerModeFeature(IEcChannel ec) : IFeature<PowerModeState>
{
    private const byte ModeRegisterPrimary = 0xD8;
    private const byte ModeRegisterSecondary = 0xD5;

    private const byte ModeEntertainment = 0x00;
    private const byte ModeQuiet = 0x01;
    private const byte ModePowerSaving = 0x02;
    private const byte ModePerformance = 0x03;

    private bool _probed;
    private byte _address;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ec.IsAvailable || !await IsClevoMachineAsync().ConfigureAwait(false))
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

        if (!ec.TryRead(_address, out var raw))
            throw ExceptionHelper.PowerModeNotSupported();

        return ToState(raw);
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var rawValue = ToRaw(state);
        if (!ec.TryWrite(_address, rawValue))
            throw ExceptionHelper.PowerModeNotSupported();

        if (!ec.TryRead(_address, out var readBack) || ToState(readBack) != state)
            throw ExceptionHelper.PowerModeNotSupported();
    }

    public void InvalidateResolution()
    {
        _probed = false;
        _address = 0;
    }

    private Task<bool> ProbeAsync()
    {
        if (_probed)
            return Task.FromResult(_address != 0);
        _probed = true;

        if (ec.TryRead(ModeRegisterPrimary, out var val1) && IsValidMode(val1))
        {
            _address = ModeRegisterPrimary;
            return Task.FromResult(true);
        }

        if (ec.TryRead(ModeRegisterSecondary, out var val2) && IsValidMode(val2))
        {
            _address = ModeRegisterSecondary;
            return Task.FromResult(true);
        }

        _address = 0;
        return Task.FromResult(false);
    }

    private static bool IsValidMode(byte value) =>
        value is ModeEntertainment or ModeQuiet or ModePowerSaving or ModePerformance;

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private static PowerModeState ToState(byte raw) => raw switch
    {
        ModeQuiet or ModePowerSaving => PowerModeState.Quiet,
        ModePerformance => PowerModeState.Performance,
        _ => PowerModeState.Balance,
    };

    private static byte ToRaw(PowerModeState state) => state switch
    {
        PowerModeState.Quiet => ModeQuiet,
        PowerModeState.Performance => ModePerformance,
        _ => ModeEntertainment,
    };

    public static async Task<bool> IsClevoMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            var model = mi.Model ?? string.Empty;

            return vendor.Contains("CLEVO", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Clevo", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("SAGER", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Schenker", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("XMG", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Tuxedo", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("System76", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("Clevo", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("NH5", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("NH7", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("PB5", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("PB7", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("X170", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
