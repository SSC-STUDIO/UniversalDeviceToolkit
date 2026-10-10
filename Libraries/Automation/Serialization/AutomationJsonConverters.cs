using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Steps;

namespace UniversalDeviceToolkit.Lib.Automation.Serialization;

internal static class AutomationJsonDiscriminators
{
    private const string TriggerSuffix = "AutomationPipelineTrigger";
    private const string StepSuffix = "AutomationStep";

    internal static string ForTrigger(Type t) => ForType(t, TriggerSuffix);
    internal static string ForStep(Type t) => ForType(t, StepSuffix);

    private static string ForType(Type t, string suffix)
    {
        var n = t.Name;
        if (n.EndsWith(suffix, StringComparison.Ordinal))
            n = n[..^suffix.Length];
        return string.IsNullOrEmpty(n) ? t.Name : JsonNamingPolicy.CamelCase.ConvertName(n);
    }
}

/// <summary>
/// Public bridge for hosts that need to edit automation data without taking a
/// dependency on the internal converter implementation. The discriminator based
/// payload is intentionally stable and preserves unknown trigger/step properties.
/// </summary>
public static class AutomationSerialization
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };
        options.Converters.Add(new AutomationPipelineTriggerJsonConverter());
        options.Converters.Add(new AutomationStepJsonConverter());
        return options;
    }

    public static string SerializeTrigger(IAutomationPipelineTrigger trigger) =>
        JsonSerializer.Serialize<IAutomationPipelineTrigger?>(trigger, CreateOptions());

    public static IAutomationPipelineTrigger? DeserializeTrigger(string json) =>
        JsonSerializer.Deserialize<IAutomationPipelineTrigger?>(json, CreateOptions());

    public static string SerializeStep(IAutomationStep step) =>
        JsonSerializer.Serialize<IAutomationStep?>(step, CreateOptions());

    public static IAutomationStep? DeserializeStep(string json) =>
        JsonSerializer.Deserialize<IAutomationStep?>(json, CreateOptions());
}

internal sealed class AutomationPipelineTriggerJsonConverter : JsonConverter<IAutomationPipelineTrigger?>
{
    private static readonly Dictionary<string, Type> TypesByDiscriminator = [];
    private static readonly Dictionary<Type, string> DiscriminatorsByType = [];

    static AutomationPipelineTriggerJsonConverter()
    {
        foreach (var t in typeof(IAutomationPipelineTrigger).Assembly.SafeGetTypes())
        {
            if (!t.IsClass || t.IsAbstract || !typeof(IAutomationPipelineTrigger).IsAssignableFrom(t))
                continue;

            var disc = AutomationJsonDiscriminators.ForTrigger(t);
            TypesByDiscriminator[disc] = t;
            DiscriminatorsByType[t] = disc;
        }
    }

