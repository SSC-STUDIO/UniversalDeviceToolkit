using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Serialization;

namespace UniversalDeviceToolkit.Lib.Settings;

public class ApplicationSettings : AbstractSettings<ApplicationSettings.ApplicationSettingsStore>
{
    public class Notifications
    {
        public bool UpdateAvailable { get; set; } = true;
        public bool CapsNumLock { get; set; }
        public bool FnLock { get; set; }
        public bool TouchpadLock { get; set; } = true;
        public bool KeyboardBacklight { get; set; } = true;
        public bool CameraLock { get; set; } = true;
        public bool Microphone { get; set; } = true;
        public bool PowerMode { get; set; }
        public bool RefreshRate { get; set; } = true;
        public bool ACAdapter { get; set; }
        public bool SmartKey { get; set; }
        public bool AutomationNotification { get; set; } = true;

        /// <summary>In-app success toasts (plugin install, optimization, cleanup). Default on.</summary>
        public bool SuccessNotifications { get; set; } = true;

        /// <summary>Optional short beep when a toast is published. Default off.</summary>
        public bool NotificationSound { get; set; }

        /// <summary>
        /// Optional per-category policies (persist / severity). Enable follows the
        /// legacy bool toggles above — the settings UI writes those flags, not this map.
        /// Missing keys are filled on load from those toggles.
        /// </summary>
        public Dictionary<string, NotificationTypePolicy> TypePolicies { get; set; } = NotificationTypePolicyStore.CreateDefaults();
    }

    public class ApplicationSettingsStore
    {
        public Theme Theme { get; set; }
        public ThemeStylePreset ThemeStylePreset { get; set; } = ThemeStylePreset.Default;
        public RGBColor? AccentColor { get; set; }
        public AccentColorSource AccentColorSource { get; set; }

        /// <summary>When enabled, user-selected accent colors are also written to Windows.</summary>
        public bool ApplyAccentColorToSystem { get; set; } = true;

        /// <summary>
        /// When true, the resolved accent also tints the theme-style surface palette
        /// (backgrounds / fills / strokes via ThemeStylePreset). The accent itself
        /// (selection rings, links, ApplicationAccentColorManager) always applies
        /// regardless of this flag — see WPF ThemeManager.SetColor vs ApplyStylePreset.
        /// Default true; missing legacy JSON fields keep this initializer value.
        /// </summary>
        public bool ApplyAccentColorToTheme { get; set; } = true;
        public WindowBackdropStyle WindowBackdropStyle { get; set; } = WindowBackdropStyle.Windows;
        public AppFontStyle AppFontStyle { get; set; } = AppFontStyle.Default;
        public AppTextSize AppTextSize { get; set; } = AppTextSize.Standard;
        public AppScale AppScale { get; set; } = AppScale.Standard;
        public PowerModeMappingMode PowerModeMappingMode { get; set; } = PowerModeMappingMode.WindowsPowerMode;
        public Dictionary<PowerModeState, Guid> PowerPlans { get; set; } = [];
        public Dictionary<PowerModeState, WindowsPowerMode> PowerModes { get; set; } = [];
        public bool MinimizeToTray { get; set; } = true;
        public bool MinimizeOnClose { get; set; }
        public WindowSize? WindowSize { get; set; }
        public WindowPlacement? WindowPlacement { get; set; }
        public bool DontShowNotifications { get; set; }
        public NotificationPosition NotificationPosition { get; set; } = NotificationPosition.BottomRight;
        public NotificationDuration NotificationDuration { get; set; } = NotificationDuration.Normal;
        public bool NotificationAlwaysOnTop { get; set; }
        public bool NotificationOnAllScreens { get; set; }
        public bool AnimationsEnabled { get; set; } = true;
        public double AnimationSpeed { get; set; } = 2.0;
        public bool NavigationPaneExpanded { get; set; } = true;
        public Notifications Notifications { get; set; } = new();
        public TemperatureUnit TemperatureUnit { get; set; }
        public List<RefreshRate> ExcludedRefreshRates { get; set; } = [];
        public WarrantyInfo? WarrantyInfo { get; set; }
        public Guid? SmartKeySinglePressActionId { get; set; }
        public Guid? SmartKeyDoublePressActionId { get; set; }
        public List<Guid> SmartKeySinglePressActionList { get; set; } = [];
        public List<Guid> SmartKeyDoublePressActionList { get; set; } = [];
        public bool SynchronizeBrightnessToAllPowerPlans { get; set; }
        public ModifierKey SmartFnLockFlags { get; set; }
        public bool ResetBatteryOnSinceTimerOnReboot { get; set; }
        public bool DisableUnsupportedHardwareWarning { get; set; }
        public bool ForceSoftwareRendering { get; set; }
        public bool EnableHardwareSensors { get; set; }
        public List<string> ExcludedProcesses { get; set; } = [];

