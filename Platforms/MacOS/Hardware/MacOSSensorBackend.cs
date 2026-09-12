using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using UniversalDeviceToolkit.Abstractions.Hardware;

namespace UniversalDeviceToolkit.Platform.MacOS.Hardware;

/// <summary>
/// macOS implementation of <see cref="ISensorBackend"/>.
/// Uses sysctl, vm_stat, and IOKit to gather sensor data.
/// </summary>
public sealed class MacOSSensorBackend : ISensorBackend
{
    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsMacOS();

    /// <inheritdoc />
    public IReadOnlyList<SensorReading> GetReadings()
    {
        var readings = new List<SensorReading>();
        ReadCpuUsageAsync(readings).GetAwaiter().GetResult();
        ReadMemoryUsageAsync(readings).GetAwaiter().GetResult();
        return readings;
    }

    private static async Task ReadCpuUsageAsync(List<SensorReading> readings)
    {
        try
        {
            var psi = new ProcessStartInfo("sysctl", "kern.cp_time")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return;

            // Fix: Read asynchronously to avoid ReadToEnd + WaitForExit deadlock
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            await errorTask;

            // Parse output: "kern.cp_time: user nice system idle"
            var match = Regex.Match(output, @"kern\.cp_time:\s+(.+)", RegexOptions.IgnoreCase);
            if (!match.Success) return;

            var parts = match.Groups[1].Value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;

            var values = parts.Select(p => long.TryParse(p, out var v) ? v : 0L).ToArray();
            var total = values.Sum();
            var idle = values.Length > 3 ? values[3] : 0L;

            if (total <= 0) return;

            var usage = (1.0 - (double)idle / total) * 100.0;
            readings.Add(new SensorReading("CPU Total", "Usage", Math.Round(usage, 1), "%"));
        }
        catch
        {
            // Silently ignore failures
        }
    }

    private static async Task ReadMemoryUsageAsync(List<SensorReading> readings)
    {
        try
        {
            var psi = new ProcessStartInfo("vm_stat")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return;

            // Fix: Read asynchronously to avoid ReadToEnd + WaitForExit deadlock
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            await errorTask;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            long? freePages = null;
            long? activePages = null;
            long? inactivePages = null;
            long? speculativePages = null;
            long? wiredPages = null;
            long? purgablePages = null;
            long? fileCachePages = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("Pages free:", StringComparison.OrdinalIgnoreCase))
                    freePages = ParseVmStatValue(line);
                else if (line.StartsWith("Pages active:", StringComparison.OrdinalIgnoreCase))
                    activePages = ParseVmStatValue(line);
                else if (line.StartsWith("Pages inactive:", StringComparison.OrdinalIgnoreCase))
                    inactivePages = ParseVmStatValue(line);
                else if (line.StartsWith("Pages speculative:", StringComparison.OrdinalIgnoreCase))
                    speculativePages = ParseVmStatValue(line);
                else if (line.StartsWith("Pages wired:", StringComparison.OrdinalIgnoreCase))
                    wiredPages = ParseVmStatValue(line);
                else if (line.StartsWith("Pages purgeable:", StringComparison.OrdinalIgnoreCase))
                    purgablePages = ParseVmStatValue(line);
                else if (line.StartsWith("File cache:", StringComparison.OrdinalIgnoreCase))
                    fileCachePages = ParseVmStatValue(line);
            }

            // Estimate used memory percentage
            if (freePages is not null && activePages is not null && wiredPages is not null)
            {
                var total = (long)(freePages + activePages + wiredPages +
                                   (inactivePages ?? 0) + (speculativePages ?? 0) +
                                   (fileCachePages ?? 0));
                if (total > 0)
                {
                    var usedPages = (long)(activePages + wiredPages);
                    var usedPct = ((double)usedPages / total) * 100.0;
                    readings.Add(new SensorReading("Memory", "Usage", Math.Round(usedPct, 1), "%"));
                }
            }
        }
        catch
        {
            // Silently ignore failures
        }
    }

    private static long? ParseVmStatValue(string line)
    {
        var match = Regex.Match(line, @"[:\s]+(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return long.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }
}
