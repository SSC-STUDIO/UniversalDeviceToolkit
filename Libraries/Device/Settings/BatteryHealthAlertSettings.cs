using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Settings;

/// <summary>
/// Built-in battery health alert thresholds (migrated from Battery Health plugin).
/// Uses main-app battery samples — no separate WMI polling loop.
/// </summary>
public sealed class BatteryHealthAlertSettings()
    : AbstractSettings<BatteryHealthAlertSettings.BatteryHealthAlertStore>("battery_health_alerts.json")
{
    public const int DefaultLowHealthThreshold = 80;
    public const int DefaultCriticalHealthThreshold = 60;

    protected override BatteryHealthAlertStore Default => new();

    public override BatteryHealthAlertStore? LoadStore()
    {
        var store = base.LoadStore();
        store?.Normalize();
        return store;
    }

    public override async Task<BatteryHealthAlertStore?> LoadStoreAsync()
    {
        var store = await base.LoadStoreAsync().ConfigureAwait(false);
        store?.Normalize();
        return store;
    }

    public sealed class BatteryHealthAlertStore
    {
        /// <summary>Master switch for threshold notifications. Default on so migrated users keep alerts.</summary>
        public bool AlertsEnabled { get; set; } = true;

        public int LowHealthThreshold { get; set; } = DefaultLowHealthThreshold;

        public int CriticalHealthThreshold { get; set; } = DefaultCriticalHealthThreshold;

        /// <summary>Optional temperature alert in °C; 0 disables.</summary>
        public double TemperatureThresholdC { get; set; }

        public void Normalize()
        {
            if (LowHealthThreshold is < 1 or > 100)
                LowHealthThreshold = DefaultLowHealthThreshold;
            if (CriticalHealthThreshold is < 0 or > 99)
                CriticalHealthThreshold = DefaultCriticalHealthThreshold;
            if (CriticalHealthThreshold >= LowHealthThreshold)
                CriticalHealthThreshold = Math.Max(0, LowHealthThreshold - 20);
            if (TemperatureThresholdC is < 0 or > 120)
                TemperatureThresholdC = 0;
        }
    }
}
