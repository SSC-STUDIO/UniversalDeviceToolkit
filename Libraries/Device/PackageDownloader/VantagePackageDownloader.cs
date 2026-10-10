using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UniversalDeviceToolkit.Lib.PackageDownloader.Detectors;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.PackageDownloader;

public class VantagePackageDownloader(HttpClientFactory httpClientFactory)
    : AbstractPackageDownloader(httpClientFactory)
{
    private readonly struct PackageDefinition(string location, string category)
    {
        public string Location { get; } = location;
        public string Category { get; } = category;
    }

    private const string CATALOG_BASE_URL = "https://download.lenovo.com/catalog/";

    public override async Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default)
    {
        if (!PackageDownloadSecurity.IsValidMachineType(machineType))
            throw new ArgumentException("Machine type is invalid.", nameof(machineType));

        progress?.Report(0);

        var osString = os switch
        {
            OS.Windows11 => "win11",
            OS.Windows10 => "win10",
            OS.Windows8 => "win8",
            OS.Windows7 => "win7",
            _ => throw new ArgumentOutOfRangeException(nameof(os), os, null)
        };

        using var httpClient = HttpClientFactory.Create();

        var packageDefinitions = await GetPackageDefinitionsAsync(httpClient, $"{CATALOG_BASE_URL}{machineType}_{osString}.xml", token).ConfigureAwait(false);

        var updateDetector = new VantagePackageUpdateDetector();
        try
        {
            await updateDetector.BuildDriverInfoCache().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or global::System.Runtime.InteropServices.COMException)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to build driver info cache, continuing without it. [message={ex.Message}]", ex);
        }

        var count = 0;
        var totalCount = packageDefinitions.Count;

        var packages = new List<Package>();
        foreach (var packageDefinition in packageDefinitions)
        {
            try
            {
                var package = await GetPackage(httpClient, updateDetector, packageDefinition, token).ConfigureAwait(false);
                if (package.HasValue)
                    packages.Add(package.Value);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Couldn't load package from {packageDefinition.Location}.", ex);
            }

            count++;
            progress?.Report(totalCount == 0 ? 100 : count * 100 / totalCount);
        }

        return packages;
    }

    private static async Task<List<PackageDefinition>> GetPackageDefinitionsAsync(HttpClient httpClient, string location, CancellationToken token)
    {
        string catalogString;

        try
        {
            catalogString = await httpClient.GetStringAsync(location, token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new UpdateCatalogNotFoundException(ex.Message, ex);
        }

        var document = LoadXmlWithoutDtd(catalogString);

        var packageNodes = document.SelectNodes("/packages/package");
        if (packageNodes is null)
            return [];

        var packageDefinitions = new List<PackageDefinition>();
        foreach (var packageNode in packageNodes.OfType<XmlElement>())
        {
            token.ThrowIfCancellationRequested();

            var pLocation = packageNode.SelectSingleNode("location")?.InnerText;
            var pCategory = packageNode.SelectSingleNode("category")?.InnerText;

            if (string.IsNullOrWhiteSpace(pLocation) || string.IsNullOrWhiteSpace(pCategory))
                continue;

            if (!PackageDownloadSecurity.IsAllowedPackageDownloadUrl(pLocation))
                continue;

            packageDefinitions.Add(new(pLocation, pCategory));
        }

        return packageDefinitions;
    }

    private static async Task<Package?> GetPackage(HttpClient httpClient, VantagePackageUpdateDetector updateDetector, PackageDefinition packageDefinition, CancellationToken token)
    {
        var location = packageDefinition.Location;
        if (!Uri.TryCreate(location, UriKind.Absolute, out var packageUri)
            || !PackageDownloadSecurity.IsAllowedPackageDownloadUrl(location))
        {
            return null;
        }

        var packageString = await httpClient.GetStringAsync(location, token).ConfigureAwait(false);
        var document = LoadXmlWithoutDtd(packageString);

        var id = document.SelectSingleNode("/Package/@id")?.InnerText;
        var title = document.SelectSingleNode("/Package/Title/Desc")?.InnerText;
        var version = document.SelectSingleNode("/Package/@version")?.InnerText;
        var fileName = document.SelectSingleNode("/Package/Files/Installer/File/Name")?.InnerText;
        var fileSizeNode = document.SelectSingleNode("/Package/Files/Installer/File/Size")?.InnerText;
        var releaseDateString = document.SelectSingleNode("/Package/ReleaseDate")?.InnerText;

        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(version) ||
            string.IsNullOrWhiteSpace(fileName) ||
            string.IsNullOrWhiteSpace(fileSizeNode) ||
            string.IsNullOrWhiteSpace(releaseDateString) ||
            !PathSecurity.IsValidFileName(fileName) ||
            !int.TryParse(fileSizeNode, out var fileSizeBytes) ||
            !DateTime.TryParse(releaseDateString, out var releaseDate))
        {
            return null;
        }

        var fileUri = new Uri(packageUri, fileName);
        if (!PackageDownloadSecurity.IsAllowedPackageDownloadUrl(fileUri.ToString()))
            return null;

        var fileCrcRaw = document.SelectSingleNode("/Package/Files/Installer/File/CRC")?.InnerText;
        var fileCrc = PackageDownloadSecurity.TryParseSha256Hex(fileCrcRaw, out _) ? fileCrcRaw!.Trim() : null;
        var fileSize = $"{fileSizeBytes / 1024.0 / 1024.0:0.00} MB";
        var readmeName = document.SelectSingleNode("/Package/Files/Readme/File/Name")?.InnerText;
        string? readme = null;
        if (!string.IsNullOrWhiteSpace(readmeName) && PathSecurity.IsValidFileName(readmeName))
        {
            var readmeUri = new Uri(packageUri, readmeName);
            if (PackageDownloadSecurity.IsAllowedPackageDownloadUrl(readmeUri.ToString()))
                readme = readmeUri.ToString();
        }

        var rebootString = document.SelectSingleNode("/Package/Reboot/@type")?.InnerText ?? string.Empty;
        var reboot = int.TryParse(rebootString, out var rebootInt) ? (RebootType)rebootInt : RebootType.NotRequired;
        var baseLocation = new Uri(packageUri, "./").ToString().TrimEnd('/');

        var isUpdate = false;
        try
        {
            isUpdate = await updateDetector.DetectAsync(httpClient, document, baseLocation, token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't detect update for package {id}. [title={title}, location={location}]",
                    ex);
        }

        return new()
        {
            Id = id,
            Title = title,
            Description = string.Empty,
            Version = version,
            Category = packageDefinition.Category,
            FileName = fileName,
            FileSize = fileSize,
            FileCrc = fileCrc,
            ReleaseDate = releaseDate,
            Readme = readme,
            FileLocation = fileUri.ToString(),
            IsUpdate = isUpdate,
            Reboot = reboot
        };
    }

    private static XmlDocument LoadXmlWithoutDtd(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(new StringReader(xml), settings);
        var document = new XmlDocument { XmlResolver = null };
        document.Load(reader);
        return document;
    }
}
