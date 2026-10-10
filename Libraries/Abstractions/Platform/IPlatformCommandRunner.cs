using System.Diagnostics;

namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Testable boundary for platform command-line probes such as sysctl and pmset.
/// </summary>
public interface IPlatformCommandRunner
{
    PlatformCommandResult Run(string fileName, params string[] arguments);
}

public sealed record PlatformCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

public sealed class ProcessPlatformCommandRunner : IPlatformCommandRunner
{
    public PlatformCommandResult Run(string fileName, params string[] arguments)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return new PlatformCommandResult(-1, string.Empty, "File name is required.");

        arguments ??= [];

        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }.WithArguments(arguments));

            if (process is null)
                return new PlatformCommandResult(-1, string.Empty, "Process could not be started.");

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                return new PlatformCommandResult(-1, string.Empty, "Process timed out.");
            }

            if (!Task.WhenAll(output, error).Wait(5000))
                return new PlatformCommandResult(process.ExitCode, string.Empty, "Timed out reading process output.");

            return new PlatformCommandResult(process.ExitCode, output.Result, error.Result);
        }
        catch (Exception ex)
        {
            return new PlatformCommandResult(-1, string.Empty, ex.Message);
        }
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArguments(this ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }
}
