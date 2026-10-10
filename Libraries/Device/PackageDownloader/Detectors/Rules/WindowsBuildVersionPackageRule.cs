using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal readonly struct WindowsBuildVersionPackageRule : IPackageRule
{
    private int Version { get; init; }

    public static bool TryCreate(XmlNode? node, out WindowsBuildVersionPackageRule value)
    {
        var versionString = node?.SelectSingleNode("BuildVersion")?.InnerText;

        if (!TryParseBuildVersion(versionString, out var version))
        {
            value = default;
            return false;
        }

        value = new WindowsBuildVersionPackageRule { Version = version };
        return true;
    }

    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => CheckBuildNumberAsync();

    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => CheckBuildNumberAsync();

    private async Task<bool> CheckBuildNumberAsync()
    {
        var buildNumberString = await WMI.Win32.OperatingSystem.GetBuildNumberAsync().ConfigureAwait(false);
        var buildNumber = int.TryParse(buildNumberString, out var bn) ? bn : 0;
        var result = Version <= buildNumber;
        return result;
    }

    private static bool TryParseBuildVersion(string? versionString, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        var cleaned = RemoveNonVersionCharacters(versionString);
        if (string.IsNullOrEmpty(cleaned))
            return false;

        if (int.TryParse(cleaned, out version) && version > 0)
            return true;

        var parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(parts[i], out var part) && part >= 1000)
            {
                version = part;
                return true;
            }
        }

        return false;
    }

    private static string RemoveNonVersionCharacters(string? versionString)
    {
        var arr = versionString?.ToCharArray() ?? [];
        arr = Array.FindAll(arr, c => char.IsDigit(c) || c == '.');
        return new string(arr);
    }
}
