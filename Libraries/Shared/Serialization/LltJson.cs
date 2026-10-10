using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Shared.Serialization;

/// <summary>
/// Cross-platform JSON serialization options.
/// Extracted from Lib.Serialization.LltJson — pure System.Text.Json, no platform dependency.
/// </summary>
public static class LltJson
{
    /// <summary>
    /// Matches legacy Newtonsoft settings for app settings files: indented, enums as strings, explicit nulls, bounded depth.
    /// </summary>
    public static JsonSerializerOptions CreateSettingsOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            MaxDepth = 32,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    /// <summary>
    /// Compact JSON for IPC / pipes (no indentation).
    /// </summary>
    public static JsonSerializerOptions CreateCompactOptions()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}
