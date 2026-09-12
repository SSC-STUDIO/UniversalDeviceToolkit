using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Msi;

/// <summary>
/// MSI shift modes over the EC (protocol mirrors the mainline msi-ec driver and
/// the BeardOverflow conf tables):
///   Gen2 (11th gen Core+): shift mode at 0xD2, fan mode at 0xD4
///   Gen1 (10th gen and older): shift mode at 0xF2, fan mode at 0xF4
///   Values: eco 0xC2, comfort 0xC1, sport 0xC0, turbo 0xC4.
/// Generation is detected read-only: the address whose value is a valid shift
/// mode wins. Writes go to the detected generation only, verified by read-back.
/// Self-disables on non-MSI machines or when no EC channel is present.
/// </summary>
public class MsiPowerModeFeature(IEcChannel ec) : IFeature<PowerModeState>
{
    private const byte ShiftModeGen2 = 0xD2;
    private const byte ShiftModeGen1 = 0xF2;

    private const byte ModeEco = 0xC2;
    private const byte ModeComfort = 0xC1;
    private const byte ModeSport = 0xC0;
    private const byte ModeTurbo = 0xC4;

    private bool _probed;
    private byte _shiftAddress;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ec.IsAvailable || !await IsMsiMachineAsync().ConfigureAwait(false))
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

        if (!ec.TryRead(_shiftAddress, out var raw))
            throw ExceptionHelper.PowerModeNotSupported();

        return ToState(raw);
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        if (!ec.TryWrite(_shiftAddress, ToRaw(state)))
            throw ExceptionHelper.PowerModeNotSupported();

        if (!ec.TryRead(_shiftAddress, out var readBack) || ToState(readBack) != state)
            throw ExceptionHelper.PowerModeNotSupported();
    }

    public void InvalidateResolution()
    {
        _probed = false;
        _shiftAddress = 0;
    }

    private Task<bool> ProbeAsync()
    {
        if (_probed)
            return Task.FromResult(_shiftAddress != 0);
        _probed = true;

        // Read-only generation detection: prefer the Gen2 layout (11th gen+).
        if (ec.TryRead(ShiftModeGen2, out var gen2Value) && IsShiftModeValue(gen2Value))
        {
            _shiftAddress = ShiftModeGen2;
            return Task.FromResult(true);
        }

        if (ec.TryRead(ShiftModeGen1, out var gen1Value) && IsShiftModeValue(gen1Value))
        {
            _shiftAddress = ShiftModeGen1;
            return Task.FromResult(true);
        }

        _shiftAddress = 0;
        return Task.FromResult(false);
    }

    private static bool IsShiftModeValue(byte value) =>
        value is ModeEco or ModeComfort or ModeSport or ModeTurbo;

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private static PowerModeState ToState(byte raw) => raw switch
    {
        ModeEco => PowerModeState.Quiet,
        ModeSport or ModeTurbo => PowerModeState.Performance,
        _ => PowerModeState.Balance,
    };

    private static byte ToRaw(PowerModeState state) => state switch
    {
        PowerModeState.Quiet => ModeEco,
        PowerModeState.Performance => ModeSport,
        _ => ModeComfort,
    };

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
