using System.IO;

namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Read-only platform probe used by capability detection. Keeping filesystem
/// access behind this contract makes non-host platforms testable on Windows.
/// </summary>
public interface IPlatformProbe
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IReadOnlyList<string> EnumerateFiles(string path, string searchPattern, bool recursive = false);
    IReadOnlyList<string> EnumerateDirectories(string path);
}

public sealed class PhysicalPlatformProbe : IPlatformProbe
{
    public bool FileExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public bool DirectoryExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern, bool recursive = false)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(searchPattern) || !Directory.Exists(path))
            return [];

        try
        {
            return Directory.EnumerateFiles(
                    path,
                    searchPattern,
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return [];

        try
        {
            return Directory.EnumerateDirectories(path).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
