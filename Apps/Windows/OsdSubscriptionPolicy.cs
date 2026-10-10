using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Honors explicit Host denials while preserving older, partial capability replies.</summary>
internal readonly record struct OsdSubscriptionPolicy(bool Sensors, bool Fps)
{
    internal static OsdSubscriptionPolicy Compatible => new(true, true);

    internal static OsdSubscriptionPolicy FromCapabilities(JsonElement value) => new(
        !IsDisabled(value, "capabilities", "sensors") && !IsDisabled(value, "backends", "sensorBackend"),
        !IsDisabled(value, "capabilities", "fps"));

    internal OsdSubscriptionPolicy ForView(bool visible, JsonElement settings)
    {
        var hasFpsItems = settings.ValueKind == JsonValueKind.Object && settings.TryGetProperty("Items", out var items)
            && items.ValueKind == JsonValueKind.Array && items.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String && item.GetString() is "Fps" or "LowFps" or "FrameTime");
        return new(visible && Sensors, visible && Fps && hasFpsItems);
    }

    private static bool IsDisabled(JsonElement value, string group, string name) => value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(group, out var properties) && properties.ValueKind == JsonValueKind.Object
        && properties.TryGetProperty(name, out var flag) && flag.ValueKind == JsonValueKind.False;
}
