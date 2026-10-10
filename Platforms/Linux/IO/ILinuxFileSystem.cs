namespace UniversalDeviceToolkit.Platform.Linux.IO;

/// <summary>
/// Read-only view of Linux procfs/sysfs used by Host sensor and identity backends.
/// Tests inject a memory filesystem; production uses the real OS.
/// </summary>
public interface ILinuxFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    /// <summary>Returns file contents, or <see langword="null"/> when missing/unreadable.</summary>
    string? ReadText(string path);

    IReadOnlyList<string> EnumerateDirectories(string path);

    IReadOnlyList<string> EnumerateFiles(string path, string searchPattern);
}
