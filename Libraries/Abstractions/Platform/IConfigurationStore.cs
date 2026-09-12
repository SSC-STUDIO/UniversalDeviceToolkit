namespace UniversalDeviceToolkit.Abstractions.Platform;

/// <summary>
/// Platform-agnostic abstraction for reading and writing persistent application configuration.
/// Implementations may use the Windows Registry, INI files, dconf, or other stores.
/// </summary>
public interface IConfigurationStore
{
    /// <summary>
    /// Retrieves a single configuration value.
    /// </summary>
    /// <param name="section">The configuration section or category.</param>
    /// <param name="key">The key within the section.</param>
    /// <returns>The stored value, or <see langword="null"/> if not found.</returns>
    string? GetValue(string section, string key);

    /// <summary>
    /// Stores a configuration value, or removes it when <paramref name="value"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="section">The configuration section or category.</param>
    /// <param name="key">The key within the section.</param>
    /// <param name="value">The value to store, or <see langword="null"/> to delete the entry.</param>
    void SetValue(string section, string key, string? value);

    /// <summary>
    /// Returns all key-value pairs within the specified section.
    /// </summary>
    /// <param name="section">The configuration section or category.</param>
    /// <returns>A read-only dictionary of key-value pairs in the section.</returns>
    IReadOnlyDictionary<string, string> GetSection(string section);
}
