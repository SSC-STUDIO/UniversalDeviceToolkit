using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Lighting;

/// <summary>
/// Capability gate for 24-zone / ambient lighting devices.
/// Returns unsupported until a verified device protocol probe exists — never expose fake UI.
/// </summary>
public static class Keyboard24ZoneLightingCapability
{
    /// <summary>
    /// True only when firmware/protocol evidence confirms a 24-zone lighting device.
    /// Current matrix status: not confirmed — always false.
    /// </summary>
    public static Task<bool> IsSupportedAsync()
    {
        // Intentionally gated: KeyboardLayout.Keyboard24Zone exists for forward-compat,
        // but Spectrum layout detection never reports it without zone evidence.
        return Task.FromResult(false);
    }

    /// <summary>
    /// Special-key LED/actions that depend on Lenovo spectrum firmware stay hidden
    /// unless the spectrum controller reports a live device handle.
    /// </summary>
    public static async Task<bool> AreSpectrumSpecialKeysSupportedAsync()
    {
        try
        {
            var spectrum = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
            return await spectrum.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "lighting-spectrum-special-keys",
                "Spectrum special-keys capability probe failed.",
                ex);
            return false;
        }
    }
}
