using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Asus;

/// <summary>
/// ASUS performance modes over the ATKACPI driver (protocol mirrors G-Helper's
/// AsusACPI / Linux asus-wmi). Supported states map as:
///   Silent → Quiet, Balanced → Balance, Turbo/Manual → Performance.
/// Self-disables unless the machine is an ASUS and an ATK performance endpoint
/// (ROG 0x00120075 or Vivobook 0x00110019) responds.
/// </summary>
public class AsusPowerModeFeature(IAsusAtkDriver atk) : IFeature<PowerModeState>
{
    private const uint PerformanceModeRog = 0x00120075;
    private const uint PerformanceModeVivo = 0x00110019;

    private const int RogBalanced = 0;
    private const int RogTurbo = 1;
    private const int RogSilent = 2;
    private const int RogManual = 4;

    private bool _probed;
    private uint _endpoint;
    private bool _isVivo;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!atk.IsAvailable)
            return false;

        if (!await IsAsusMachineAsync().ConfigureAwait(false))
            return false;

        return await ProbeEndpointAsync().ConfigureAwait(false);
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

        var raw = atk.DeviceGet(_endpoint);
        if (raw < 0)
            throw ExceptionHelper.PowerModeNotSupported();

        return ToState(raw);
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var result = atk.DeviceSet(_endpoint, ToRaw(state));
        if (result != 1 && Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"ATK performance-mode write returned {result} (some endpoints never report success); verifying by read-back. [endpoint=0x{_endpoint:X8}]");

        // Some machines do not return a success code (kernel ignores it for this
        // endpoint too), so confirm by reading the mode back.
        var readBack = atk.DeviceGet(_endpoint);
        if (readBack >= 0 && ToState(readBack) != state)
            throw ExceptionHelper.PowerModeNotSupported();
    }

    public void InvalidateResolution()
    {
        _probed = false;
        _endpoint = 0;
        _isVivo = false;
    }

    private async Task<bool> ProbeEndpointAsync()
    {
        if (_probed)
            return _endpoint != 0;

        _probed = true;

        // ROG/TUF endpoint first; Vivobook endpoint swaps Silent/Turbo values.
        if (atk.DeviceGet(PerformanceModeRog) >= 0)
        {
            _endpoint = PerformanceModeRog;
            _isVivo = false;
            return true;
        }

        if (atk.DeviceGet(PerformanceModeVivo) >= 0)
        {
            _endpoint = PerformanceModeVivo;
            _isVivo = true;
            return true;
        }

        _endpoint = 0;
        return false;
    }

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private PowerModeState ToState(int raw) => _isVivo
        ? raw switch
        {
            RogBalanced => PowerModeState.Balance,
            1 => PowerModeState.Quiet,   // vivo: 1 = silent
            2 => PowerModeState.Performance, // vivo: 2 = turbo
            _ => PowerModeState.Balance,
        }
        : raw switch
        {
            RogBalanced => PowerModeState.Balance,
            RogTurbo => PowerModeState.Performance,
            RogSilent => PowerModeState.Quiet,
            RogManual => PowerModeState.Performance,
            _ => PowerModeState.Balance,
        };

    private int ToRaw(PowerModeState state) => _isVivo
        ? state switch
        {
            PowerModeState.Quiet => 1,
            PowerModeState.Performance => 2,
            _ => RogBalanced,
        }
        : state switch
        {
            PowerModeState.Quiet => RogSilent,
            PowerModeState.Performance => RogTurbo,
            _ => RogBalanced,
        };

    private static async Task<bool> IsAsusMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Vendor?.Contains("ASUS", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }
}
