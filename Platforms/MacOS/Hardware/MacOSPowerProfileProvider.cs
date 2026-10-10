using System.Diagnostics;
using System.Text.RegularExpressions;
using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.MacOS.Hardware;

/// <summary>
/// macOS implementation of <see cref="IPowerProfileProvider"/>.
/// Uses pmset command to manage power profiles.
/// </summary>
public sealed class MacOSPowerProfileProvider : IPowerProfileProvider
{
    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsMacOS();

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableProfiles()
    {
        var profiles = new List<string>();

        try
        {
            var psi = new ProcessStartInfo("pmset", "-g custom")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return profiles;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Parse pmset output to detect current power mode
            var hasBattery = Regex.IsMatch(output, @"Battery Power:", RegexOptions.IgnoreCase);
            var hasAC = Regex.IsMatch(output, @"AC Power:", RegexOptions.IgnoreCase);

            if (hasAC || hasBattery)
            {
                profiles.Add("balanced");
                profiles.Add("high-performance");
                profiles.Add("power-saver");
            }
        }
        catch
        {
            // Silently ignore failures
        }

        return profiles;
    }

    /// <inheritdoc />
    public string? GetActiveProfile()
    {
        try
        {
            var psi = new ProcessStartInfo("pmset", "-g")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Detect if on battery or AC power
            var sleep = Regex.Match(output, @"sleep\s+(\d+)", RegexOptions.IgnoreCase);
            if (sleep.Success && int.TryParse(sleep.Groups[1].Value, out var sleepMinutes))
            {
                if (sleepMinutes <= 5)
                    return "high-performance";
                if (sleepMinutes >= 30)
                    return "power-saver";
            }

            return "balanced";
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetActiveProfileAsync(string profileName)
    {
        try
        {
            var psi = new ProcessStartInfo("pmset", GetProfileArgs(profileName))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is not null)
                await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // Silently ignore failures
        }
    }

    private static string GetProfileArgs(string profileName)
    {
        return profileName switch
        {
            "high-performance" => "-a sleep 0 disksleep 0",
            "power-saver" => "-a sleep 60 disksleep 10",
            _ => "-a sleep 10 disksleep 5" // balanced
        };
    }
}
