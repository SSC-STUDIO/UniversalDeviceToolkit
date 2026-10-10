using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.GameDetection;

public class GameBoostSettings() : AbstractSettings<GameBoostSettings.GameBoostSettingsStore>("gameBoost.json")
{
    public class GameBoostSettingsStore
    {
        public bool AutoGameBoost { get; set; } = true;
        public bool BoostGamePriority { get; set; } = true;
        public bool OptimizeCpuAffinity { get; set; } = true;
        public bool SuppressBackgroundProcesses { get; set; } = true;
        public bool MuteNotifications { get; set; } = false;
        public string? GamePowerPlanGuid { get; set; }

        public List<string> CustomGameProcesses { get; set; } = [];
        public List<string> BackgroundWhitelist { get; set; } =
        [
            "obs64",
            "obs32",
            "discord",
            "steam",
            "steamwebhelper",
            "epicgameslauncher",
            "voicemeeter",
            "voicemeeterpro",
            "voicemeeter8",
            "spotify",
            "devenv",
            "rider64",
            "code",
            "UniversalDeviceToolkit",
            "UniversalDeviceToolkit.Host",
            "UniversalDeviceToolkit.Electron"
        ];
    }

    public override GameBoostSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<GameBoostSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    internal static GameBoostSettingsStore? Normalize(GameBoostSettingsStore? store)
    {
        if (store is null)
            return null;

        var defaults = new GameBoostSettingsStore();

        store.CustomGameProcesses = NormalizeProcessList(store.CustomGameProcesses);
        store.BackgroundWhitelist = NormalizeProcessList(store.BackgroundWhitelist, defaults.BackgroundWhitelist);

        if (string.IsNullOrWhiteSpace(store.GamePowerPlanGuid))
            store.GamePowerPlanGuid = null;

        return store;
    }

    private static List<string> NormalizeProcessList(List<string>? list, List<string>? fallback = null)
    {
        if (list is null)
            return fallback ?? [];

        return list
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