        public List<CustomCleanupRule> CustomCleanupRules { get; set; } = [];

        public List<string>? SelectedCleanupActions { get; set; }
        public List<string>? SelectedOptimizationActions { get; set; }
        public int LastWindowsOptimizationPageMode { get; set; }
        public Dictionary<string, bool> NavigationItemsVisibility { get; set; } = new()
        {
            { "keyboard", true },
            { "mouse", true },
            { "battery", true },
            { "automation", true },
            { "macro", true },
            { "windowsOptimization", true },

            { "about", true }
        };
    }

    public ApplicationSettings() : base("settings.json") { }

    protected override void ConfigureJsonSerializerOptions(JsonSerializerOptions options)
    {
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());
    }

    public override ApplicationSettingsStore? LoadStore()
    {
        var store = base.LoadStore();

        if (store is null)
            return Default;

        return Normalize(store);
    }

    public override async Task<ApplicationSettingsStore?> LoadStoreAsync()
    {
        var store = await base.LoadStoreAsync().ConfigureAwait(false);

        if (store is null)
            return Default;

        return Normalize(store);
    }

    private static ApplicationSettingsStore Normalize(ApplicationSettingsStore store)
    {
        store.PowerPlans ??= [];
        store.PowerModes ??= [];
        store.Notifications ??= new();
        NotificationTypePolicyStore.EnsurePolicies(store.Notifications);
        store.ExcludedRefreshRates ??= [];
        store.SmartKeySinglePressActionList ??= [];
        store.SmartKeyDoublePressActionList ??= [];
        store.ExcludedProcesses ??= [];
        store.CustomCleanupRules = NormalizeCleanupRules(store.CustomCleanupRules);
        store.NavigationItemsVisibility ??= new ApplicationSettingsStore().NavigationItemsVisibility;
        // Fill any missing nav keys with defaults without overwriting values the user already
        // persisted (except the one-time migration from the former hidden default below).
        var defaults = new ApplicationSettingsStore().NavigationItemsVisibility;
        foreach (var pair in defaults)
        {
            if (!store.NavigationItemsVisibility.ContainsKey(pair.Key))
                store.NavigationItemsVisibility[pair.Key] = pair.Value;
        }

        // Older builds kept a plugin-extensions nav entry; drop it now that the
        // extension system has been retired.
        store.NavigationItemsVisibility.Remove("pluginExtensions");

        return store;
    }

    private static List<CustomCleanupRule> NormalizeCleanupRules(List<CustomCleanupRule>? rules)
    {
        if (rules is null)
            return [];

        var normalized = new List<CustomCleanupRule>();
        foreach (var rule in rules)
        {
            if (rule is null)
                continue;

            rule.DirectoryPath ??= string.Empty;
            rule.Extensions ??= [];
            normalized.Add(rule);
        }

        return normalized;
    }
}

public class CustomCleanupRule
{
    public string DirectoryPath { get; set; } = string.Empty;
    public List<string> Extensions { get; set; } = [];
    public bool Recursive { get; set; } = true;
}