    public override IAutomationPipelineTrigger? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("$type", out var discEl))
        {
            var key = discEl.GetString();
            if (!string.IsNullOrEmpty(key))
            {
                if (TypesByDiscriminator.TryGetValue(key, out var mapped))
                    return (IAutomationPipelineTrigger?)JsonSerializer.Deserialize(root.GetRawText(), mapped, options);

                var byName = typeof(IAutomationPipelineTrigger).Assembly.SafeGetTypes()
                    .FirstOrDefault(t => t.IsClass && !t.IsAbstract && typeof(IAutomationPipelineTrigger).IsAssignableFrom(t)
                        && (t.Name == key || t.FullName == key || t.FullName?.EndsWith("." + key, StringComparison.Ordinal) == true));
                if (byName is not null)
                    return (IAutomationPipelineTrigger?)JsonSerializer.Deserialize(root.GetRawText(), byName, options);
            }
        }

        return InferLegacy(root, options);
    }

    public override void Write(Utf8JsonWriter writer, IAutomationPipelineTrigger? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var runtimeType = value.GetType();
        if (!DiscriminatorsByType.TryGetValue(runtimeType, out var disc))
            disc = AutomationJsonDiscriminators.ForTrigger(runtimeType);

        using var stream = new MemoryStream();
        using (var uw = new Utf8JsonWriter(stream))
            JsonSerializer.Serialize(uw, value, runtimeType, options);

        using var produced = JsonDocument.Parse(stream.ToArray());
        writer.WriteStartObject();
        writer.WriteString("$type", disc);
        foreach (var p in produced.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(p.Name);
            p.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static IAutomationPipelineTrigger? InferLegacy(JsonElement root, JsonSerializerOptions options)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in root.EnumerateObject())
            names.Add(p.Name);

        if (names.Count == 0)
            return null;

        var raw = root.GetRawText();

        try
        {
            if (names.Contains("Triggers"))
            {
                // And uses newline-joined DisplayName; Or uses a localized "OR" separator.
                // Prefer DisplayName hint when present; otherwise default to And (historical).
                if (root.TryGetProperty("DisplayName", out var compositeName) &&
                    compositeName.ValueKind == JsonValueKind.String)
                {
                    var dn = compositeName.GetString() ?? string.Empty;
                    if (dn.Contains(" OR ", StringComparison.OrdinalIgnoreCase) ||
                        dn.Contains(" || ", StringComparison.Ordinal) ||
                        dn.Contains("或", StringComparison.Ordinal))
                        return JsonSerializer.Deserialize<OrAutomationPipelineTrigger>(raw, options);
                }

                return JsonSerializer.Deserialize<AndAutomationPipelineTrigger>(raw, options);
            }

            if (names.Contains("Ssids"))
                return JsonSerializer.Deserialize<WiFiConnectedAutomationPipelineTrigger>(raw, options);

            if (names.Contains("PowerModeState"))
                return JsonSerializer.Deserialize<PowerModeAutomationPipelineTrigger>(raw, options);

            if (names.Contains("PresetId"))
                return JsonSerializer.Deserialize<GodModePresetChangedAutomationPipelineTrigger>(raw, options);

            if (names.Contains("Processes"))
            {
                // Same shape for start/stop; use DisplayName / ProcessesStarted when available.
                if (root.TryGetProperty("DisplayName", out var procName) &&
                    procName.ValueKind == JsonValueKind.String)
                {
                    var dn = procName.GetString() ?? string.Empty;
                    if (dn.Contains("stop", StringComparison.OrdinalIgnoreCase) ||
                        dn.Contains("关闭", StringComparison.Ordinal) ||
                        dn.Contains("停止", StringComparison.Ordinal))
                        return JsonSerializer.Deserialize<ProcessesStopRunningAutomationPipelineTrigger>(raw, options);
                }

                if (root.TryGetProperty("ProcessesStarted", out var started) &&
                    started.ValueKind is JsonValueKind.False)
                    return JsonSerializer.Deserialize<ProcessesStopRunningAutomationPipelineTrigger>(raw, options);

                return JsonSerializer.Deserialize<ProcessesAreRunningAutomationPipelineTrigger>(raw, options);
            }

            if (names.Contains("InstanceIds"))
            {
                if (root.TryGetProperty("DisplayName", out var devName) &&
                    devName.ValueKind == JsonValueKind.String)
                {
                    var dn = devName.GetString() ?? string.Empty;
                    if (dn.Contains("disconnect", StringComparison.OrdinalIgnoreCase) ||
                        dn.Contains("断开", StringComparison.Ordinal))
                        return JsonSerializer.Deserialize<DeviceDisconnectedAutomationPipelineTrigger>(raw, options);
                }

                if (root.TryGetProperty("DeviceConnected", out var conn) &&
                    conn.ValueKind is JsonValueKind.False)
                    return JsonSerializer.Deserialize<DeviceDisconnectedAutomationPipelineTrigger>(raw, options);

                return JsonSerializer.Deserialize<DeviceConnectedAutomationPipelineTrigger>(raw, options);
            }

            if (names.Contains("InactivityTimeSpan"))
                return JsonSerializer.Deserialize<UserInactivityAutomationPipelineTrigger>(raw, options);

            if (names.Contains("Period"))
                return JsonSerializer.Deserialize<PeriodicAutomationPipelineTrigger>(raw, options);

            if (names.Contains("Metric") && names.Contains("Threshold"))
                return JsonSerializer.Deserialize<HardwareSensorAutomationPipelineTrigger>(raw, options);

            if (names.Contains("ChargeFilter") || (names.Contains("Threshold") && names.Contains("Comparison") && !names.Contains("Metric")))
                return JsonSerializer.Deserialize<BatteryPercentageAutomationPipelineTrigger>(raw, options);

            if (names.Contains("IsSunrise") || names.Contains("IsSunset") || names.Contains("Days"))
                return JsonSerializer.Deserialize<TimeAutomationPipelineTrigger>(raw, options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (raw.Trim() == "{}")
            return null;

        var candidates = typeof(IAutomationPipelineTrigger).Assembly.SafeGetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IAutomationPipelineTrigger).IsAssignableFrom(t))
            .OrderByDescending(GetBestCtorArity)
            .ThenBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in candidates)
        {
            try
            {
                var o = JsonSerializer.Deserialize(raw, type, options);
                if (o is IAutomationPipelineTrigger trig)
                    return trig;
            }
            catch (JsonException)
            {
                // try next
            }
        }

        return null;
    }

    private static int GetBestCtorArity(Type t)
    {
        var ctors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        return ctors.Length == 0 ? 0 : ctors.Max(c => c.GetParameters().Length);
    }
}

