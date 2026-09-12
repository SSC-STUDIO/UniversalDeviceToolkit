using System;
using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.Settings;

/// <summary>
/// Per-notification-type policy: enable, optional persistence hint, and severity.
/// Position remains a global setting (default bottom-right); OSD stays separate.
/// </summary>
public sealed class NotificationTypePolicy
{
    public bool Enabled { get; set; } = true;
    public bool Persist { get; set; }
    public NotificationPriority Severity { get; set; } = NotificationPriority.Normal;
}

public static class NotificationTypePolicyStore
{
    public static NotificationTypePolicy GetOrDefault(
        Dictionary<string, NotificationTypePolicy>? policies,
        string key,
        bool legacyEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Policy key is required.", nameof(key));

        if (TryFind(policies, key, out var policy))
        {
            return Clone(policy);
        }

        return new NotificationTypePolicy
        {
            Enabled = legacyEnabled,
            Persist = false,
            Severity = NotificationPriority.Normal
        };
    }

    /// <summary>
    /// Resolves persist/severity from <see cref="ApplicationSettings.Notifications.TypePolicies"/>
    /// and enable from the legacy bool toggles the settings UI actually writes.
    /// </summary>
    public static NotificationTypePolicy Resolve(ApplicationSettings.Notifications notifications, string key)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Policy key is required.", nameof(key));

        var legacyEnabled = GetLegacyEnabled(notifications, key);
        if (TryFind(notifications.TypePolicies, key, out var policy))
        {
            return new NotificationTypePolicy
            {
                Enabled = legacyEnabled,
                Persist = policy.Persist,
                Severity = policy.Severity
            };
        }

