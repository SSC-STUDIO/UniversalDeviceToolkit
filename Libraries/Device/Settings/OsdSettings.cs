using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Settings;

public class OsdSettings() : AbstractSettings<OsdSettings.OsdSettingsStore>("osd.json")
{
    public class OsdSettingsStore
    {
        public bool ShowOsd { get; set; }
        public double OsdRefreshInterval { get; set; } = 1;
        public int SelectedStyleIndex { get; set; } = 0;
        public List<OsdItem> Items { get; set; } = Enum.GetValues<OsdItem>().ToList();

        public double BackgroundOpacity { get; set; } = 0.6;
        public string BackgroundColor { get; set; } = "#1E1E1E";
        public int FontSize { get; set; } = 12;
        public int CornerRadiusTop { get; set; } = 6;
        public int CornerRadiusBottom { get; set; } = 6;
        public bool IsLocked { get; set; } = false;
        public double? PanelPositionX { get; set; }
        public double? PanelPositionY { get; set; }
        public double? BarPositionX { get; set; }
        public double? BarPositionY { get; set; }

        public int TempThresholdWarning { get; set; } = 75;
        public int TempThresholdCritical { get; set; } = 90;
        public int UsageThresholdWarning { get; set; } = 70;
        public int UsageThresholdCritical { get; set; } = 90;
        public int FpsThresholdCritical { get; set; } = 30;
        public int LowFpsDeltaThreshold { get; set; } = 30;

        public string CategoryColor { get; set; } = "#2196F3";
        public string LabelColor { get; set; } = "#ADFF2F";
        public string ValueColor { get; set; } = "#FFFFFF";
        public string WarningColor { get; set; } = "#FFFF00";
        public string CriticalColor { get; set; } = "#FF0000";
        public string SeparatorColor { get; set; } = "#555555";
        public int SnapThreshold { get; set; } = 20;
    }

    public override OsdSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<OsdSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    internal static OsdSettingsStore? Normalize(OsdSettingsStore? store)
    {
        if (store is null)
            return null;

        var defaults = new OsdSettingsStore();

        store.OsdRefreshInterval = ClampFinite(store.OsdRefreshInterval, 0.1, 10, defaults.OsdRefreshInterval);
        store.SelectedStyleIndex = store.SelectedStyleIndex is >= 0 and <= 2
            ? store.SelectedStyleIndex
            : defaults.SelectedStyleIndex;
        store.BackgroundOpacity = ClampFinite(store.BackgroundOpacity, 0, 1, defaults.BackgroundOpacity);
        store.FontSize = Math.Clamp(store.FontSize, 8, 24);
        store.CornerRadiusTop = Math.Clamp(store.CornerRadiusTop, 0, 50);
        store.CornerRadiusBottom = Math.Clamp(store.CornerRadiusBottom, 0, 50);
        store.PanelPositionX = NormalizePosition(store.PanelPositionX);
        store.PanelPositionY = NormalizePosition(store.PanelPositionY);
        store.BarPositionX = NormalizePosition(store.BarPositionX);
        store.BarPositionY = NormalizePosition(store.BarPositionY);
        store.TempThresholdWarning = Math.Clamp(store.TempThresholdWarning, 0, 110);
        store.TempThresholdCritical = Math.Clamp(store.TempThresholdCritical, 0, 110);
        store.UsageThresholdWarning = Math.Clamp(store.UsageThresholdWarning, 0, 100);
        store.UsageThresholdCritical = Math.Clamp(store.UsageThresholdCritical, 0, 100);
        store.FpsThresholdCritical = Math.Clamp(store.FpsThresholdCritical, 0, 1000);
        store.LowFpsDeltaThreshold = Math.Clamp(store.LowFpsDeltaThreshold, 0, 1000);
        store.SnapThreshold = Math.Clamp(store.SnapThreshold, 0, 100);

        store.BackgroundColor = NormalizeColor(store.BackgroundColor, defaults.BackgroundColor);
        store.CategoryColor = NormalizeColor(store.CategoryColor, defaults.CategoryColor);
        store.LabelColor = NormalizeColor(store.LabelColor, defaults.LabelColor);
        store.ValueColor = NormalizeColor(store.ValueColor, defaults.ValueColor);
        store.WarningColor = NormalizeColor(store.WarningColor, defaults.WarningColor);
        store.CriticalColor = NormalizeColor(store.CriticalColor, defaults.CriticalColor);
        store.SeparatorColor = NormalizeColor(store.SeparatorColor, defaults.SeparatorColor);
        store.Items = NormalizeItems(store.Items);
        return store;
    }

    private static double ClampFinite(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static double? NormalizePosition(double? value)
    {
        const double positionLimit = 100_000;
        return value is not null && double.IsFinite(value.Value)
            ? Math.Clamp(value.Value, -positionLimit, positionLimit)
            : null;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (value is null || value.Length != 7 || value[0] != '#')
            return fallback;

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsAsciiHexDigit(value[index]))
                return fallback;
        }

        return value.ToUpperInvariant();
    }

    private static bool IsAsciiHexDigit(char value) =>
        value is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f';

    private static List<OsdItem> NormalizeItems(List<OsdItem>? items)
    {
        if (items is null)
            return Enum.GetValues<OsdItem>().ToList();

        return items
            .Where(Enum.IsDefined)
            .Distinct()
            .ToList();
    }
}
