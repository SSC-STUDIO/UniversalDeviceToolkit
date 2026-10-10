using System.Diagnostics;

namespace UniversalDeviceToolkit.Tests;

public static class DirectoryLinkTestHelper
{
    public static void Create(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        // cmd uses its own quote rules; ArgumentList would backslash-escape the
        // embedded quotes. Junctions require neither elevation nor developer mode.
        var start = new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start) ?? throw new IOException("Could not start junction creation.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Test junction creation timed out.");
        }
        var output = Task.WhenAll(standardOutput, standardError).GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new IOException($"Could not create test junction ({process.ExitCode}): {string.Join(Environment.NewLine, output)}");
    }
}
