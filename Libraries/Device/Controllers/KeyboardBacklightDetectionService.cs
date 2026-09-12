using global::System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Lib.Controllers;

/// <summary>Adapts Windows keyboard controllers to the platform-neutral hardware abstractions.</summary>
public sealed class KeyboardBacklightDetectionService(
    RGBKeyboardBacklightController rgbController,
    SpectrumKeyboardBacklightController spectrumController) : IKeyboardBacklightDetectionService
{
    public Task<bool> IsSpectrumSupportedAsync() => spectrumController.IsSupportedAsync();

    public Task<bool> IsRgbSupportedAsync() => rgbController.IsSupportedAsync();
}
