namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Platform-agnostic service for detecting keyboard backlight capabilities.
/// </summary>
public interface IKeyboardBacklightDetectionService
{
    /// <summary>
    /// Checks whether Spectrum keyboard backlight is supported.
    /// </summary>
    Task<bool> IsSpectrumSupportedAsync();

    /// <summary>
    /// Checks whether RGB keyboard backlight is supported.
    /// </summary>
    Task<bool> IsRgbSupportedAsync();
}
