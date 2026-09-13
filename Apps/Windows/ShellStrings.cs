using System.Reflection;
using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal static class ShellStrings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Catalog = Load();

    internal static string Get(string language, string key)
    {
        var tag = language.Replace('_', '-');
        var exact = Catalog.Keys.FirstOrDefault(candidate => candidate.Equals(tag, StringComparison.OrdinalIgnoreCase));
        var parts = tag.Split('-');
        var baseLanguage = Catalog.Keys.FirstOrDefault(candidate => candidate.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
        var normalized = exact ?? (parts[0].Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? parts.Length > 1 && new[] { "hant", "tw", "hk", "mo" }.Contains(parts[1], StringComparer.OrdinalIgnoreCase) ? "zh-Hant" : "zh-CN"
            : baseLanguage ?? Catalog.Keys.FirstOrDefault(candidate => candidate.Split('-')[0].Equals(parts[0], StringComparison.OrdinalIgnoreCase)) ?? "en");
        return Catalog[normalized].GetValueOrDefault(key) ?? Catalog["en"].GetValueOrDefault(key) ?? key;
    }

    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UniversalDeviceToolkit.Windows.NativeLocales.json")
            ?? throw new InvalidOperationException("The native language catalog is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
            ?? throw new InvalidOperationException("The native language catalog is invalid.");
    }
}
