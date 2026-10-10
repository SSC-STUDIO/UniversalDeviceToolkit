// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Abstractions.PackageDownloader.Detectors.Rules;

/// <summary>
/// Cross-platform package detection rule for driver installation decisions.
/// </summary>
public interface IPackageRule
{
    /// <summary>
    /// Checks if dependencies are satisfied on the current system.
    /// </summary>
    Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token);

    /// <summary>
    /// Detects if installation is needed based on detected drivers.
    /// </summary>
    Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token);
}
