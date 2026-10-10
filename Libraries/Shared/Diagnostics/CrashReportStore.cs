using System.Reflection;
using System.Text;
using System.Text.Json;
using UniversalDeviceToolkit.Shared.Logging;
using UniversalDeviceToolkit.Shared.Utils;

namespace UniversalDeviceToolkit.Shared.Diagnostics;

/// <summary>
/// Cross-host persistence for fatal-error reports. The schema intentionally
/// matches the legacy WPF report files so either desktop host can recover them.
/// </summary>
public static class CrashReportStore
{
    private static readonly DateTime StartedAtUtc = DateTime.UtcNow;

    private static string CrashReportFolder => Path.Combine(Folders.AppData, "crash_reports");

    public static string CrashReportDirectory => CrashReportFolder;

    public static string Save(Exception? exception, string source)
    {
        try
        {
            Directory.CreateDirectory(CrashReportFolder);

            var report = new CrashReport
            {
                Timestamp = DateTime.UtcNow,
                AppVersion = GetAppVersion(),
                OsVersion = Environment.OSVersion.VersionString,
                RuntimeVersion = $".NET {Environment.Version}",
                ExceptionType = exception?.GetType().FullName ?? "Unknown",
                ExceptionMessage = exception?.Message ?? "No exception message",
                StackTrace = exception?.StackTrace ?? "No stack trace available",
                Source = source,
                Uptime = DateTime.UtcNow - StartedAtUtc,
            };

            if (exception?.InnerException is { } innerException)
            {
                report.InnerExceptionType = innerException.GetType().FullName;
                report.InnerExceptionMessage = innerException.Message;
                report.InnerExceptionStackTrace = innerException.StackTrace;
            }

            var filePath = Path.Combine(
                CrashReportFolder,
                $"crash_{DateTime.UtcNow:yyyy_MM_dd_HH_mm_ss_fff}.json");
            File.WriteAllText(
                filePath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            SharedLog.Trace($"Crash report saved to: {filePath}");
            return filePath;
        }
        catch (Exception saveException)
        {
            SharedLog.Error("Failed to save crash report.", saveException);
            return string.Empty;
        }
    }

    public static IReadOnlyList<string> GetUnsent()
    {
        try
        {
            return Directory.Exists(CrashReportFolder)
                ? Directory.GetFiles(CrashReportFolder, "crash_*.json")
                : [];
        }
        catch (Exception exception)
        {
            SharedLog.Warning("Failed to enumerate unsent crash reports.", exception);
            return [];
        }
    }

    public static CrashReport? Load(string path)
    {
        if (!IsStorePath(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CrashReport>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception exception)
        {
            SharedLog.Warning($"Failed to load crash report: {path}", exception);
            return null;
        }
    }

    public static void Delete(string path)
    {
        if (!IsStorePath(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            SharedLog.Warning($"Failed to delete crash report: {path}", exception);
        }
    }

    public static void CleanupOld(int daysToKeep = 30)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
            foreach (var path in GetUnsent())
            {
                if (File.GetCreationTimeUtc(path) < cutoff)
                    Delete(path);
            }
        }
        catch (Exception exception)
        {
            SharedLog.Warning("Failed during old crash report cleanup.", exception);
        }
    }

    private static bool IsStorePath(string? path) =>
        PathSecurity.IsPathWithinAllowedDirectory(path, CrashReportFolder, allowNonExistent: true);

    private static string GetAppVersion()
    {
        try
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown";
        }
        catch (Exception exception)
        {
            SharedLog.Trace("Failed to read app version for crash report metadata.", exception);
            return "Unknown";
        }
    }
}

/// <summary>Version-neutral crash report schema shared by the desktop hosts.</summary>
public sealed class CrashReport
{
    public DateTime Timestamp { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public TimeSpan Uptime { get; set; }
    public string? InnerExceptionType { get; set; }
    public string? InnerExceptionMessage { get; set; }
    public string? InnerExceptionStackTrace { get; set; }
}
