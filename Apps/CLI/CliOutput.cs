using System;
using System.Collections.Generic;
using System.Text.Json;

namespace UniversalDeviceToolkit.CLI;

internal static class CliOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public const int InvalidArgumentExitCode = 1;

    public static bool Json { get; set; }

    public static void Success(string command, string? value = null, string? name = null)
    {
        if (!Json)
        {
            if (!string.IsNullOrEmpty(value))
                Console.WriteLine(value);
            return;
        }

        Write(new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["command"] = command,
            ["name"] = name,
            ["value"] = value,
        });
    }

    public static void SuccessList(string command, string newlineSeparated, string? name = null)
    {
        if (!Json)
        {
            if (!string.IsNullOrEmpty(newlineSeparated))
                Console.WriteLine(newlineSeparated);
            return;
        }

        Write(new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["command"] = command,
            ["name"] = name,
            ["value"] = SplitLines(newlineSeparated),
        });
    }

    public static void Error(string code, string message, string? command = null)
    {
        if (!Json)
        {
            Console.Error.WriteLine(message);
            return;
        }

        Write(new Dictionary<string, object?>
        {
            ["ok"] = false,
            ["code"] = code,
            ["command"] = command,
            ["message"] = message,
        });
    }

    public static int Fail(string code, string message, string? command = null)
    {
        Error(code, message, command);
        return InvalidArgumentExitCode;
    }

    internal static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    public static void Write(object payload)
    {
        Console.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
