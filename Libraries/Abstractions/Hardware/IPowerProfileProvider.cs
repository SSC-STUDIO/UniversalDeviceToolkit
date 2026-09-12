namespace UniversalDeviceToolkit.Abstractions.Hardware;

/// <summary>
/// Abstraction for managing system power profiles (e.g. Performance, Balanced, Power Saver).
/// Implementations may use Windows power plans, Linux TLP profiles, or other platform APIs.
/// </summary>
public interface IPowerProfileProvider
{
    /// <summary>
    /// Gets a value indicating whether the power profile provider is available and functional.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns all available power profile names.
    /// </summary>
    /// <returns>A read-only list of profile names.</returns>
    IReadOnlyList<string> GetAvailableProfiles();

    /// <summary>
    /// Gets the name of the currently active power profile, or <see langword="null"/> if unknown.
    /// </summary>
    string? GetActiveProfile();

    /// <summary>
    /// Activates the specified power profile.
    /// </summary>
    /// <param name="profileName">The name of the profile to activate (must be one of <see cref="GetAvailableProfiles"/>).</param>
    Task SetActiveProfileAsync(string profileName);
}
