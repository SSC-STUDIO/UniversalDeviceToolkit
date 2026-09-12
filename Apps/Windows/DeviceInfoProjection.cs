using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal static class DeviceInfoProjection
{
    internal static JsonElement From(JsonElement source)
    {
        var hardware = Property(source, "hardware");
        return JsonSerializer.SerializeToElement(new
        {
            vendor = Text(source, "vendor"),
            model = Text(source, "model") is { Length: > 0 } model ? model : "Universal Device Toolkit",
            machineType = Text(source, "machineType"),
            serialNumber = Text(source, "serialNumber"),
            biosVersion = Text(source, "biosVersion"),
            processor = Fields(Property(hardware, "processor"), ["name"], ["numberOfCores", "numberOfLogicalProcessors", "maxClockSpeedMHz"], requireName: true),
            videoController = Fields(Property(hardware, "videoController"), ["name", "adapterCompatibility"], ["adapterRamBytes"], requireName: true),
            memory = Fields(Property(hardware, "memory"), [], ["totalCapacityBytes", "moduleCount", "configuredClockSpeedMHz", "speedMHz"]),
            warranty = Fields(Property(source, "warranty"), ["startDate", "endDate", "link"], [])
        });
    }

    private static Dictionary<string, JsonElement>? Fields(JsonElement source, string[] strings, string[] numbers, bool requireName = false)
    {
        if (source.ValueKind != JsonValueKind.Object || (requireName && Text(source, "name").Length == 0)) return null;
        var result = new Dictionary<string, JsonElement>();
        foreach (var name in strings)
            if (Property(source, name) is { ValueKind: JsonValueKind.String } value) result[name] = value;
        foreach (var name in numbers)
            if (Property(source, name) is { ValueKind: JsonValueKind.Number } value) result[name] = value;
        return result.Count > 0 ? result : null;
    }

    private static JsonElement Property(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value) ? value : default;

    private static string Text(JsonElement source, string name) =>
        Property(source, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() ?? string.Empty : string.Empty;
}
