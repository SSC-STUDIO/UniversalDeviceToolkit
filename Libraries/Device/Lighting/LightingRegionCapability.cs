using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Lighting;

/// <summary>
/// Capability DTO for lighting regions. UI must use this instead of raw model strings.
/// Unverified protocols stay <see cref="Supported"/> = false.
/// </summary>
public sealed record LightingRegionCapability(
    string RegionId,
    string DisplayName,
    bool Supported,
    string Protocol,
    string? Notes = null);

/// <summary>
/// Central lighting capability gate — 24-zone Spectrum, front/rear ambient, spectrum keys.
/// </summary>
public static class LightingCapabilityGate
{
    public const string RegionSpectrumKeyboard = "spectrum-keyboard";
    public const string RegionSpectrum24Zone = "spectrum-24-zone";
    public const string RegionFrontAmbient = "front-ambient";
    public const string RegionRearAmbient = "rear-ambient";

    /// <summary>
    /// Snapshot of lighting regions for the current machine. Never exposes fake UI.
    /// </summary>
    public static async Task<IReadOnlyList<LightingRegionCapability>> GetRegionsAsync()
    {
        var spectrumSupported = false;
        try
        {
            var spectrum = IoCContainer.TryResolve<SpectrumKeyboardBacklightController>();
            if (spectrum is not null)
                spectrumSupported = await spectrum.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Lighting capability spectrum probe failed.", ex);
            spectrumSupported = false;
        }

        var zone24 = await Keyboard24ZoneLightingCapability.IsSupportedAsync().ConfigureAwait(false);

        return
        [
            new LightingRegionCapability(
                RegionSpectrumKeyboard,
                "Spectrum keyboard",
                spectrumSupported,
                "spectrum-hid",
                spectrumSupported ? null : "No live Spectrum device handle."),
            new LightingRegionCapability(
                RegionSpectrum24Zone,
                "24-zone Spectrum",
                zone24,
                "spectrum-24-zone",
                "Gated until firmware zone evidence exists."),
            new LightingRegionCapability(
                RegionFrontAmbient,
                "Front ambient light",
                Supported: false,
                Protocol: "ambient-front",
                Notes: "Protocol not verified on UDT matrix."),
            new LightingRegionCapability(
                RegionRearAmbient,
                "Rear ambient light",
                Supported: false,
                Protocol: "ambient-rear",
                Notes: "Protocol not verified on UDT matrix.")
        ];
    }

    public static async Task<bool> IsRegionSupportedAsync(string regionId)
    {
        var regions = await GetRegionsAsync().ConfigureAwait(false);
        foreach (var region in regions)
        {
            if (string.Equals(region.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
                return region.Supported;
        }

        return false;
    }
}
