using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Tongfang;

/// <summary>
/// Tongfang / Uniwill / MECHREVO / Hasee performance modes over the ACPI EC port I/O.
/// Supported layouts:
///   Primary (Uniwill GMx / GKx / MECHREVO Jiaolong &amp; Kuangshi): register 0xD7
///     Values: 0x01 = Office (Quiet), 0x02 = Balance / Gaming, 0x03 = Turbo / Beast (Performance)
///   Secondary (Older Tongfang / QC71 / Intel NUC barebones): register 0xE4
///     Values: 0x00 = Battery Saver (Quiet), 0x01 = Balance, 0x02 = Performance
/// Probing is strictly read-only; writes are verified by read-back.
/// Self-disables on non-Tongfang/MECHREVO machines or when EC channel is unavailable.
/// </summary>
public class TongfangPowerModeFeature(IEcChannel ec) : IFeature<PowerModeState>
{
    private const byte ModeRegisterPrimary = 0xD7;
    private const byte ModeRegisterSecondary = 0xE4;

    private const byte PrimaryOffice = 0x01;
    private const byte PrimaryGaming = 0x02;
    private const byte PrimaryTurbo = 0x03;

    private const byte SecondaryQuiet = 0x00;
    private const byte SecondaryBalance = 0x01;
    private const byte SecondaryPerformance = 0x02;

    private enum LayoutType
    {
        None,
        Primary,
        Secondary
    }

    private bool _probed;
    private LayoutType _layout = LayoutType.None;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ec.IsAvailable || !await IsTongfangMachineAsync().ConfigureAwait(false))
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

        var address = _layout == LayoutType.Primary ? ModeRegisterPrimary : ModeRegisterSecondary;
        if (!ec.TryRead(address, out var raw))
            throw ExceptionHelper.PowerModeNotSupported();

        return ToState(raw, _layout);
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var address = _layout == LayoutType.Primary ? ModeRegisterPrimary : ModeRegisterSecondary;
        var rawValue = ToRaw(state, _layout);

        if (!ec.TryWrite(address, rawValue))
            throw ExceptionHelper.PowerModeNotSupported();

        if (!ec.TryRead(address, out var readBack) || ToState(readBack, _layout) != state)
            throw ExceptionHelper.PowerModeNotSupported();
    }

    public void InvalidateResolution()
    {
        _probed = false;
        _layout = LayoutType.None;
    }

    private Task<bool> ProbeAsync()
    {
        if (_probed)
            return Task.FromResult(_layout != LayoutType.None);
        _probed = true;

        // Primary layout (0xD7: 1..3)
        if (ec.TryRead(ModeRegisterPrimary, out var primaryVal) && IsPrimaryModeValue(primaryVal))
        {
            _layout = LayoutType.Primary;
            return Task.FromResult(true);
        }

        // Secondary layout (0xE4: 0..2)
        if (ec.TryRead(ModeRegisterSecondary, out var secVal) && IsSecondaryModeValue(secVal))
        {
            _layout = LayoutType.Secondary;
            return Task.FromResult(true);
        }

        _layout = LayoutType.None;
        return Task.FromResult(false);
    }

    private static bool IsPrimaryModeValue(byte value) =>
        value is PrimaryOffice or PrimaryGaming or PrimaryTurbo;

    private static bool IsSecondaryModeValue(byte value) =>
        value is SecondaryQuiet or SecondaryBalance or SecondaryPerformance;

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private static PowerModeState ToState(byte raw, LayoutType layout)
    {
        if (layout == LayoutType.Primary)
        {
            return raw switch
            {
                PrimaryOffice => PowerModeState.Quiet,
                PrimaryTurbo => PowerModeState.Performance,
                _ => PowerModeState.Balance,
            };
        }

        return raw switch
        {
            SecondaryQuiet => PowerModeState.Quiet,
            SecondaryPerformance => PowerModeState.Performance,
            _ => PowerModeState.Balance,
        };
    }

    private static byte ToRaw(PowerModeState state, LayoutType layout)
    {
        if (layout == LayoutType.Primary)
        {
            return state switch
            {
                PowerModeState.Quiet => PrimaryOffice,
                PowerModeState.Performance => PrimaryTurbo,
                _ => PrimaryGaming,
            };
        }

        return state switch
        {
            PowerModeState.Quiet => SecondaryQuiet,
            PowerModeState.Performance => SecondaryPerformance,
            _ => SecondaryBalance,
        };
    }

    public static async Task<bool> IsTongfangMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            var model = mi.Model ?? string.Empty;

            return vendor.Contains("Tongfang", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Tsinghua Tongfang", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("THTF", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("MECHREVO", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Mechanical Revolution", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("MACHENIKE", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Machenike", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("THUNDEROBOT", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Thunderobot", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("COLORFUL", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("MAIBENBEN", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Uniwill", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Eluktronics", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Hasee", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("Jiaolong", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("Kuangshi", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("MECH-", StringComparison.OrdinalIgnoreCase) ||
                   model.Contains("ZhanShen", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
