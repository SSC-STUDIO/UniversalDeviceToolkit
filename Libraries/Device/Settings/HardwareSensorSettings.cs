using System;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Settings;

/// <summary>
/// Shared dashboard sensor presentation preferences.
/// </summary>
public class HardwareSensorSettings()
    : AbstractSettings<HardwareSensorSettings.HardwareSensorSettingsStore>("hardware_sensors.json")
{
    public event EventHandler? SectionsChanged;

    public class HardwareSensorSettingsStore
    {
        public bool SelectedGpuIsIgpu { get; set; }
        public bool ShowCpuAverageFrequency { get; set; }
        public bool DisplayMemoryInGigabytes { get; set; }
        public string[] VisibleSections { get; set; } = ["CPU", "Battery", "GPU"];
        public string[] SectionOrder { get; set; } = ["CPU", "Battery", "GPU"];
    }

    protected override HardwareSensorSettingsStore Default => new();

    public override HardwareSensorSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<HardwareSensorSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    public void NotifySectionsChanged() => SectionsChanged?.Invoke(this, EventArgs.Empty);

    private static HardwareSensorSettingsStore? Normalize(HardwareSensorSettingsStore? store)
    {
        if (store is null)
            return null;

        store.VisibleSections ??= ["CPU", "Battery", "GPU"];
        store.SectionOrder ??= ["CPU", "Battery", "GPU"];
        return store;
    }
}
