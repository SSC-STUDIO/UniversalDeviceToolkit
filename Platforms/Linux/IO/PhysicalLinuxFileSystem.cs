namespace UniversalDeviceToolkit.Platform.Linux.IO;

/// <summary>Real /proc and /sys reader. Missing or unreadable paths return empty results.</summary>
public sealed class PhysicalLinuxFileSystem : ILinuxFileSystem
{
    public static PhysicalLinuxFileSystem Instance { get; } = new();

    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    public string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.GetDirectories(path).OrderBy(item => item, StringComparer.Ordinal).ToArray()
                : [];
        }
        catch
        {
            return [];
        }
    }

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.GetFiles(path, searchPattern).OrderBy(item => item, StringComparer.Ordinal).ToArray()
                : [];
        }
        catch
        {
            return [];
        }
    }
}
