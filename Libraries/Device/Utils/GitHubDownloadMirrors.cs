using System;
using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Generates fallback download URLs via public GitHub proxy mirrors for networks
/// where github.com / raw.githubusercontent.com are unreachable or unstable.
/// The direct URL is always yielded first; mirror variants are appended as fallbacks.
/// </summary>
public static class GitHubDownloadMirrors
{
    private static readonly string[] ProxyMirrorPrefixes =
    {
        "https://gh-proxy.com/",
        "https://ghfast.top/",
    };

    public static bool IsMirrorHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) &&
        (host.Equals("gh-proxy.com", StringComparison.OrdinalIgnoreCase) ||
         host.Equals("ghfast.top", StringComparison.OrdinalIgnoreCase));

    public static bool IsMirrorableUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
               || uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Yields the direct URL first, then proxy-mirror variants for GitHub-hosted URLs.
    /// Non-GitHub URLs (including existing mirror URLs) are yielded as-is.
    /// </summary>
    public static IEnumerable<string> WithMirrorFallbacks(string url)
    {
        yield return url;

        if (!IsMirrorableUrl(url))
            yield break;

        foreach (var prefix in ProxyMirrorPrefixes)
            yield return prefix + url;
    }
}
