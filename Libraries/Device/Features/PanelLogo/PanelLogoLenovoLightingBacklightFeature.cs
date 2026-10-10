using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Features.PanelLogo;

public class PanelLogoLenovoLightingBacklightFeature() : AbstractLenovoLightingFeature<PanelLogoBacklightState>(3, 1, 0)
{
    public override async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var machineInformation = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        if (machineInformation.Properties.IsExcludedFromPanelLogoLenovoLighting)
            return false;

        return await base.IsSupportedAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override PanelLogoBacklightState FromInternal(int stateType, int _) => (PanelLogoBacklightState)stateType;

    protected override (int stateType, int level) ToInternal(PanelLogoBacklightState state) => ((int)state, 0);
}
