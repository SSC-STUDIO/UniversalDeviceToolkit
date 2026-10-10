using System.Collections.Generic;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.Macro.Utils;

public class MacroSettings() : AbstractSettings<MacroSettings.MacroSettingsStore>("macro.json")
{
    public class MacroSettingsStore
    {
        public bool IsEnabled { get; set; }

        public Dictionary<MacroIdentifier, MacroSequence> Sequences { get; set; } = [];
    }

    protected override MacroSettingsStore Default => new();
}
