using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Serialization;

/// <summary>
/// Ensures <see cref="GPUOverclockInfo"/> round-trips for settings JSON; readonly positional structs can deserialize incorrectly with default STJ options.
/// </summary>
public sealed class GPUOverclockInfoJsonConverter : JsonConverter<GPUOverclockInfo>
{
    public override GPUOverclockInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        int core = 0;
        int memory = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.GetString();
            reader.Read();
            var val = reader.GetInt32();

            if (string.Equals(name, nameof(GPUOverclockInfo.CoreDeltaMhz), StringComparison.OrdinalIgnoreCase))
                core = val;
            else if (string.Equals(name, nameof(GPUOverclockInfo.MemoryDeltaMhz), StringComparison.OrdinalIgnoreCase))
                memory = val;
        }

        return new GPUOverclockInfo(core, memory);
    }

    public override void Write(Utf8JsonWriter writer, GPUOverclockInfo value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(GPUOverclockInfo.CoreDeltaMhz), value.CoreDeltaMhz);
        writer.WriteNumber(nameof(GPUOverclockInfo.MemoryDeltaMhz), value.MemoryDeltaMhz);
        writer.WriteEndObject();
    }
}
