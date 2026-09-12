using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UniversalDeviceToolkit.Lib.System;

namespace UniversalDeviceToolkit.Lib.PackageDownloader.Detectors.Rules;

internal readonly struct RegistryKeyPackageRule : IPackageRule
{
    private string Key { get; init; }

    public static bool TryCreate(XmlNode? node, out RegistryKeyPackageRule value)
    {
        var key = node?.SelectSingleNode("Key")?.InnerText;

        if (key is null || !RegistryRulePath.TrySplit(key, out _, out _))
        {
            value = default;
            return false;
        }

        value = new RegistryKeyPackageRule { Key = key };
        return true;
    }

    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => KeyExists();

    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => KeyExists();

    private Task<bool> KeyExists()
    {
        if (!RegistryRulePath.TrySplit(Key, out var hive, out var path))
            return Task.FromResult(false);

        var result = Registry.KeyExists(hive, path);
        return Task.FromResult(result);
    }
}
