namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

/// <summary>
/// Interface for IGPU Mode feature operations.
/// Enables testability by allowing mock implementations.
/// </summary>
public interface IIGPUModeFeature : IFeature<IGPUModeState>
{
    /// <summary>
    /// Gets or sets whether experimental GPU working mode is enabled.
    /// </summary>
    bool ExperimentalGPUWorkingMode { get; set; }
}
