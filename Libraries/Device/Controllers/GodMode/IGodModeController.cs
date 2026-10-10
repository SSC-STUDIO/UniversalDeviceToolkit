using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Controllers.GodMode;

/// <summary>
/// God Mode controller interface for managing custom performance modes and fan curves.
/// </summary>
/// <remarks>
/// God Mode allows users to customize CPU/GPU power limits, fan curves, and other advanced performance parameters.
/// Implementation is provided by GodModeControllerV1 or GodModeControllerV2 depending on hardware version.
/// </remarks>
public interface IGodModeController
{
    /// <summary>
    /// Event raised when a preset is changed.
    /// </summary>
    /// <remarks>
    /// Event arguments contain the newly activated preset ID.
    /// </remarks>
    event EventHandler<Guid> PresetChanged;

    /// <summary>
    /// Checks whether Lenovo Vantage software needs to be disabled.
    /// </summary>
    /// <returns>Returns true if Vantage needs to be disabled, false otherwise.</returns>
    Task<bool> NeedsVantageDisabledAsync();

    /// <summary>
    /// Checks whether Legion Zone software needs to be disabled.
    /// </summary>
    /// <returns>Returns true if Legion Zone needs to be disabled, false otherwise.</returns>
    Task<bool> NeedsLegionZoneDisabledAsync();

    /// <summary>
    /// Gets the currently activated preset ID.
    /// </summary>
    /// <returns>The unique identifier of the currently activated preset.</returns>
    Task<Guid> GetActivePresetIdAsync();

    /// <summary>
    /// Gets the name of the currently activated preset.
    /// </summary>
    /// <returns>The name of the currently activated preset, or null if none.</returns>
    Task<string?> GetActivePresetNameAsync();

    /// <summary>
    /// Gets the current God Mode state.
    /// </summary>
    /// <returns>A GodModeState object containing fan curve, power limit, and other parameters.</returns>
    Task<GodModeState> GetStateAsync();

    /// <summary>
    /// Sets the God Mode state.
    /// </summary>
    /// <param name="state">The God Mode state to set.</param>
    /// <remarks>
    /// This method only updates the in-memory state; call <see cref="ApplyStateAsync"/> to apply to hardware.
    /// </remarks>
    Task SetStateAsync(GodModeState state);

    /// <summary>
    /// Applies the current God Mode state to hardware.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplyStateAsync();

    /// <summary>
    /// Gets the default fan table.
    /// </summary>
    /// <returns>A FanTable object containing the default fan speed curve.</returns>
    Task<FanTable> GetDefaultFanTableAsync();

    /// <summary>
    /// Gets the minimum fan table.
    /// </summary>
    /// <returns>A FanTable object containing the minimum fan speed curve.</returns>
    Task<FanTable> GetMinimumFanTableAsync();

    /// <summary>
    /// Gets default settings for other power modes.
    /// </summary>
    /// <returns>A dictionary mapping power mode states to their default settings.</returns>
    Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync();

    /// <summary>
    /// Restores default settings for the specified power mode.
    /// </summary>
    /// <param name="state">The power mode to restore defaults for.</param>
    Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state);
}