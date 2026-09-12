using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;

namespace UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight;

public class WhiteKeyboardBacklightFeature(WhiteKeyboardLenovoLightingBacklightFeature feature1,
    WhiteKeyboardDriverBacklightFeature feature2,
    SpectrumKeyboardBacklightController spectrumController,
    RGBKeyboardBacklightController rgbController)
    : AbstractCompositeFeature<WhiteKeyboardBacklightState>(feature1, feature2)
{
    protected override async Task<IFeature<WhiteKeyboardBacklightState>?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (await spectrumController.IsSupportedAsync().ConfigureAwait(false) || await rgbController.IsSupportedAsync().ConfigureAwait(false))
            return null;

        return await base.ResolveAsync(cancellationToken).ConfigureAwait(false);
    }
}
