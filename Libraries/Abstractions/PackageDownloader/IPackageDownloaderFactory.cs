namespace UniversalDeviceToolkit.Abstractions.PackageDownloader;

/// <summary>
/// Target operating system for package queries.
/// </summary>
public enum OS
{
    Windows11,
    Windows10,
    Windows8,
    Windows7
}

/// <summary>
/// Identifies the package download source.
/// </summary>
public enum PackageDownloaderType
{
    PCSupport,
    Vantage
}

/// <summary>
/// Platform-agnostic factory for creating package downloader instances.
/// </summary>
public interface IPackageDownloaderFactory
{
    /// <summary>
    /// Gets a package downloader instance for the specified source type.
    /// </summary>
    IPackageDownloader GetInstance(PackageDownloaderType type);
}
