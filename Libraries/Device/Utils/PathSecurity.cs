using SharedPathSecurity = UniversalDeviceToolkit.Shared.Utils.PathSecurity;

namespace UniversalDeviceToolkit.Lib.Utils;

// Thin ABI-compatible wrapper over UniversalDeviceToolkit.Shared.Utils.PathSecurity
// (single source of truth). All validation logic lives in the Shared assembly.
public static class PathSecurity
{
    public static bool IsValidFileName(string? fileName) => SharedPathSecurity.IsValidFileName(fileName);

    public static bool IsPathWithinAllowedDirectory(string? path, string? basePath, bool allowNonExistent = true) =>
        SharedPathSecurity.IsPathWithinAllowedDirectory(path, basePath, allowNonExistent);

    public static string SanitizeFileName(string? fileName, string replacement = "_") =>
        SharedPathSecurity.SanitizeFileName(fileName, replacement);

    public static string? CreateSafeFilePath(string baseDirectory, string? fileName) =>
        SharedPathSecurity.CreateSafeFilePath(baseDirectory, fileName);

    public static bool IsValidPluginId(string? pluginId) => SharedPathSecurity.IsValidPluginId(pluginId);

    public static bool IsValidDirectoryPath(string? path) => SharedPathSecurity.IsValidDirectoryPath(path);

    public static bool IsValidRegistryPath(string? registryPath) => SharedPathSecurity.IsValidRegistryPath(registryPath);

    public static bool IsValidDriverPath(string? driverPath) => SharedPathSecurity.IsValidDriverPath(driverPath);
}
