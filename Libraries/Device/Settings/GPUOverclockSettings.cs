using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Serialization;
using static UniversalDeviceToolkit.Lib.Settings.GPUOverclockSettings;

namespace UniversalDeviceToolkit.Lib.Settings;

public class GPUOverclockSettings() : AbstractSettings<GPUOverclockSettingsStore>("gpu_oc.json")
{
    public const string DefaultProfileName = "Custom";

    protected override void ConfigureJsonSerializerOptions(JsonSerializerOptions options)
    {
        options.Converters.Add(new GPUOverclockInfoJsonConverter());
    }

    public class GPUOverclockSettingsStore
    {
        public class Profile
        {
            public string Name { get; set; } = DefaultProfileName;
            public GPUOverclockInfo Info { get; set; } = GPUOverclockInfo.Zero;
        }

        public bool Enabled { get; set; }

        // Legacy single-profile value. Keep it for migration and compatibility with existing gpu_oc.json files.
        public GPUOverclockInfo Info { get; set; } = GPUOverclockInfo.Zero;
        public Guid ActiveProfileId { get; set; }
        public Dictionary<Guid, Profile> Profiles { get; set; } = [];
    }

    public override GPUOverclockSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<GPUOverclockSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    private static GPUOverclockSettingsStore? Normalize(GPUOverclockSettingsStore? store)
    {
        if (store is null)
            return null;

        store.Profiles = store.Profiles?
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => NormalizeProfile(kv.Value))
            ?? [];
        return store;
    }

    private static GPUOverclockSettingsStore.Profile NormalizeProfile(GPUOverclockSettingsStore.Profile profile)
    {
        profile.Name ??= DefaultProfileName;
        return profile;
    }
}
