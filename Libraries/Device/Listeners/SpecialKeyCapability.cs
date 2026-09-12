using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Listeners;

/// <summary>
/// Protocol / interaction metadata for a discoverable special key.
/// Only entries that pass <see cref="SpecialKeyDiscovery"/> capability filters should appear in UI.
/// </summary>
public sealed record SpecialKeyDescriptor(
    SpecialKey Id,
    string Protocol,
    bool SupportsSingleClick,
    bool SupportsDoubleClick,
    bool HasLedFeedback,
    bool RequiresSpectrumDevice,
    bool RequiresWhiteKeyboardDevice,
    bool RequiresLegionMachine)
{
    public string DisplayId => Id.ToString();
}

/// <summary>
/// Static discovery catalog for Lenovo utility-event special keys.
/// Filtering is capability-based — never show empty non-capable UI.
/// </summary>
public static class SpecialKeyDiscovery
{
    private static readonly SpecialKeyDescriptor[] Catalog =
    [
        new(SpecialKey.FnF9, "wmi:lenovo-utility", true, true, false, false, false, true),
        new(SpecialKey.FnLockOn, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnLockOff, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnPrtSc, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnPrtSc2, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.CameraOn, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.CameraOff, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnR, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnR2, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnN, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnF4, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.FnF8, "wmi:lenovo-utility", true, false, false, false, false, true),
        new(SpecialKey.SpectrumBacklightOff, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumBacklight1, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumBacklight2, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumBacklight3, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumPreset1, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumPreset2, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumPreset3, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumPreset4, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumPreset5, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.SpectrumPreset6, "wmi:lenovo-utility", true, false, true, true, false, true),
        new(SpecialKey.WhiteBacklightOff, "wmi:lenovo-utility", true, false, true, false, true, true),
        new(SpecialKey.WhiteBacklight1, "wmi:lenovo-utility", true, false, true, false, true, true),
        new(SpecialKey.WhiteBacklight2, "wmi:lenovo-utility", true, false, true, false, true, true)
    ];

    public static IReadOnlyList<SpecialKeyDescriptor> All => Catalog;

    public static SpecialKeyDescriptor? Find(SpecialKey key) =>
        Catalog.FirstOrDefault(d => d.Id == key);

    /// <summary>
    /// Returns keys that are valid for the given device capability snapshot.
    /// Non-Legion / unsupported devices receive an empty list (hide UI completely).
    /// </summary>
    public static IReadOnlyList<SpecialKeyDescriptor> FilterForDevice(
        bool isLegionMachine,
        bool spectrumSupported,
        bool whiteKeyboardSupported)
    {
        if (!isLegionMachine)
            return Array.Empty<SpecialKeyDescriptor>();

        return Catalog
            .Where(d =>
            {
                if (d.RequiresSpectrumDevice && !spectrumSupported)
                    return false;
                if (d.RequiresWhiteKeyboardDevice && !whiteKeyboardSupported)
                    return false;
                return true;
            })
            .ToList();
    }
}

/// <summary>
/// Isolates LED feedback failures so key actions still complete.
/// </summary>
public static class SpecialKeyLedIsolation
{
    public static void RunLedFeedback(string operation, Action ledAction, Action<Exception>? onFailure = null)
    {
        try
        {
            ledAction();
        }
        catch (Exception ex)
        {
            onFailure?.Invoke(ex);
            Log.Instance.Warning($"Special-key LED feedback failed (action still delivered). [op={operation}]", ex);
        }
    }

    public static async Task RunLedFeedbackAsync(
        string operation,
        Func<Task> ledAction,
        Action<Exception>? onFailure = null)
    {
        try
        {
            await ledAction().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            onFailure?.Invoke(ex);
            Log.Instance.Warning($"Special-key LED feedback failed (action still delivered). [op={operation}]", ex);
        }
    }
}
