using System;
using System.IO;
using System.Linq;

namespace UniversalDeviceToolkit.Shared.Utils;

/// <summary>
/// Cross-platform path security utilities.
/// Extracted from Lib.Utils.PathSecurity — this file is the single
/// implementation; UniversalDeviceToolkit.Lib.Utils.PathSecurity is a thin
/// delegating wrapper for ABI compatibility.
/// </summary>
public static class PathSecurity
{
    // Windows reserved device names that could be used for attacks
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    // Dangerous path patterns that indicate path traversal attempts
    private static readonly string[] DangerousPathPatterns =
    [
        "..", "~", "%", "$", "@", "|", ">", "<", "*", "?", "\"", "'", "\0"
    ];

    // Invalid file name characters (cross-platform superset)
    private static readonly char[] InvalidFileNameChars =
    [
        '<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0'
    ];

    /// <summary>
    /// Validates that a file name does not contain path traversal or other dangerous patterns.
    /// </summary>
    public static bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.Contains('/') || fileName.Contains('\\'))
            return false;

        foreach (var pattern in DangerousPathPatterns)
        {
            if (fileName.Contains(pattern))
                return false;
        }

        if (fileName.IndexOfAny(InvalidFileNameChars) >= 0)
            return false;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(nameWithoutExt))
            return false;

        if (ReservedDeviceNames.Any(r => nameWithoutExt.Equals(r, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (fileName.EndsWith(".", StringComparison.Ordinal) || fileName.EndsWith(" ", StringComparison.Ordinal))
            return false;

        return true;
    }

    /// <summary>
    /// Validates a full path to ensure it doesn't escape the allowed base directory.
    /// Also resolves symbolic links and junction points to prevent symlink-based path traversal.
    /// </summary>
    public static bool IsPathWithinAllowedDirectory(string? path, string? basePath, bool allowNonExistent = true)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(basePath))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullBasePath = Path.GetFullPath(basePath);

            if (!IsUnderAllowedRoot(fullPath, fullBasePath))
                return false;

            if (fullPath.Contains(".." + Path.DirectorySeparatorChar) ||
                fullPath.Contains(".." + Path.AltDirectorySeparatorChar))
                return false;

            // SECURITY: Resolve symbolic links / junction points to prevent symlink-based traversal.
            // An attacker could create a symlink inside the allowed directory pointing outside it.
            // Unconditional (matching the legacy Lib behavior): validated whenever the path exists,
            // regardless of allowNonExistent.
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                var isReparsePoint = (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0;
                var resolvedPath = ResolveSymbolicLinks(fullPath);
                if (isReparsePoint && resolvedPath is null)
                    return false;
                if (resolvedPath is not null && !IsUnderAllowedRoot(resolvedPath, fullBasePath))
                    return false;
            }

            if (!allowNonExistent)
            {
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to resolve the final target of a symbolic link or junction point.
    /// Returns null if the path is not a reparse point.
    /// </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsUnderAllowedRoot(string fullPath, string fullBasePath)
    {
        var baseWithSeparator = fullBasePath;
        if (!baseWithSeparator.EndsWith(Path.DirectorySeparatorChar) &&
            !baseWithSeparator.EndsWith(Path.AltDirectorySeparatorChar))
        {
            baseWithSeparator += Path.DirectorySeparatorChar;
        }

        if (fullPath.StartsWith(baseWithSeparator, PathComparison))
            return true;

        var trimmedBase = baseWithSeparator.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(trimmedBase, PathComparison);
    }

    private static string? ResolveSymbolicLinks(string path)
    {
        try
        {
            FileSystemInfo info = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
            if (!info.Exists)
                return null;

            if (info.LinkTarget is null && (info.Attributes & FileAttributes.ReparsePoint) == 0)
                return null;

            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sanitizes a file name by removing or replacing dangerous characters.
    /// </summary>
    public static string SanitizeFileName(string? fileName, string? replacement = "_")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unnamed";

        replacement = string.IsNullOrEmpty(replacement) ? "_" : replacement;

        // Remove directory separators first
        var sanitized = fileName.Replace("/", replacement).Replace("\\", replacement);

        // Remove dangerous patterns
        foreach (var pattern in DangerousPathPatterns)
        {
            sanitized = sanitized.Replace(pattern, replacement);
        }

        // Remove invalid characters
        foreach (var c in InvalidFileNameChars)
        {
            sanitized = sanitized.Replace(c.ToString(), replacement);
        }

        // Check for reserved device names
        var nameWithoutExt = Path.GetFileNameWithoutExtension(sanitized);
        if (!string.IsNullOrEmpty(nameWithoutExt) &&
            ReservedDeviceNames.Any(r => nameWithoutExt.Equals(r, StringComparison.OrdinalIgnoreCase)))
        {
            sanitized = "_" + sanitized;
        }

        // Trim trailing dots and spaces
        sanitized = sanitized.TrimEnd('.', ' ');

        // Ensure not empty
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "unnamed";

        return sanitized;
    }

    /// <summary>
    /// Creates a safe file path by combining a base directory with a file name,
    /// ensuring the result stays within the base directory.
    /// </summary>
    public static string? CreateSafeFilePath(string baseDirectory, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(fileName))
            return null;

        // Sanitize the file name first
        var sanitizedFileName = SanitizeFileName(fileName);

        // Combine paths
        var fullPath = Path.Combine(baseDirectory, sanitizedFileName);

        // Validate the result is within the base directory
        if (!IsPathWithinAllowedDirectory(fullPath, baseDirectory))
            return null;

        return fullPath;
    }

    /// <summary>
    /// Validates a plugin ID to ensure it doesn't contain path traversal patterns.
    /// </summary>
    public static bool IsValidPluginId(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return false;

        // Plugin IDs should be alphanumeric with limited safe characters
        foreach (char c in pluginId)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                return false;
        }

        // Check for dangerous patterns
        if (pluginId.Contains(".."))
            return false;

        // Must start with letter
        if (!char.IsLetter(pluginId[0]))
            return false;

        return true;
    }

    /// <summary>
    /// Validates a directory path for safety.
    /// </summary>
    public static bool IsValidDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Check for dangerous patterns
            foreach (var pattern in DangerousPathPatterns)
            {
                if (path.Contains(pattern))
                    return false;
            }

            // Try to get full path - this will throw for invalid paths
            var fullPath = Path.GetFullPath(path);

            // Check path length
            if (fullPath.Length > 260) // Windows MAX_PATH
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a registry path for safety.
    /// </summary>
    public static bool IsValidRegistryPath(string? registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath))
            return false;

        // Only allow specific registry roots
        var allowedRoots = new[]
        {
            "HKEY_CURRENT_USER",
            "HKEY_LOCAL_MACHINE",
            "HKEY_CLASSES_ROOT",
            "HKEY_USERS",
            "HKCU",
            "HKLM",
            "HKCR",
            "HKU"
        };

        var upperPath = registryPath.ToUpperInvariant();

        bool startsWithAllowedRoot = false;
        foreach (var root in allowedRoots)
        {
            if (HasRegistryRoot(upperPath, root) || HasRegistryRoot(upperPath, "\\" + root))
            {
                startsWithAllowedRoot = true;
                break;
            }
        }

        if (!startsWithAllowedRoot)
            return false;

        // Check for path traversal in registry path
        if (registryPath.Contains(".."))
            return false;

        // Check for null bytes
        if (registryPath.Contains('\0'))
            return false;

        return true;
    }

    // Dynamically resolved system driver directories — avoids hardcoding C:\ drive letter
    // which breaks validation when Windows is installed on a different drive (D:\, etc.).
    private static readonly string[] AllowedDriverRoots = InitDriverRoots();

    private static string[] InitDriverRoots()
    {
        // Environment.SystemDirectory returns e.g. "C:\Windows\System32" or "D:\Windows\System32"
        // regardless of which drive Windows is installed on.
        var systemDir = Environment.SystemDirectory;

        return new[]
        {
            Path.Combine(systemDir, "drivers"),     // e.g. C:\Windows\System32\drivers
            Path.Combine(systemDir, "DriverStore"),  // e.g. C:\Windows\System32\DriverStore
        };
    }

    /// <summary>
    /// Validates a driver path for safety.
    /// On non-Windows platforms this always returns false (no driver roots exist).
    /// </summary>
    public static bool IsValidDriverPath(string? driverPath)
    {
        if (string.IsNullOrWhiteSpace(driverPath))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(driverPath);

            // Must be under an allowed driver root (directory boundary, not bare prefix).
            // Without a trailing separator, "…\System32\driversEvil\x.sys" would match "…\drivers".
            bool inAllowedLocation = false;
            foreach (var root in AllowedDriverRoots)
            {
                if (IsUnderAllowedRoot(fullPath, Path.GetFullPath(root)))
                {
                    inAllowedLocation = true;
                    break;
                }
            }

            if (!inAllowedLocation)
                return false;

            // Check for path traversal
            if (fullPath.Contains(".."))
                return false;

            // Must be a .sys file
            if (!fullPath.EndsWith(".sys", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasRegistryRoot(string upperPath, string root)
    {
        if (upperPath.Length < root.Length || !upperPath.StartsWith(root, StringComparison.Ordinal))
            return false;

        return upperPath.Length == root.Length || upperPath[root.Length] == '\\';
    }
}
