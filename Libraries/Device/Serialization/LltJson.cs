namespace UniversalDeviceToolkit.Lib.Serialization;

using global::System.Text.Json;

/// <summary>Compatibility facade over the cross-platform JSON options.</summary>
public static class LltJson
{
    public static JsonSerializerOptions CreateSettingsOptions() =>
        UniversalDeviceToolkit.Shared.Serialization.LltJson.CreateSettingsOptions();

    public static JsonSerializerOptions CreateCompactOptions() =>
        UniversalDeviceToolkit.Shared.Serialization.LltJson.CreateCompactOptions();
}
