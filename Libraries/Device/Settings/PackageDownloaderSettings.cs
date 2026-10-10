using System.Collections.Generic;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Settings;

public class PackageDownloaderSettings()
    : AbstractSettings<PackageDownloaderSettings.PackageDownloaderSettingsStore>("package_downloader.json")
{
    public class PackageDownloaderSettingsStore
    {
        public string? DownloadPath { get; set; }
        public bool OnlyShowUpdates { get; set; }
        public HashSet<string> HiddenPackages { get; set; } = [];
    }

    public override PackageDownloaderSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<PackageDownloaderSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    private static PackageDownloaderSettingsStore? Normalize(PackageDownloaderSettingsStore? store)
    {
        if (store is null)
            return null;

        store.HiddenPackages ??= [];
        return store;
    }
}
