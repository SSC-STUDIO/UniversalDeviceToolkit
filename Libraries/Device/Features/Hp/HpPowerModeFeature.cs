using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Hp;

/// <summary>
/// HP OMEN/Victus performance modes over the HP WMI BIOS interface (protocol
/// mirrors OmenMon's BiosCtl and the Linux hp-wmi driver):
///   set via command type 0x1A on the default channel {0xFF, mode, 0, 0},
///   values from the thermal policy version (command 0x28, byte 3):
///   V0: Default 0x00 / Performance 0x01 / Cool 0x02,
///   V1: Balanced 0x30 / Performance 0x31 / Cool 0x50.
/// Mode reads are EC-only, so the feature tracks the session state and defaults
/// to Balance at startup. Self-disables on non-HP machines or when the BIOS
/// interface is absent.
/// </summary>
public class HpPowerModeFeature(IHpWmiBios bios) : IFeature<PowerModeState>
{
    private const uint CmdFanCount = 0x10;
    private const uint CmdSetPerformanceMode = 0x1A;
    private const uint CmdSystemDesignData = 0x28;

    private bool _probed;
    private bool _isV1 = true;
    private PowerModeState _lastSet = PowerModeState.Balance;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!bios.IsAvailable || !await IsHpMachineAsync().ConfigureAwait(false))
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

        // The performance-mode register is EC-only; without an EC channel the
        // session state is the best available answer (starts at Balance).
        return _lastSet;
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var (returnCode, _) = bios.Execute(CmdSetPerformanceMode, [0xFF, (byte)ToRaw(state), 0x00, 0x00]);
        if (returnCode != 0)
            throw ExceptionHelper.PowerModeNotSupported();

        _lastSet = state;
    }

    public void InvalidateResolution()
    {
        _probed = false;
        _isV1 = true;
    }

    private async Task<bool> ProbeAsync()
    {
        if (_probed)
            return true;
        _probed = true;

        // Fan-count probe doubles as the interface liveness check (OmenMon uses
        // it the same way); ignore the returned count.
        var (probeCode, _) = bios.Execute(CmdFanCount, [0, 0, 0, 0]);
        if (probeCode != 0)
            return false;

        var (designCode, designData) = bios.Execute(CmdSystemDesignData, [0, 0, 0, 0]);
        if (designCode == 0 && designData.Length > 3)
            _isV1 = designData[3] != 0;

        return true;
    }

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private int ToRaw(PowerModeState state) => _isV1
        ? state switch
        {
            PowerModeState.Quiet => 0x50,
            PowerModeState.Performance => 0x31,
            _ => 0x30,
        }
        : state switch
        {
            PowerModeState.Quiet => 0x02,
            PowerModeState.Performance => 0x01,
            _ => 0x00,
        };

    private static async Task<bool> IsHpMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            var vendor = mi.Vendor ?? string.Empty;
            return vendor.Contains("HP", StringComparison.OrdinalIgnoreCase) ||
                   vendor.Contains("Hewlett", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
