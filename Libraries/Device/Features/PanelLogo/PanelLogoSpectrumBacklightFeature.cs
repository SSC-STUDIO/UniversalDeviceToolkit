using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;

namespace UniversalDeviceToolkit.Lib.Features.PanelLogo;

public class PanelLogoSpectrumBacklightFeature(SpectrumKeyboardBacklightController controller) : IFeature<PanelLogoBacklightState>
{
    public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isSupported = await controller.IsSupportedAsync().ConfigureAwait(false);
        if (!isSupported)
            return false;

        var (layout, _, _) = await controller.GetKeyboardLayoutAsync().ConfigureAwait(false);
        return layout == SpectrumLayout.Full;
    }

    public Task<PanelLogoBacklightState[]> GetAllStatesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enum.GetValues<PanelLogoBacklightState>());
    }

    public async Task<PanelLogoBacklightState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await controller.GetLogoStatusAsync().ConfigureAwait(false)
            ? PanelLogoBacklightState.On
            : PanelLogoBacklightState.Off;
    }

    public Task SetStateAsync(PanelLogoBacklightState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return controller.SetLogoStatusAsync(state == PanelLogoBacklightState.On);
    }

    public void InvalidateResolution()
    {
    }
}
