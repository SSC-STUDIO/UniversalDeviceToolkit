using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using static UniversalDeviceToolkit.Lib.Settings.FanCurveSettings;

namespace UniversalDeviceToolkit.Lib.Settings;


public class FanCurveSettings() : AbstractSettings<FanCurveSettingsStore>("fan_curves.json")
{
    public class FanCurveSettingsStore
    {
        public List<FanCurveEntry> Entries { get; set; } = [];
    }

    public override FanCurveSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<FanCurveSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    private static FanCurveSettingsStore? Normalize(FanCurveSettingsStore? store)
    {
        if (store is null)
            return null;

        store.Entries ??= [];
        return store;
    }
}
