using static UniversalDeviceToolkit.Lib.Settings.BalanceModeSettings;

namespace UniversalDeviceToolkit.Lib.Settings;

public class BalanceModeSettings() : AbstractSettings<BalanceModeSettingsStore>("balancemode.json")
{
    public class BalanceModeSettingsStore
    {
        public bool AIModeEnabled { get; set; }
    }

    // ReSharper disable once StringLiteralTypo
}
