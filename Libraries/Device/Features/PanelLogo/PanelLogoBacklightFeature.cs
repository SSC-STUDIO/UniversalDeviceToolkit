namespace UniversalDeviceToolkit.Lib.Features.PanelLogo;

public class PanelLogoBacklightFeature(
    PanelLogoLenovoLightingBacklightFeature lenovoLightingFeature,
    PanelLogoSpectrumBacklightFeature spectrumFeature)
    : AbstractCompositeFeature<PanelLogoBacklightState>(lenovoLightingFeature, spectrumFeature);
