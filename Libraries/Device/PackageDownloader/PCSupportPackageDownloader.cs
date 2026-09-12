using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.PackageDownloader;

public class PCSupportPackageDownloader(HttpClientFactory httpClientFactory)
    : AbstractPackageDownloader(httpClientFactory)
{
    private const string CATALOG_BASE_URL = "https://pcsupport.lenovo.com/us/en/api/v4/downloads/drivers?productId=";

    public override async Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default)
    {
        if (!PackageDownloadSecurity.IsValidMachineType(machineType))
            throw new ArgumentException("Machine type is invalid.", nameof(machineType));

        var osString = os switch
        {
            OS.Windows11 => "Windows 11",
            OS.Windows10 => "Windows 10",
            OS.Windows8 => "Windows 8",
            OS.Windows7 => "Windows 7",
            _ => throw new InvalidOperationException(nameof(os)),
        };

        using var httpClient = HttpClientFactory.Create();
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://pcsupport.lenovo.com/");

        progress?.Report(0);

        var catalogJson = await httpClient.GetStringAsync($"{CATALOG_BASE_URL}{machineType}", token).ConfigureAwait(false);
        var catalogJsonNode = JsonNode.Parse(catalogJson);
        var downloadsNode = catalogJsonNode?["body"]?["DownloadItems"]?.AsArray();

        if (downloadsNode is null)
            return [];

        var packages = new List<Package>();
        foreach (var downloadNode in downloadsNode)
        {
            if (!IsCompatible(downloadNode, osString))
                continue;

            var package = ParsePackage(downloadNode!);
            if (package is null)
                continue;

            packages.Add(package.Value);
        }

        return packages;
    }

    private static Package? ParsePackage(JsonNode downloadNode)
    {
        var idNode = downloadNode["ID"];
        if (idNode is null) return null;
        var id = idNode.ToJsonString();

        var category = downloadNode["Category"]?["Name"]?.ToString();
        if (category is null) return null;

        var title = downloadNode["Title"]?.ToString();
        if (title is null) return null;

        var description = downloadNode["Summary"]?.ToString() ?? string.Empty;
        var version = downloadNode["SummaryInfo"]?["Version"]?.ToString() ?? string.Empty;

        var filesNode = downloadNode["Files"]?.AsArray();
        if (filesNode is null || filesNode.Count == 0)
            return null;

        var mainFileNode = filesNode.FirstOrDefault(n => n?["TypeString"]?.ToString().Equals("exe", StringComparison.OrdinalIgnoreCase) == true)
                           ?? filesNode.FirstOrDefault(n => n?["TypeString"]?.ToString().Equals("zip", StringComparison.OrdinalIgnoreCase) == true)
                           ?? filesNode.FirstOrDefault();

        if (mainFileNode is null)
            return null;

        var fileLocation = mainFileNode["URL"]?.ToString();
        if (fileLocation is null || !PackageDownloadSecurity.IsAllowedPackageDownloadUrl(fileLocation))
            return null;

        if (!Uri.TryCreate(fileLocation, UriKind.Absolute, out var fileUri))
            return null;

        var fileName = PathSecurity.SanitizeFileName(Uri.UnescapeDataString(fileUri.Segments.LastOrDefault("file")));
        var fileSize = mainFileNode["Size"]?.ToString() ?? string.Empty;
        var fileCrcRaw = mainFileNode["SHA256"]?.ToString();
        var fileCrc = PackageDownloadSecurity.TryParseSha256Hex(fileCrcRaw, out _) ? fileCrcRaw!.Trim() : null;

        var dateUnixStr = mainFileNode["Date"]?["Unix"]?.ToString();
        var releaseDate = dateUnixStr is not null && long.TryParse(dateUnixStr, out var unix)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unix).DateTime
            : DateTime.MinValue;

        var readmeFileNode = filesNode.FirstOrDefault(n => n?["TypeString"]?.ToString().Equals("txt readme", StringComparison.OrdinalIgnoreCase) == true)
                              ?? filesNode.FirstOrDefault(n => n?["TypeString"]?.ToString().Equals("html", StringComparison.OrdinalIgnoreCase) == true);

        var readmeRaw = readmeFileNode?["URL"]?.ToString();
        var readme = !string.IsNullOrWhiteSpace(readmeRaw) && PackageDownloadSecurity.IsAllowedPackageDownloadUrl(readmeRaw)
            ? readmeRaw.Trim()
            : null;

        return new()
        {
            Id = id,
            Title = title,
            Description = title == description ? string.Empty : description,
            Version = version,
            Category = category,
            FileName = fileName,
            FileSize = fileSize,
            FileCrc = fileCrc,
            ReleaseDate = releaseDate,
            Readme = readme,
            FileLocation = fileLocation,
        };
    }

    private static bool IsCompatible(JsonNode? downloadNode, string osString)
    {
        var operatingSystems = downloadNode?["OperatingSystemKeys"]?.AsArray();

        if (operatingSystems is null || operatingSystems.IsEmpty())
            return true;

        foreach (var operatingSystem in operatingSystems)
            if (operatingSystem is not null && operatingSystem.ToString().StartsWith(osString, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
