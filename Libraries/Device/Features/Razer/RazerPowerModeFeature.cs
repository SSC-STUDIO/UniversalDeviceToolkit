using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Razer;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.Razer;

/// <summary>
/// Razer Blade performance modes over the EC HID protocol (class 0x0D):
///   Balanced 0x00 → Balance, Gaming 0x01 → Performance, Silent 0x05 → Quiet,
///   Creator 0x02 / Custom 0x04 → Performance (best UI mapping).
/// Mode values are written per zone (CPU always; GPU best-effort — Book/Stealth
/// have no dGPU zone). Self-disables on non-Razer machines or when no HID
/// control interface answers.
/// </summary>
public class RazerPowerModeFeature(IRazerHidController controller) : IFeature<PowerModeState>
{
    private const byte ModeBalanced = 0x00;
    private const byte ModeGaming = 0x01;
    private const byte ModeCreator = 0x02;
    private const byte ModeCustom = 0x04;
    private const byte ModeSilent = 0x05;

    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await IsRazerMachineAsync().ConfigureAwait(false))
            return false;

        return controller.Probe();
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

        var raw = controller.GetPerformanceMode(RazerPacket.ZoneCpu);
        return raw switch
        {
            ModeGaming or ModeCreator or ModeCustom => PowerModeState.Performance,
            ModeSilent => PowerModeState.Quiet,
            _ => PowerModeState.Balance,
        };
    }

    public async Task SetStateAsync(PowerModeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (state is not (PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance))
            throw ExceptionHelper.UnsupportedPowerMode(state);

        await ThrowIfUnsupportedAsync(cancellationToken).ConfigureAwait(false);

        var mode = state switch
        {
            PowerModeState.Quiet => ModeSilent,
            PowerModeState.Performance => ModeGaming,
            _ => ModeBalanced,
        };

        if (!controller.SetPerformanceMode(RazerPacket.ZoneCpu, mode, manualFan: false))
            throw ExceptionHelper.PowerModeNotSupported();

        // GPU zone is best-effort: no-dGPU models (Book/Stealth) may reject it.
        controller.SetPerformanceMode(RazerPacket.ZoneGpu, mode, manualFan: false);
    }

    public void InvalidateResolution()
    {
    }

    private async Task ThrowIfUnsupportedAsync(CancellationToken cancellationToken)
    {
        if (!await IsSupportedAsync(cancellationToken).ConfigureAwait(false))
            throw ExceptionHelper.PowerModeNotSupported();
    }

    private static async Task<bool> IsRazerMachineAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi.Vendor?.Contains("Razer", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }
}
