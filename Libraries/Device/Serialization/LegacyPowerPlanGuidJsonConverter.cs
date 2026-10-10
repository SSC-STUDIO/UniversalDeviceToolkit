using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Serialization;

/// <summary>
/// Migrates legacy Windows power-plan instance id strings into <see cref="Guid"/> (introduced in 2.12.0).
/// </summary>
internal sealed class LegacyPowerPlanGuidJsonConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Guid.Empty;

        var originalValue = reader.GetString() ?? string.Empty;
        var value = originalValue;

        const string prefix = "Microsoft:PowerPlan\\{";
        const string suffix = "}";

        var prefixIndex = value.IndexOf(prefix, StringComparison.InvariantCulture);

        var suffixIndex = -1;
        if (prefixIndex >= 0)
        {
            var suffixStartPos = prefixIndex + prefix.Length;
            if (suffixStartPos >= 0 && suffixStartPos <= value.Length && suffixStartPos >= prefixIndex)
                suffixIndex = value.IndexOf(suffix, suffixStartPos, StringComparison.InvariantCulture);
        }

        if (prefixIndex >= 0 && suffixIndex >= 0 && suffixIndex >= prefixIndex + prefix.Length)
        {
            var start = prefixIndex + prefix.Length;
            var length = suffixIndex - start;

            if (start >= 0 && start < value.Length && length >= 0 && start + length <= value.Length)
            {
                if (length > 0)
                    value = value.Substring(start, length);
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"LegacyPowerPlanGuidJsonConverter: Invalid format — prefix/suffix with no GUID content. Original: '{originalValue}'");
                    return Guid.Empty;
                }
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"LegacyPowerPlanGuidJsonConverter: Invalid bounds (start={start}, length={length}, len={value.Length}). Original: '{originalValue}'");
                return Guid.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            if (Log.Instance.IsTraceEnabled && !string.IsNullOrWhiteSpace(originalValue))
                Log.Instance.Trace($"LegacyPowerPlanGuidJsonConverter: Empty GUID after extraction. Original: '{originalValue}'");
            return Guid.Empty;
        }

        if (Guid.TryParse(value, out var guid))
            return guid;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"LegacyPowerPlanGuidJsonConverter: Failed to parse GUID from '{value}' (original: '{originalValue}'). Returning Guid.Empty.");

        return Guid.Empty;
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
