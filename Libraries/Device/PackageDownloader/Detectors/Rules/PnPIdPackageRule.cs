using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal readonly struct PnPIdPackageRule : IPackageRule
{
    private string HardwareId { get; init; }

    public static bool TryCreate(XmlNode? node, out PnPIdPackageRule value)
    {
        var hardwareId = node?.InnerText?.Trim();

        if (hardwareId is null || !HardwareIdMatch.IsSpecificHardwareId(hardwareId))
        {
            value = default;
            return false;
        }

        value = new() { HardwareId = hardwareId };
        return true;
    }

    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient _1, CancellationToken _2)
    {
        var result = MatchingDriverInfoExists(HardwareId, driverInfoCache);
        return Task.FromResult(result);
    }

    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient _1, CancellationToken _2)
    {
        var result = MatchingDriverInfoExists(HardwareId, driverInfoCache);
        return Task.FromResult(result);
    }
    private static bool MatchingDriverInfoExists(string hardwareId, IEnumerable<DriverInfo> driverInfoCache)
    {
        return driverInfoCache.Any(di =>
            HardwareIdMatch.Matches(di.DeviceId, hardwareId) || HardwareIdMatch.Matches(di.HardwareId, hardwareId));
    }
}
