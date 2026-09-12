using System;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Release-tag helpers for application updates. Historic plugin-catalog tags
/// (and any future rolling release channels) must never surface as app updates.
/// </summary>
public static class ReleaseTagPolicy
{
    public static bool IsCatalogTag(string? tag) =>
        tag is not null && (
            string.Equals(tag, "plugin-catalog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tag, "plugin-catalog-preview", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when a GitHub release is a public application update: not a draft,
    /// not a rolling plugin-catalog tag, and not a prerelease unless requested.
    /// SemVer hyphens are treated as prerelease even if the GitHub flag is off.
    /// </summary>
    public static bool IsPublicApplicationRelease(
        string? tagName,
        bool draft,
        bool prerelease,
        bool includePrerelease)
    {
        if (draft || IsCatalogTag(tagName))
            return false;

        if (!includePrerelease && (prerelease || IsPrereleaseApplicationVersion(tagName)))
            return false;

        return true;
    }

    /// <summary>
    /// True when the version label contains a SemVer prerelease hyphen, matching
    /// <c>Release.yml</c> (tags such as <c>v6.0.0-preview.1</c>). Build metadata
    /// after <c>+</c> is ignored.
    /// </summary>
    public static bool IsPrereleaseApplicationVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return false;

        var core = informationalVersion.Trim();
        if (core.StartsWith('v') || core.StartsWith('V'))
            core = core[1..];

        var plus = core.IndexOf('+');
        if (plus >= 0)
            core = core[..plus];

        return core.Contains('-', StringComparison.Ordinal);
    }
}
