using System.Globalization;
using System.Resources;

namespace UniversalDeviceToolkit.Abstractions.Localization;

/// <summary>
/// Canonical culture catalog shared by every shipped UDT executable.
/// </summary>
public static class LocalizationCatalog
{
    public static IReadOnlyList<CultureInfo> SupportedCultures { get; } =
    [
        new("en"), new("ar"), new("bg"), new("cs"), new("de"), new("el"),
        new("es"), new("fr"), new("hu"), new("it"), new("ja"), new("lv"),
        new("nl-NL"), new("pl"), new("pt"), new("pt-BR"), new("ro"),
        new("ru"), new("sk"), new("tr"), new("uk"), new("vi"),
        new("zh-Hans"), new("zh-Hant"), new("uz-Latn-UZ")
    ];

    public static CultureInfo DefaultCulture { get; } = new("en");

    public static CultureInfo NormalizeCulture(CultureInfo? culture)
    {
        if (culture is null)
            return DefaultCulture;

        var exact = SupportedCultures.FirstOrDefault(item =>
            item.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        if (culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            var traditional = culture.Name.Contains("TW", StringComparison.OrdinalIgnoreCase)
                || culture.Name.Contains("HK", StringComparison.OrdinalIgnoreCase)
                || culture.Name.Contains("MO", StringComparison.OrdinalIgnoreCase);
            return SupportedCultures.First(item => item.Name.Equals(
                traditional ? "zh-Hant" : "zh-Hans", StringComparison.OrdinalIgnoreCase));
        }

        var parent = SupportedCultures.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(culture.Parent.Name)
            && item.Name.Equals(culture.Parent.Name, StringComparison.OrdinalIgnoreCase));
        if (parent is not null)
            return parent;

        return SupportedCultures.FirstOrDefault(item =>
            item.TwoLetterISOLanguageName.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            ?? DefaultCulture;
    }

    public static CultureInfo NormalizeCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return DefaultCulture;

        try
        {
            return NormalizeCulture(CultureInfo.GetCultureInfo(cultureName.Trim()));
        }
        catch (CultureNotFoundException)
        {
            return DefaultCulture;
        }
    }

    public static IEnumerable<CultureInfo> GetFallbackChain(CultureInfo? culture)
    {
        var active = NormalizeCulture(culture);
        var requestedIsChinese = IsChinese(active);
        var current = active;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (current != CultureInfo.InvariantCulture)
        {
            if ((requestedIsChinese || !IsChinese(current)) && seen.Add(current.Name))
                yield return current;

            current = current.Parent;
        }

        if (seen.Add(DefaultCulture.Name))
            yield return DefaultCulture;
    }

    public static string GetDisplayName(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (culture.Name.Equals("uz-Latn-UZ", StringComparison.OrdinalIgnoreCase))
            return "Uzbek (Latin)";

        return culture.NativeName;
    }

    public static bool IsChinese(CultureInfo? culture) =>
        culture is not null
        && culture != CultureInfo.InvariantCulture
        && culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public static bool IsRightToLeft(CultureInfo? culture) =>
        NormalizeCulture(culture).TextInfo.IsRightToLeft;

    public static string GetString(ResourceManager manager, string key, string? fallback = "", CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        fallback ??= string.Empty;

        if (string.IsNullOrWhiteSpace(key))
            return fallback;

        foreach (var candidate in GetFallbackChain(culture))
        {
            try
            {
                var set = manager.GetResourceSet(candidate, createIfNotExists: true, tryParents: false);
                var value = set?.GetString(key, ignoreCase: false);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            catch (MissingManifestResourceException)
            {
                continue;
            }
        }

        try
        {
            return manager.GetString(key, CultureInfo.InvariantCulture) ?? fallback;
        }
        catch (MissingManifestResourceException)
        {
            return fallback;
        }
    }
}
