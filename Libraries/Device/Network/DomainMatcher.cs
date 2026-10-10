using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>
/// Host / domain matching for PAC rules and selective proxy decisions.
/// Supports exact host match and suffix match (e.g. github.com matches api.github.com).
/// </summary>
public static class DomainMatcher
{
    public static bool Matches(string? host, string? domainRule)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(domainRule))
            return false;

        var h = Normalize(host);
        var d = Normalize(domainRule);
        if (h.Length == 0 || d.Length == 0)
            return false;

        if (h.Equals(d, StringComparison.Ordinal))
            return true;

        // Suffix: host ends with ".domain"
        return h.EndsWith("." + d, StringComparison.Ordinal);
    }

    public static bool MatchesAny(string? host, IEnumerable<string>? domainRules)
    {
        if (string.IsNullOrWhiteSpace(host) || domainRules is null)
            return false;

        return domainRules.Any(rule => Matches(host, rule));
    }

    /// <summary>
    /// Explicit host-suffix allowlist. Null or empty means <b>deny all</b> (fail closed):
    /// a loopback proxy must not become an open forwarder before rules are applied.
    /// </summary>
    public static bool IsAllowed(string? host, IEnumerable<string>? allowlist)
    {
        if (allowlist is null)
            return false;

        // Materialize once so emptiness and match share the same snapshot.
        var rules = allowlist as IList<string> ?? allowlist.ToList();
        if (rules.Count == 0)
            return false;

        if (string.IsNullOrWhiteSpace(host))
            return false;

        return MatchesAny(host, rules);
    }

    public static string Normalize(string value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (v.StartsWith("*.", StringComparison.Ordinal))
            v = v[2..];
        return v.Trim('.');
    }
}