        return new NotificationTypePolicy
        {
            Enabled = legacyEnabled,
            Persist = false,
            Severity = NotificationPriority.Normal
        };
    }

    public static NotificationTypePolicy Resolve(
        ApplicationSettings.Notifications notifications,
        NotificationType type) =>
        Resolve(notifications, ToPolicyKey(type));

    public static bool ShouldShow(ApplicationSettings.ApplicationSettingsStore store, NotificationType type)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (store.DontShowNotifications)
            return false;

        return Resolve(store.Notifications ?? new(), type).Enabled;
    }

    public static bool GetLegacyEnabled(ApplicationSettings.Notifications notifications, string key)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        if (string.IsNullOrWhiteSpace(key))
            return true;

        return key.ToUpperInvariant() switch
        {
            "UPDATEAVAILABLE" => notifications.UpdateAvailable,
            "CAPSNUMLOCK" => notifications.CapsNumLock,
            "FNLOCK" => notifications.FnLock,
            "TOUCHPADLOCK" => notifications.TouchpadLock,
            "KEYBOARDBACKLIGHT" => notifications.KeyboardBacklight,
            "CAMERALOCK" => notifications.CameraLock,
            "MICROPHONE" => notifications.Microphone,
            "POWERMODE" => notifications.PowerMode,
            "REFRESHRATE" => notifications.RefreshRate,
            "ACADAPTER" => notifications.ACAdapter,
            "SMARTKEY" => notifications.SmartKey,
            "AUTOMATIONNOTIFICATION" => notifications.AutomationNotification,
            _ => true
        };
    }

    public static string ToPolicyKey(NotificationType type) => type switch
    {
        NotificationType.UpdateAvailable => "UpdateAvailable",
        NotificationType.CapsLockOn or NotificationType.CapsLockOff
            or NotificationType.NumLockOn or NotificationType.NumLockOff => "CapsNumLock",
        NotificationType.FnLockOn or NotificationType.FnLockOff => "FnLock",
        NotificationType.TouchpadOn or NotificationType.TouchpadOff => "TouchpadLock",
        NotificationType.RGBKeyboardBacklightChanged or NotificationType.RGBKeyboardBacklightOff
            or NotificationType.SpectrumBacklightChanged or NotificationType.SpectrumBacklightOff
            or NotificationType.SpectrumBacklightPresetChanged
            or NotificationType.WhiteKeyboardBacklightChanged or NotificationType.WhiteKeyboardBacklightOff
            or NotificationType.PanelLogoLightingOn or NotificationType.PanelLogoLightingOff
            or NotificationType.PortLightingOn or NotificationType.PortLightingOff => "KeyboardBacklight",
        NotificationType.CameraOn or NotificationType.CameraOff => "CameraLock",
        NotificationType.MicrophoneOn or NotificationType.MicrophoneOff => "Microphone",
        NotificationType.PowerModeQuiet or NotificationType.PowerModeBalance
            or NotificationType.PowerModePerformance or NotificationType.PowerModeExtreme
            or NotificationType.PowerModeGodMode
            or NotificationType.ITSModeAuto or NotificationType.ITSModeCool
            or NotificationType.ITSModePerformance or NotificationType.ITSModeGeek => "PowerMode",
        NotificationType.RefreshRate => "RefreshRate",
        NotificationType.ACAdapterConnected or NotificationType.ACAdapterConnectedLowWattage
            or NotificationType.ACAdapterDisconnected => "ACAdapter",
        NotificationType.SmartKeySinglePress or NotificationType.SmartKeyDoublePress => "SmartKey",
        NotificationType.AutomationNotification => "AutomationNotification",
        _ => type.ToString(),
    };

    /// <summary>
    /// Rebuilds policies with a case-insensitive comparer, fills keys added in later
    /// releases, and mirrors enable from the legacy toggles the settings UI persists.
    /// </summary>
    public static Dictionary<string, NotificationTypePolicy> EnsurePolicies(
        ApplicationSettings.Notifications notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        var result = new Dictionary<string, NotificationTypePolicy>(StringComparer.OrdinalIgnoreCase);
        if (notifications.TypePolicies is not null)
        {
            foreach (var pair in notifications.TypePolicies)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                    continue;
                result[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in CreateDefaults())
        {
            var enabled = GetLegacyEnabled(notifications, pair.Key);
            if (result.TryGetValue(pair.Key, out var existing))
            {
                existing.Enabled = enabled;
                continue;
            }

            result[pair.Key] = new NotificationTypePolicy
            {
                Enabled = enabled,
                Persist = pair.Value.Persist,
                Severity = pair.Value.Severity
            };
        }

        notifications.TypePolicies = result;
        return result;
    }

    public static Dictionary<string, NotificationTypePolicy> CreateDefaults() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["UpdateAvailable"] = new() { Enabled = true },
            ["CapsNumLock"] = new() { Enabled = false },
            ["FnLock"] = new() { Enabled = false },
            ["TouchpadLock"] = new() { Enabled = true },
            ["KeyboardBacklight"] = new() { Enabled = true },
            ["CameraLock"] = new() { Enabled = true },
            ["Microphone"] = new() { Enabled = true },
            ["PowerMode"] = new() { Enabled = false },
            ["RefreshRate"] = new() { Enabled = true },
            ["ACAdapter"] = new() { Enabled = false },
            ["SmartKey"] = new() { Enabled = false },
            ["AutomationNotification"] = new() { Enabled = true },
        };

    private static bool TryFind(
        Dictionary<string, NotificationTypePolicy>? policies,
        string key,
        out NotificationTypePolicy policy)
    {
        policy = null!;
        if (policies is null)
            return false;

        if (policies.TryGetValue(key, out var exact) && exact is not null)
        {
            policy = exact;
            return true;
        }

        foreach (var pair in policies)
        {
            if (pair.Value is null)
                continue;
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                policy = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static NotificationTypePolicy Clone(NotificationTypePolicy policy) => new()
    {
        Enabled = policy.Enabled,
        Persist = policy.Persist,
        Severity = policy.Severity
    };
}
