using System.Globalization;

namespace UniversalDeviceToolkit.Abstractions.Localization;

/// <summary>
/// Platform-agnostic abstraction for retrieving localized strings.
/// </summary>
public interface IStringLocalizer
{
    /// <summary>
    /// Gets the localized string for the specified key.
    /// </summary>
    /// <param name="key">The resource key to look up.</param>
    /// <param name="fallback">A fallback value returned when the key is not found.</param>
    /// <returns>The localized string, or <paramref name="fallback"/> if the key is missing.</returns>
    string GetString(string key, string fallback = "");

    /// <summary>
    /// Gets or sets the culture used for string lookups.
    /// </summary>
    CultureInfo CurrentCulture { get; set; }
}
