using System;
using System.IO;
using System.Security.Cryptography;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Validates installer launch paths before elevation/execution.
/// </summary>
public static class InstallerLaunchPathValidator
{
    public const int Sha256HexLength = 64;

    public static bool IsSha256Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != Sha256HexLength)
            return false;

        foreach (var character in value)
        {
            var isDigit = character is >= '0' and <= '9';
            var isUpperHex = character is >= 'A' and <= 'F';
            var isLowerHex = character is >= 'a' and <= 'f';
            if (!isDigit && !isUpperHex && !isLowerHex)
                return false;
        }

        return true;
    }

    public static bool TryComputeSha256Hex(string? filePath, out string sha256Hex, out string failureReason)
    {
        sha256Hex = string.Empty;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            failureReason = "Installer path is empty.";
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            sha256Hex = Convert.ToHexString(SHA256.HashData(stream));
            return true;
        }
        catch (Exception ex)
        {
            sha256Hex = string.Empty;
            failureReason = $"Failed to hash installer file: {ex.Message}";
            return false;
        }
    }

    public static bool TryValidateForExecution(
        string? installerPath,
        string? downloadDirectory,
        string? expectedFileName,
        string? expectedSha256Hex,
        out string normalizedInstallerPath,
        out string failureReason)
    {
        normalizedInstallerPath = string.Empty;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(installerPath))
        {
            failureReason = "Installer path is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(downloadDirectory))
        {
            failureReason = "Download directory is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(expectedFileName))
        {
            failureReason = "Expected installer file name is empty.";
            return false;
        }

        if (!IsSha256Hex(expectedSha256Hex))
        {
            failureReason = "Installer checksum is missing or invalid.";
            return false;
        }

        string normalizedDownloadDirectory;
        try
        {
            normalizedInstallerPath = Path.GetFullPath(installerPath);
            normalizedDownloadDirectory = Path.GetFullPath(downloadDirectory);
        }
        catch (Exception ex)
        {
            normalizedInstallerPath = string.Empty;
            failureReason = $"Invalid installer path: {ex.Message}";
            return false;
        }

        if (!File.Exists(normalizedInstallerPath))
        {
            normalizedInstallerPath = string.Empty;
            failureReason = "Installer file does not exist.";
            return false;
        }

        var normalizedDownloadPrefix = EnsureTrailingSeparator(normalizedDownloadDirectory);
        if (!normalizedInstallerPath.StartsWith(normalizedDownloadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedInstallerPath = string.Empty;
            failureReason = "Installer path is outside the configured download directory.";
            return false;
        }

        var actualFileName = Path.GetFileName(normalizedInstallerPath);
        if (!string.Equals(actualFileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
        {
            normalizedInstallerPath = string.Empty;
            failureReason = $"Unexpected installer file name: {actualFileName}.";
            return false;
        }

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(normalizedInstallerPath);
        }
        catch (Exception ex)
        {
            normalizedInstallerPath = string.Empty;
            failureReason = $"Failed to inspect installer file attributes: {ex.Message}";
            return false;
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            normalizedInstallerPath = string.Empty;
            failureReason = "Installer path points to a directory.";
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            normalizedInstallerPath = string.Empty;
            failureReason = "Installer path points to a reparse point.";
            return false;
        }

        if (!TryComputeSha256Hex(normalizedInstallerPath, out var actualSha256Hex, out failureReason))
        {
            normalizedInstallerPath = string.Empty;
            return false;
        }

        if (!string.Equals(actualSha256Hex, expectedSha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            normalizedInstallerPath = string.Empty;
            failureReason = "Installer checksum mismatch.";
            return false;
        }

        return true;
    }

    private static string EnsureTrailingSeparator(string directoryPath)
    {
        if (directoryPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) || directoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            return directoryPath;

        return directoryPath + Path.DirectorySeparatorChar;
    }
}
