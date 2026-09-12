using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Macro.Utils.TypeConverters;

/// <summary>
/// JSON converter for <see cref="MacroIdentifier"/>.
/// Serializes as "Source,Key" string. Overrides both Read/Write and the
/// AsPropertyName variants needed for Dictionary&lt;MacroIdentifier, ...&gt; support.
/// </summary>
public class MacroIdentifierJsonConverter : JsonConverter<MacroIdentifier>
{
    private static MacroIdentifier Parse(string str)
    {
        var parts = str.Split(',');
        if (parts.Length != 2)
            throw new JsonException($"Invalid MacroIdentifier format: '{str}'. Expected 'Source,Key'.");

        if (!Enum.TryParse<MacroSource>(parts[0], out var source))
            throw new JsonException($"Invalid MacroSource: '{parts[0]}'.");

        if (!ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var key))
            throw new JsonException($"Invalid key value: '{parts[1]}'.");

        return new MacroIdentifier(source, key);
    }

    private static string Format(MacroIdentifier value) =>
        $"{value.Source},{value.Key}";

    public override MacroIdentifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string token for MacroIdentifier, got {reader.TokenType}.");
        return Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, MacroIdentifier value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Format(value));
    }

    public override MacroIdentifier ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName token for MacroIdentifier key, got {reader.TokenType}.");
        return Parse(reader.GetString()!);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, MacroIdentifier value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(Format(value));
    }
}
