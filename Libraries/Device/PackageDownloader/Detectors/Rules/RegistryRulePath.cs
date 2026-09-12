using System;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal static class RegistryRulePath
{
    public static bool TrySplit(string? key, out string hive, out string path)
    {
        hive = string.Empty;
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(key) || !PathSecurity.IsValidRegistryPath(key))
            return false;

        var parts = key.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        hive = parts[0];
        path = string.Join('\\', parts.Skip(1));
        if (string.IsNullOrEmpty(path) || path.Contains(':') || Path.IsPathRooted(path))
            return false;

        return true;
    }
}
