namespace UniversalDeviceToolkit.Abstractions.PackageDownloader;

/// <summary>
/// Platform-agnostic abstraction for downloading driver/software packages.
/// </summary>
public interface IPackageDownloader
{
    /// <summary>
    /// Retrieves the list of available packages for the specified machine and OS.
    /// </summary>
    Task<List<PackageInfo>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default);

    /// <summary>
    /// Downloads a package file to the specified location.
    /// </summary>
    Task<string> DownloadPackageFileAsync(PackageInfo package, string location, IProgress<float>? progress = null, CancellationToken token = default);
}
