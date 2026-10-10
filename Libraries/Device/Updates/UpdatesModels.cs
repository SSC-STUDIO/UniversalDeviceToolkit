using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Text.RegularExpressions;
using Octokit;

namespace UniversalDeviceToolkit.Lib;

public readonly struct Update(Release release)
{
    public Version Version { get; } = ParseVersion(release.TagName);
    public string TagName { get; } = release.TagName;
    public bool IsPrerelease { get; } = release.Prerelease;
    public string Title { get; } = release.Name;
    public string Description { get; } = release.Body;
    public DateTimeOffset Date { get; } = release.PublishedAt ?? release.CreatedAt;
    public string? Url { get; } = SelectSetupAsset(release)?.BrowserDownloadUrl;

    /// <summary>
    /// SHA256 hash of the update package parsed from the release body.
    /// </summary>
    public string? Sha256Hash { get; } = ExtractSha256Hash(release);

    /// <summary>
    /// URL to the SHA256 hash file for integrity verification.
    /// Supports both current `_SHA256.txt` release assets and legacy `.sha256` files.
    /// </summary>
    public string? Sha256Url { get; } = SelectSha256Asset(release)?.BrowserDownloadUrl;

    private static ReleaseAsset? SelectSetupAsset(Release release) => release.Assets
        .Where(IsSetupAsset)
        .OrderBy(GetSetupAssetPriority)
        .FirstOrDefault();

    private static ReleaseAsset? SelectSha256Asset(Release release)
    {
        var setup = SelectSetupAsset(release);
        if (setup is null)
            return null;

        return release.Assets.FirstOrDefault(asset =>
            asset.Name.Equals(setup.Name + ".sha256", StringComparison.OrdinalIgnoreCase))
            ?? release.Assets.FirstOrDefault(asset =>
                asset.Name.EndsWith("_SHA256.txt", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSetupAsset(ReleaseAsset releaseAsset) =>
        releaseAsset.Name.EndsWith("UniversalDeviceToolkitSetup.exe", StringComparison.OrdinalIgnoreCase) ||
        (releaseAsset.Name.EndsWith("setup.exe", StringComparison.OrdinalIgnoreCase) &&
         !releaseAsset.Name.Contains("_lang_", StringComparison.OrdinalIgnoreCase));

    private static bool IsEnglishOnlyAsset(ReleaseAsset releaseAsset) =>
        releaseAsset.Name.Contains("_English_", StringComparison.OrdinalIgnoreCase) ||
        releaseAsset.Name.Contains("-English", StringComparison.OrdinalIgnoreCase);

    private static bool IsOnlineOnlyAsset(ReleaseAsset releaseAsset) =>
        releaseAsset.Name.Contains("_Online_", StringComparison.OrdinalIgnoreCase) ||
        releaseAsset.Name.Contains("-Online", StringComparison.OrdinalIgnoreCase);

    private static bool IsUniversalFullAsset(ReleaseAsset releaseAsset) =>
        releaseAsset.Name.Contains("UniversalDeviceToolkit", StringComparison.OrdinalIgnoreCase) &&
        releaseAsset.Name.Contains("_Full_", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegacyBridgeAsset(ReleaseAsset releaseAsset) =>
        releaseAsset.Name.Contains("LenovoLegionToolkit", StringComparison.OrdinalIgnoreCase) &&
        releaseAsset.Name.EndsWith("_Setup.exe", StringComparison.OrdinalIgnoreCase) &&
        !IsEnglishOnlyAsset(releaseAsset) &&
        !IsOnlineOnlyAsset(releaseAsset);

    private static int GetSetupAssetPriority(ReleaseAsset releaseAsset)
    {
        if (IsUniversalFullAsset(releaseAsset))
            return 0;

        if (IsLegacyBridgeAsset(releaseAsset))
            return 1;

        if (IsOnlineOnlyAsset(releaseAsset) || IsEnglishOnlyAsset(releaseAsset))
            return 3;

        return 2;
    }

    private static string? ExtractSha256Hash(Release release)
    {
        if (string.IsNullOrWhiteSpace(release.Body))
            return null;

        var packageFileName = SelectSetupAsset(release)?.Name;

        foreach (var line in release.Body.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(packageFileName) ||
                !Regex.IsMatch(line, $@"(?:^|[\s(/\\*='"" ]){Regex.Escape(packageFileName)}(?:$|[\s)'"",;:])", RegexOptions.IgnoreCase))
                continue;

            var fileSpecificHash = TryExtractFirstSha256Hash(line);
            if (fileSpecificHash is not null)
                return fileSpecificHash;
        }

        // Historical releases may publish a bare hash instead of a filename entry.
        var legacyHash = Regex.Match(release.Body, @"^\s*(?:SHA256\s*:\s*)?([a-fA-F0-9]{64})\s*$", RegexOptions.IgnoreCase);
        return legacyHash.Success ? legacyHash.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static string? TryExtractFirstSha256Hash(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Regex.Match(text, @"(?<![a-fA-F0-9])([a-fA-F0-9]{64})(?![a-fA-F0-9])", RegexOptions.IgnoreCase);
        return match.Success
            ? match.Groups[1].Value.ToLowerInvariant()
            : null;
    }

    private static Version ParseVersion(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            throw ExceptionHelper.TagNameNullOrEmpty(nameof(tagName));

        // Strip 'v' prefix if present
        if (tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            tagName = tagName.Substring(1);

        // Strip prerelease suffix (e.g. -beta, -rc1)
        var dashIndex = tagName.IndexOf('-');
        if (dashIndex > 0)
        {
            tagName = tagName.Substring(0, dashIndex);
        }

        // Try to parse the version directly
        if (Version.TryParse(tagName, out var version))
            return version;

        // If parsing fails, try to extract version from tag name
        // Handle cases like "3" -> "3.0.0", "3.0" -> "3.0.0"
        var parts = tagName.Split('.');
        if (parts.Length == 1 && int.TryParse(parts[0], out var major))
            return new Version(major, 0);
        if (parts.Length == 2 && int.TryParse(parts[0], out var major2) && int.TryParse(parts[1], out var minor))
            return new Version(major2, minor);

        // If all else fails, throw a more descriptive error
        throw ExceptionHelper.UnparseableVersionFormat(tagName);
    }

    #region Equality

    public override bool Equals(object? obj) => obj is Update other && Version.Equals(other.Version);

    public override int GetHashCode() => Version.GetHashCode();

    public static bool operator ==(Update left, Update right) => left.Equals(right);

    public static bool operator !=(Update left, Update right) => !left.Equals(right);

    #endregion
}
