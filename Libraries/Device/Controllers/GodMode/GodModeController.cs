using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.GodMode;

/// <summary>
/// God Mode controller that automatically selects V1 or V2 implementation based on hardware version.
/// </summary>
/// <remarks>
/// <para>
/// This controller acts as a Facade, automatically selecting the appropriate implementation based on machine info:
/// </para>
/// <list type="bullet">
///   <item><description>GodModeControllerV1 - For older Legion devices</description></item>
///   <item><description>GodModeControllerV2 - For newer Legion devices</description></item>
/// </list>
/// </remarks>
public class GodModeController(GodModeControllerV1 controllerV1, GodModeControllerV2 controllerV2)
    : IGodModeController
{
    private IGodModeController ControllerV1 => controllerV1;
    private IGodModeController ControllerV2 => controllerV2;

    public event EventHandler<Guid>? PresetChanged
    {
        add
        {
            ControllerV1.PresetChanged += value;
            ControllerV2.PresetChanged += value;
        }
        remove
        {
            ControllerV1.PresetChanged -= value;
            ControllerV2.PresetChanged -= value;
        }
    }

    public async Task<bool> NeedsVantageDisabledAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.NeedsVantageDisabledAsync().ConfigureAwait(false);
    }

    public async Task<bool> NeedsLegionZoneDisabledAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.NeedsLegionZoneDisabledAsync().ConfigureAwait(false);
    }

    public async Task<Guid> GetActivePresetIdAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.GetActivePresetIdAsync().ConfigureAwait(false);
    }

    public async Task<string?> GetActivePresetNameAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.GetActivePresetNameAsync().ConfigureAwait(false);
    }

    public async Task<GodModeState> GetStateAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.GetStateAsync().ConfigureAwait(false);
    }

    public async Task SetStateAsync(GodModeState state)
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        await controller.SetStateAsync(state).ConfigureAwait(false);
    }

    public async Task ApplyStateAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        await controller.ApplyStateAsync().ConfigureAwait(false);
    }

    public async Task<FanTable> GetDefaultFanTableAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.GetDefaultFanTableAsync().ConfigureAwait(false);
    }

    public async Task<FanTable> GetMinimumFanTableAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.GetMinimumFanTableAsync().ConfigureAwait(false);
    }

    public async Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        return await controller.GetDefaultsInOtherPowerModesAsync().ConfigureAwait(false);
    }

    public async Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state)
    {
        var controller = await GetControllerAsync().ConfigureAwait(false);
        await controller.RestoreDefaultsInOtherPowerModeAsync(state).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks whether the current device supports God Mode functionality.
    /// </summary>
    /// <returns>Returns true if the device supports God Mode, false otherwise.</returns>
    public async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (!Compatibility.IsSupportedLegionMachine(mi))
            return false;

        return mi.Properties.SupportsGodMode;
    }

    /// <summary>
    /// Gets the appropriate controller implementation based on machine info.
    /// </summary>
    /// <returns>An IGodModeController implementation suitable for the current hardware.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no supported version is found.</exception>
    private async Task<IGodModeController> GetControllerAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        if (mi.Properties.SupportsGodModeV1)
            return controllerV1;

        if (mi.Properties.SupportsGodModeV2 || mi.Properties.SupportsGodModeV3 || mi.Properties.SupportsGodModeV4)
            return controllerV2;

        throw ExceptionHelper.NoSupportedVersionFound();
    }
}
