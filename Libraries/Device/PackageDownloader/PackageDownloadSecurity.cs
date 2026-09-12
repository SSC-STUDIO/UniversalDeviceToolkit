using System;
using System.IO;
using System.Security.Cryptography;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.PackageDownloader;

internal static class PackageDownloadSecurity
{
    public static bool IsValidMachineType(string? machineType)
    {
        if (string.IsNullOrWhiteSpace(machineType))
            return false;

        if (machineType.Length is < 2 or > 16)
            return false;

        foreach (var c in machineType)
        {
            if (!char.IsAsciiLetterOrDigit(c))
                return false;
        }

        return true;
    }

    public static bool IsAllowedPackageDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return false;

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host) || host.Contains("..", StringComparison.Ordinal))
            return false;

        return host.Equals("lenovo.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".lenovo.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseSha256Hex(string? hex, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var trimmed = hex.Trim();
        if (trimmed.Length != 64)
            return false;

        foreach (var c in trimmed)
        {
            var isHex = (c >= '0' && c <= '9')
                        || (c >= 'a' && c <= 'f')
                        || (c >= 'A' && c <= 'F');
            if (!isHex)
                return false;
        }

        bytes = Convert.FromHexString(trimmed);
        return bytes.Length == 32;
    }

    public static bool Sha256Equals(string actualHex, string expectedHex)
    {
        if (!TryParseSha256Hex(actualHex, out var actual) || !TryParseSha256Hex(expectedHex, out var expected))
            return false;

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string? CreateSafePackageFilePath(string location, string title, string fileName)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        string destinationDirectory;
        try
        {
            destinationDirectory = Path.GetFullPath(location);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!Directory.Exists(destinationDirectory))
            return null;

        var safeName = PathSecurity.SanitizeFileName(title) + " - " + PathSecurity.SanitizeFileName(Path.GetFileName(fileName));
        return PathSecurity.CreateSafeFilePath(destinationDirectory, safeName);
    }
}
