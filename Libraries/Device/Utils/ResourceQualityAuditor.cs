using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Offline auditor for .resx satellite quality. Does not machine-translate;
/// reports missing keys (English fallback expected) and contaminated non-CJK locales.
/// </summary>
public static class ResourceQualityAuditor
{
    private static readonly Regex FormatPlaceholderRegex = new(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);
    private static readonly Regex CjkIdeographRegex = new(@"\p{IsCJKUnifiedIdeographs}", RegexOptions.Compiled);
    // East-Asian punctuation that must not appear in non-CJK locales (CJK comma/period etc.).
    private static readonly char[] EastAsianPunctuation =
    [
        '\u3001', // 、
        '\u3002', // 。
        '\uFF1B', // ；
        '\uFF0C', // ，
        '\uFF1A', // ：
        '\u3010', // 【
        '\u3011', // 】
        '\u300A', // 《
        '\u300B', // 》
        '\uFF08', // （
        '\uFF09', // ）
    ];

    public sealed record AuditFinding(
        string FilePath,
        string Culture,
        string Kind,
        string Message,
        string? Key = null);

    public sealed record AuditResult(IReadOnlyList<AuditFinding> Findings)
    {
        public bool HasErrors => Findings.Any(f => f.Kind is not "MissingKey");
        public bool HasMissingKeys => Findings.Any(f => f.Kind == "MissingKey");
    }

    public static AuditResult AuditDirectory(string rootDirectory, string? englishResxFileName = "Resource.resx")
    {
        if (!Directory.Exists(rootDirectory))
            return new AuditResult(Array.Empty<AuditFinding>());

        var findings = new List<AuditFinding>();
        var resxFiles = Directory.EnumerateFiles(rootDirectory, "*.resx", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Group by folder: Resource.resx + Resource.de.resx etc.
        foreach (var group in resxFiles.GroupBy(path => Path.GetDirectoryName(path)!, StringComparer.OrdinalIgnoreCase))
        {
            var english = group.FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), englishResxFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "Resources.resx", StringComparison.OrdinalIgnoreCase));

            Dictionary<string, string>? englishMap = null;
            if (english is not null)
            {
                try
                {
                    englishMap = LoadResxMap(english, findings);
                }
                catch (Exception ex)
                {
                    findings.Add(new AuditFinding(english, "en", "ParseError", ex.Message));
                    continue;
                }
            }

            foreach (var satellite in group.Where(path => !ReferenceEquals(path, english)))
            {
                var culture = InferCultureFromFileName(Path.GetFileName(satellite));
                if (culture is null)
                    continue;

                Dictionary<string, string> map;
                try
                {
                    map = LoadResxMap(satellite, findings);
                }
                catch (Exception ex)
                {
                    findings.Add(new AuditFinding(satellite, culture, "ParseError", ex.Message));
                    continue;
                }

                // Duplicate keys are already flagged in LoadResxMap.

                if (englishMap is not null)
                {
                    foreach (var (key, englishValue) in englishMap)
                    {
                        if (!map.TryGetValue(key, out var localValue))
                        {
                            findings.Add(new AuditFinding(satellite, culture, "MissingKey",
                                $"Missing key falls back to English: {key}", key));
                            continue;
                        }

                        var enPlaceholders = ExtractPlaceholders(englishValue);
                        var localPlaceholders = ExtractPlaceholders(localValue);
                        if (!enPlaceholders.SequenceEqual(localPlaceholders))
                        {
                            findings.Add(new AuditFinding(satellite, culture, "PlaceholderMismatch",
                                $"Placeholders differ from English. en=[{string.Join(",", enPlaceholders)}] local=[{string.Join(",", localPlaceholders)}]",
                                key));
                        }

                        if (!IsChineseOrJapaneseCulture(culture) && ContainsDisallowedEastAsianContent(localValue))
                        {
                            findings.Add(new AuditFinding(satellite, culture, "EastAsianContamination",
                                "Non-CJK locale contains CJK ideographs or East-Asian punctuation.", key));
                        }
                    }
                }
                else if (!IsChineseOrJapaneseCulture(culture) &&
                         map.Values.Any(ContainsDisallowedEastAsianContent))
                {
                    foreach (var (key, value) in map.Where(pair => ContainsDisallowedEastAsianContent(pair.Value)))
                    {
                        findings.Add(new AuditFinding(satellite, culture, "EastAsianContamination",
                            "Non-CJK locale contains CJK ideographs or East-Asian punctuation.", key));
                    }
                }
            }
        }

        return new AuditResult(findings);
    }

    public static bool IsChineseOrJapaneseCulture(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return false;
        var name = cultureName.Replace('_', '-');
        return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsDisallowedEastAsianContent(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (CjkIdeographRegex.IsMatch(text))
            return true;

        return text.IndexOfAny(EastAsianPunctuation) >= 0;
    }

    public static IReadOnlyList<int> ExtractPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<int>();

        return FormatPlaceholderRegex.Matches(text)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(i => i)
            .ToArray();
    }

    private static string? InferCultureFromFileName(string fileName)
    {
        // Resource.de.resx / Resource.zh-Hans.resx / Resources.ja.resx
        var name = Path.GetFileNameWithoutExtension(fileName);
        var parts = name.Split('.');
        if (parts.Length < 2)
            return null;

        var culturePart = parts[^1];
        if (culturePart.Equals("resx", StringComparison.OrdinalIgnoreCase))
            return null;

        // Neutral base Resource.resx has no culture segment.
        if (parts.Length == 1)
            return null;

        try
        {
            _ = new CultureInfo(culturePart);
            return culturePart;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> LoadResxMap(string path, List<AuditFinding> findings)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var culture = InferCultureFromFileName(Path.GetFileName(path)) ?? "neutral";

        XDocument doc;
        try
        {
            doc = XDocument.Load(path, LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse resx XML: {ex.Message}", ex);
        }

        foreach (var data in doc.Descendants("data"))
        {
            var name = data.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            // Skip metadata / comments / file references.
            if (name.StartsWith(">>", StringComparison.Ordinal) || name.Contains('$'))
                continue;

            var value = data.Element("value")?.Value ?? string.Empty;
            if (map.ContainsKey(name))
            {
                findings.Add(new AuditFinding(path, culture, "DuplicateKey", $"Duplicate resource key: {name}", name));
                continue;
            }

            map[name] = value;
        }

        return map;
    }
}
