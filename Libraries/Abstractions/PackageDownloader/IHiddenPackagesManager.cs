namespace UniversalDeviceToolkit.Abstractions.PackageDownloader;

/// <summary>
/// Platform-agnostic manager for hidden (user-dismissed) packages.
/// </summary>
public interface IHiddenPackagesManager
{
    /// <summary>
    /// Gets the set of hidden package IDs.
    /// </summary>
    IReadOnlySet<string> HiddenPackageIds { get; }

    /// <summary>
    /// Clears all hidden packages and persists the change.
    /// </summary>
    void ClearHiddenPackages();
}
