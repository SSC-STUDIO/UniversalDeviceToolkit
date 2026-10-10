namespace UniversalDeviceToolkit.Abstractions.Macro;

/// <summary>
/// Platform-agnostic abstraction for controlling macro recording and playback.
/// </summary>
public interface IMacroController
{
    /// <summary>
    /// Gets whether the macro feature is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Enables or disables the macro feature.
    /// </summary>
    void SetEnabled(bool enabled);
}
