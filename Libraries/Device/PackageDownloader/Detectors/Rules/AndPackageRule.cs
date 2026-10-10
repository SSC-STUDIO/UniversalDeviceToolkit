using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal readonly struct AndPackageRule : IPackageRule
{
    private IReadOnlyList<IPackageRule> Rules { get; init; }

    public static bool TryCreate(IEnumerable<IPackageRule> rules, out AndPackageRule value)
    {
        var materialized = rules as IPackageRule[] ?? rules.ToArray();
        if (materialized.Length == 0)
        {
            value = default;
            return false;
        }

        value = new AndPackageRule { Rules = materialized };
        return true;
    }

    public async Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        foreach (var rule in Rules ?? Array.Empty<IPackageRule>())
        {
            if (!await rule.CheckDependenciesSatisfiedAsync(driverInfoCache, httpClient, token).ConfigureAwait(false))
                return false;
        }

        return Rules is { Count: > 0 };
    }

    public async Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        foreach (var rule in Rules ?? Array.Empty<IPackageRule>())
        {
            if (!await rule.DetectInstallNeededAsync(driverInfoCache, httpClient, token).ConfigureAwait(false))
                return false;
        }

        return Rules is { Count: > 0 };
    }
}
