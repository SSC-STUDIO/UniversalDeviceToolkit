using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>
/// Parse / write the UDT-marked hosts block. Only content between markers is owned by UDT;
/// surrounding hosts content is preserved.
/// </summary>
public static class HostsMarkedBlock
{
    public static string BeginMarker => NetworkAccelerationDefaults.HostsBeginMarker;
    public static string EndMarker => NetworkAccelerationDefaults.HostsEndMarker;

    public static bool TryExtract(string hostsContent, out string? blockBody)
    {
        blockBody = null;
        if (string.IsNullOrEmpty(hostsContent))
            return false;

        var begin = hostsContent.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (begin < 0)
            return false;

        var contentStart = begin + BeginMarker.Length;
        // Skip newline after begin marker
        if (contentStart < hostsContent.Length && hostsContent[contentStart] is '\r')
            contentStart++;
        if (contentStart < hostsContent.Length && hostsContent[contentStart] is '\n')
            contentStart++;

        var end = hostsContent.IndexOf(EndMarker, contentStart, StringComparison.Ordinal);
        if (end < 0)
            return false;

        blockBody = hostsContent[contentStart..end].TrimEnd('\r', '\n');
        return true;
    }

    public static string Upsert(string hostsContent, IEnumerable<string> lines)
    {
        var bodyLines = (lines ?? []).Select(l => (l ?? string.Empty).TrimEnd())
            .Where(l => l.Length > 0)
            .ToList();

        var block = BuildBlock(bodyLines);
        if (string.IsNullOrEmpty(hostsContent))
            return block + Environment.NewLine;

        if (!TryExtract(hostsContent, out _))
        {
            var trimmed = hostsContent.TrimEnd();
            return trimmed.Length == 0
                ? block + Environment.NewLine
                : trimmed + Environment.NewLine + Environment.NewLine + block + Environment.NewLine;
        }

        var begin = hostsContent.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = hostsContent.IndexOf(EndMarker, begin, StringComparison.Ordinal);
        var afterEnd = end + EndMarker.Length;
        if (afterEnd < hostsContent.Length && hostsContent[afterEnd] == '\r')
            afterEnd++;
        if (afterEnd < hostsContent.Length && hostsContent[afterEnd] == '\n')
            afterEnd++;

        var before = hostsContent[..begin].TrimEnd('\r', '\n');
        var after = hostsContent[afterEnd..];
        var sb = new StringBuilder();
        if (before.Length > 0)
        {
            sb.Append(before);
            sb.Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
        }

        sb.Append(block);
        sb.Append(Environment.NewLine);

        if (!string.IsNullOrWhiteSpace(after))
        {
            sb.Append(Environment.NewLine);
            sb.Append(after.TrimStart('\r', '\n'));
        }

        return sb.ToString();
    }

    public static string Remove(string hostsContent)
    {
        if (string.IsNullOrEmpty(hostsContent) || !TryExtract(hostsContent, out _))
            return hostsContent ?? string.Empty;

        var begin = hostsContent.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = hostsContent.IndexOf(EndMarker, begin, StringComparison.Ordinal);
        var afterEnd = end + EndMarker.Length;
        if (afterEnd < hostsContent.Length && hostsContent[afterEnd] == '\r')
            afterEnd++;
        if (afterEnd < hostsContent.Length && hostsContent[afterEnd] == '\n')
            afterEnd++;

        var before = hostsContent[..begin].TrimEnd('\r', '\n');
        var after = hostsContent[afterEnd..].TrimStart('\r', '\n');

        if (before.Length == 0)
            return after;
        if (after.Length == 0)
            return before + Environment.NewLine;
        return before + Environment.NewLine + Environment.NewLine + after;
    }

    private static string BuildBlock(IReadOnlyList<string> bodyLines)
    {
        var sb = new StringBuilder();
        sb.Append(BeginMarker);
        sb.Append(Environment.NewLine);
        foreach (var line in bodyLines)
        {
            sb.Append(line);
            sb.Append(Environment.NewLine);
        }

        sb.Append(EndMarker);
        return sb.ToString();
    }
}