internal sealed class AutomationStepJsonConverter : JsonConverter<IAutomationStep?>
{
    private static readonly Dictionary<string, Type> TypesByDiscriminator = [];
    private static readonly Dictionary<Type, string> DiscriminatorsByType = [];

    static AutomationStepJsonConverter()
    {
        foreach (var t in typeof(IAutomationStep).Assembly.SafeGetTypes())
        {
            if (!t.IsClass || t.IsAbstract || !typeof(IAutomationStep).IsAssignableFrom(t))
                continue;

            var disc = AutomationJsonDiscriminators.ForStep(t);
            TypesByDiscriminator[disc] = t;
            DiscriminatorsByType[t] = disc;
        }
    }

    public override IAutomationStep? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("$type", out var discEl))
        {
            var key = discEl.GetString();
            if (!string.IsNullOrEmpty(key))
            {
                if (TypesByDiscriminator.TryGetValue(key, out var mapped))
                    return (IAutomationStep?)JsonSerializer.Deserialize(root.GetRawText(), mapped, options);

                var byName = typeof(IAutomationStep).Assembly.SafeGetTypes()
                    .FirstOrDefault(t => t.IsClass && !t.IsAbstract && typeof(IAutomationStep).IsAssignableFrom(t)
                        && (t.Name == key || t.FullName == key || t.FullName?.EndsWith("." + key, StringComparison.Ordinal) == true));
                if (byName is not null)
                    return (IAutomationStep?)JsonSerializer.Deserialize(root.GetRawText(), byName, options);
            }
        }

        return InferLegacyStep(root, options);
    }

    public override void Write(Utf8JsonWriter writer, IAutomationStep? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var runtimeType = value.GetType();
        if (!DiscriminatorsByType.TryGetValue(runtimeType, out var disc))
            disc = AutomationJsonDiscriminators.ForStep(runtimeType);

        using var stream = new MemoryStream();
        using (var uw = new Utf8JsonWriter(stream))
            JsonSerializer.Serialize(uw, value, runtimeType, options);

        using var produced = JsonDocument.Parse(stream.ToArray());
        writer.WriteStartObject();
        writer.WriteString("$type", disc);
        foreach (var p in produced.RootElement.EnumerateObject())
        {
            writer.WritePropertyName(p.Name);
            p.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static IAutomationStep? InferLegacyStep(JsonElement root, JsonSerializerOptions options)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var raw = root.GetRawText();

        var ordered = typeof(IAutomationStep).Assembly.SafeGetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IAutomationStep).IsAssignableFrom(t))
            .OrderByDescending(GetBestCtorArity)
            .ThenBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in ordered)
        {
            try
            {
                var o = JsonSerializer.Deserialize(raw, type, options);
                if (o is IAutomationStep step)
                    return step;
            }
            catch (JsonException)
            {
                // try next
            }
        }

        return null;
    }

    private static int GetBestCtorArity(Type t)
    {
        var ctors = t.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        return ctors.Length == 0 ? 0 : ctors.Max(c => c.GetParameters().Length);
    }
}
