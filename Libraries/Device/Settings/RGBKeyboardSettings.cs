using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Settings;

public class RGBKeyboardSettings() : AbstractSettings<RGBKeyboardSettings.RGBKeyboardSettingsStore>("rgb_keyboard.json")
{
    public class RGBKeyboardSettingsStore
    {
        public RGBKeyboardBacklightState State { get; set; }
    }

    protected override RGBKeyboardSettingsStore Default => new()
    {
        State = CreateDefaultState(),
    };

    public override RGBKeyboardSettingsStore? LoadStore() => Normalize(base.LoadStore());

    public override async Task<RGBKeyboardSettingsStore?> LoadStoreAsync() =>
        Normalize(await base.LoadStoreAsync().ConfigureAwait(false));

    private static RGBKeyboardSettingsStore? Normalize(RGBKeyboardSettingsStore? store)
    {
        if (store is null)
            return null;

        var selectedPreset = global::System.Enum.IsDefined(store.State.SelectedPreset)
            ? store.State.SelectedPreset
            : RGBKeyboardBacklightPreset.Off;

        var presets = store.State.Presets?
            .Where(kv => global::System.Enum.IsDefined(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (presets is null || presets.Count == 0)
            presets = CreateDefaultPresets();

        store.State = new(selectedPreset, presets);
        return store;
    }

    private static RGBKeyboardBacklightState CreateDefaultState() => new(RGBKeyboardBacklightPreset.Off, CreateDefaultPresets());

    private static Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription> CreateDefaultPresets() => new()
    {
        { RGBKeyboardBacklightPreset.One, new(RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low, RGBColor.Green, RGBColor.Teal, RGBColor.Purple, RGBColor.Pink) },
        { RGBKeyboardBacklightPreset.Two, new(RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low, RGBColor.Red, RGBColor.Red, RGBColor.Red, RGBColor.Red) },
        { RGBKeyboardBacklightPreset.Three, new(RGBKeyboardBacklightEffect.Breath, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low,  RGBColor.White,RGBColor.White,RGBColor.White,RGBColor.White) },
        { RGBKeyboardBacklightPreset.Four, new(RGBKeyboardBacklightEffect.Smooth, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low, RGBColor.White,RGBColor.White,RGBColor.White,RGBColor.White) },
    };
}
