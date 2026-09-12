using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.CLI.Lib;

namespace UniversalDeviceToolkit.CLI;

public sealed class CliDoctorReport
{
    public required string AppData { get; init; }
    public bool CliSetting { get; init; }
    public bool Pipe { get; init; }
    public bool PreferredPipe { get; init; }
    public bool LegacyPipe { get; init; }
    public required string Hint { get; init; }
    public bool Ready => CliSetting && Pipe;
}

public static class CliDoctor
{
    public const string IntegrationsFileName = "integrations.json";

    public static CliDoctorReport Inspect(
        string? appDataRoot = null,
        Func<string, bool>? pipeExists = null)
    {
        var root = appDataRoot ?? ApplicationDataPaths.GetRoot();
        var cliSetting = ReadCliSetting(root);
        var probe = pipeExists ?? PipeExists;
        var names = Constants.GetClientPipeNamesFromEnvironment();
        var preferred = probe(names[0]);
        var legacy = names.Length > 1 && probe(names[1]);
        var pipe = preferred || legacy;
        return new CliDoctorReport
        {
            AppData = root,
            CliSetting = cliSetting,
            Pipe = pipe,
            PreferredPipe = preferred,
            LegacyPipe = legacy,
            Hint = BuildHint(cliSetting, pipe),
        };
    }

    public static object ToJsonPayload(CliDoctorReport report) => new
    {
        ok = true,
        command = "doctor",
        appData = report.AppData,
        cliSetting = report.CliSetting,
        pipe = report.Pipe,
        preferredPipe = report.PreferredPipe,
        legacyPipe = report.LegacyPipe,
        ready = report.Ready,
        hint = report.Hint,
    };

    public static void WriteHuman(CliDoctorReport report)
    {
        Console.WriteLine($"appData: {report.AppData}");
        Console.WriteLine($"cliSetting: {FormatFlag(report.CliSetting)}");
        Console.WriteLine($"pipe: {FormatFlag(report.Pipe)}");
        Console.WriteLine($"preferredPipe: {FormatFlag(report.PreferredPipe)}");
        Console.WriteLine($"legacyPipe: {FormatFlag(report.LegacyPipe)}");
        Console.WriteLine($"ready: {FormatFlag(report.Ready)}");
        Console.WriteLine($"hint: {report.Hint}");
    }

    public static bool ReadCliSetting(string appDataRoot)
    {
        var path = Path.Combine(appDataRoot, IntegrationsFileName);
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("CLI", StringComparison.OrdinalIgnoreCase))
                    continue;
                return property.Value.ValueKind == JsonValueKind.True;
            }
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        return false;
    }

    public static bool PipeExists(string pipeName)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect(200);
            return pipe.IsConnected;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string BuildHint(bool cliSetting, bool pipe)
    {
        if (!cliSetting)
            return "Enable CLI in Settings (Integrations) and keep UDT in the tray. Do not edit integrations.json.";
        if (!pipe)
            return "CLI is enabled but the named pipe is down. Wait a moment and run doctor again; if it stays down, restart UDT.";
        return "CLI is ready.";
    }

    private static string FormatFlag(bool value) => value ? "true" : "false";
}
