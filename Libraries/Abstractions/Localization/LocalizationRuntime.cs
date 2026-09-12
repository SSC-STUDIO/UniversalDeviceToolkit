using System.Globalization;
using System.Text;
using UniversalDeviceToolkit.Abstractions.Platform;

namespace UniversalDeviceToolkit.Abstractions.Localization;

/// <summary>
/// Process-wide language lifecycle shared by the desktop, CLI and installer hosts.
/// </summary>
public static class LocalizationRuntime
{
    private static readonly object SyncRoot = new();
    private static CultureInfo _currentCulture = LocalizationCatalog.DefaultCulture;

    public static event EventHandler<CultureChangedEventArgs>? CultureChanged;

    public static CultureInfo CurrentCulture
    {
        get
        {
            lock (SyncRoot)
                return _currentCulture;
        }
    }

    public static string LanguageFilePath => Path.Combine(ApplicationDataPaths.GetRoot(), "lang");

    public static CultureInfo Initialize(CultureInfo? preferred = null, bool persist = false)
    {
        var culture = preferred;
        if (culture is null && File.Exists(LanguageFilePath))
        {
            try
            {
                culture = LocalizationCatalog.NormalizeCulture(File.ReadAllText(LanguageFilePath, Encoding.UTF8));
            }
            catch
            {
                culture = null;
            }
        }

        culture ??= LocalizationCatalog.NormalizeCulture(CultureInfo.CurrentUICulture);
        Apply(culture, persist);
        return CurrentCulture;
    }

    public static CultureInfo Initialize(string? preferredCultureName, bool persist = false) =>
        Initialize(
            string.IsNullOrWhiteSpace(preferredCultureName)
                ? null
                : LocalizationCatalog.NormalizeCulture(preferredCultureName),
            persist);

    public static Task<CultureInfo> SetCultureAsync(CultureInfo culture, bool persist = true)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var normalized = LocalizationCatalog.NormalizeCulture(culture);
        Apply(normalized, persist);
        return Task.FromResult(normalized);
    }

    public static Task<CultureInfo> SetCultureAsync(string? cultureName, bool persist = true) =>
        SetCultureAsync(
            string.IsNullOrWhiteSpace(cultureName)
                ? LocalizationCatalog.NormalizeCulture(CultureInfo.CurrentUICulture)
                : LocalizationCatalog.NormalizeCulture(cultureName),
            persist);

    private static void Apply(CultureInfo culture, bool persist)
    {
        CultureInfo previous;
        lock (SyncRoot)
        {
            previous = _currentCulture;
            _currentCulture = culture;
        }

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        // Keep formatting deterministic for hardware values and command output.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

        if (persist)
        {
            try
            {
                var directory = Path.GetDirectoryName(LanguageFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(LanguageFilePath, culture.Name, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // A language preference must never prevent the application from starting.
            }
        }

        if (!previous.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
            CultureChanged?.Invoke(null, new CultureChangedEventArgs(previous, culture));
    }
}

public sealed class CultureChangedEventArgs : EventArgs
{
    public CultureChangedEventArgs(CultureInfo previousCulture, CultureInfo culture)
    {
        PreviousCulture = previousCulture ?? throw new ArgumentNullException(nameof(previousCulture));
        Culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public CultureInfo PreviousCulture { get; }
    public CultureInfo Culture { get; }
}
