using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Host.Settings;

public enum DashboardGroupType
{
    Power,
    Graphics,
    Display,
    Other,
    Custom
}

public enum DashboardItem
{
    PowerMode,
    BatteryMode,
    BatteryNightChargeMode,
    AlwaysOnUsb,
    InstantBoot,
    HybridMode,
    DiscreteGpu,
    OverclockDiscreteGpu,
    PanelLogoBacklight,
    PortsBacklight,
    Resolution,
    RefreshRate,
    DpiScale,
    Hdr,
    OverDrive,
    TurnOffMonitors,
    Microphone,
    FlipToStart,
    TouchpadLock,
    FnLock,
    WinKeyLock,
    WhiteKeyboardBacklight,
    ItsMode
}

public sealed class DashboardGroup
{
    public DashboardGroupType Type { get; set; }

    public string? CustomName { get; set; }

    public List<DashboardItem> Items { get; set; } = [];

    /// <summary>Mirrors the WPF DashboardGroup.DefaultGroups layout.</summary>
    public static List<DashboardGroup> DefaultGroups => new()
    {
        new()
        {
            Type = DashboardGroupType.Power,
            Items =
            [
                DashboardItem.PowerMode,
                DashboardItem.ItsMode,
                DashboardItem.BatteryMode,
                DashboardItem.BatteryNightChargeMode,
                DashboardItem.AlwaysOnUsb,
                DashboardItem.InstantBoot,
                DashboardItem.FlipToStart,
            ],
        },
        new()
        {
            Type = DashboardGroupType.Graphics,
            Items =
            [
                DashboardItem.HybridMode,
                DashboardItem.DiscreteGpu,
                DashboardItem.OverclockDiscreteGpu,
            ],
        },
        new()
        {
            Type = DashboardGroupType.Display,
            Items =
            [
                DashboardItem.Resolution,
                DashboardItem.RefreshRate,
                DashboardItem.DpiScale,
                DashboardItem.Hdr,
                DashboardItem.OverDrive,
                DashboardItem.TurnOffMonitors,
            ],
        },
        new()
        {
            Type = DashboardGroupType.Other,
            Items =
            [
                DashboardItem.Microphone,
                DashboardItem.WhiteKeyboardBacklight,
                DashboardItem.PanelLogoBacklight,
                DashboardItem.PortsBacklight,
                DashboardItem.TouchpadLock,
                DashboardItem.FnLock,
                DashboardItem.WinKeyLock,
            ],
        },
    };
}

/// <summary>
/// Host-side copy of the WPF dashboard.json settings (same schema) so the
/// Electron frontend can read/customize the dashboard without the WPF app.
/// </summary>
public sealed class HostDashboardSettings() : AbstractSettings<HostDashboardSettings.DashboardSettingsStore>("dashboard.json")
{
    public const int CurrentSchemaVersion = 4;
    public const double MinSensorsRefreshIntervalSeconds = 0.5;
    public const double MaxSensorsRefreshIntervalSeconds = 30;

    public sealed class DashboardSettingsStore
    {
        /// <summary>Electron <c>DashboardSettingsStore.schemaVersion</c> (schema v4).</summary>
        public int? SchemaVersion { get; set; } = CurrentSchemaVersion;

        public bool ShowSensors { get; set; } = true;
        public double SensorsRefreshIntervalSeconds { get; set; } = 1;
        public List<DashboardGroup>? Groups { get; set; }
    }

    protected override DashboardSettingsStore Default => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Groups = DashboardGroup.DefaultGroups,
    };

    protected override void ConfigureJsonSerializerOptions(JsonSerializerOptions options)
    {
        options.PropertyNameCaseInsensitive = true;
    }

    public override DashboardSettingsStore? LoadStore()
    {
        var store = base.LoadStore();
        return store is null ? null : Normalize(store);
    }

    public override async Task<DashboardSettingsStore?> LoadStoreAsync()
    {
        var store = await base.LoadStoreAsync().ConfigureAwait(false);
        return store is null ? null : Normalize(store);
    }

    /// <summary>
    /// Clamps the refresh interval to the Electron subscribe range and restores
    /// built-in groups when the persisted list is empty or contains no items.
    /// </summary>
    public static DashboardSettingsStore Normalize(DashboardSettingsStore store)
    {
        store.SchemaVersion = CurrentSchemaVersion;
        store.SensorsRefreshIntervalSeconds = NormalizeRefreshInterval(store.SensorsRefreshIntervalSeconds);
        store.Groups = NormalizeGroups(store.Groups);
        return store;
    }

    public void NormalizeStore() => Normalize(Store);

    private static double NormalizeRefreshInterval(double seconds)
    {
        if (!double.IsFinite(seconds))
            return 1;
        return Math.Clamp(seconds, MinSensorsRefreshIntervalSeconds, MaxSensorsRefreshIntervalSeconds);
    }

    private static List<DashboardGroup> NormalizeGroups(List<DashboardGroup>? groups)
    {
        if (groups is not { Count: > 0 })
            return DashboardGroup.DefaultGroups;

        var normalized = new List<DashboardGroup>(groups.Count);
        foreach (var group in groups)
        {
            if (!Enum.IsDefined(group.Type))
                continue;

            var items = (group.Items ?? [])
                .Where(Enum.IsDefined)
                .Distinct()
                .ToList();

            normalized.Add(new DashboardGroup
            {
                Type = group.Type,
                CustomName = string.IsNullOrWhiteSpace(group.CustomName) ? null : group.CustomName.Trim(),
                Items = items,
            });
        }

        return normalized.Count > 0 ? normalized : DashboardGroup.DefaultGroups;
    }
}
