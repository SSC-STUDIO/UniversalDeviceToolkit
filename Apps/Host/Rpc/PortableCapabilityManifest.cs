using System;
using System.Threading;
using System.Threading.Tasks;
#if !WINDOWS
using System.Collections.Generic;
using System.Linq;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Lib;
#endif

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Honest capability projection for the Electron client. Windows reports the
/// vendor stack that the existing handlers implement. Portable hosts report
/// only backends that are actually registered (IPlatformServices / adapters)
/// and never claim write or vendor-hardware support that is not present.
/// </summary>
internal static class PortableCapabilityManifest
{
    public static async Task<object> BuildAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if WINDOWS
        await Task.CompletedTask.ConfigureAwait(false);
        return BuildWindows();
#else
        return await BuildPortableAsync(cancellationToken).ConfigureAwait(false);
#endif
    }

    private static object BuildWindows() => new
    {
        platform = "windows",
        portable = false,
        vendorHardware = true,
        capabilities = new
        {
            settings = true,
            sensors = true,
            sensorsWrite = false,
            dashboard = true,
            autorun = true,
            systemInfo = true,
            features = true,
            automation = true,
            optimization = true,
            godMode = true,
            keyboard = true,
            rgb = true,
            spectrum = true,
            bootLogo = true,
            network = true,
            ai = true,
            driver = true,
            cleanup = true,
            macro = true,
            updates = true,
            fps = true,
            accentColor = true,
            gpuManagement = true,
            fanControl = true,
            keyboardBacklight = true,
            batteryManagement = true,
            displayControl = true,
            powerProfile = true,
            systemTelemetry = true,
        },
        backends = new
        {
            platformServices = false,
            deviceAdapter = false,
            sensorBackend = true,
            gpuBackend = true,
            powerProfile = true,
            autorun = true,
            configuration = true,
        },
        device = (object?)null,
        implementedMethods = Array.Empty<string>(),
        unsupportedMethods = Array.Empty<string>(),
    };

#if !WINDOWS
    private static async Task<object> BuildPortableAsync(CancellationToken cancellationToken)
    {
        var platformServices = IoCContainer.TryResolve<IPlatformServices>();
        var deviceAdapter = IoCContainer.TryResolve<IDeviceAdapter>();
        var sensorBackend = IoCContainer.TryResolve<ISensorBackend>();
        var gpuBackend = IoCContainer.TryResolve<IGpuBackend>();
        var powerProfile = IoCContainer.TryResolve<IPowerProfileProvider>();
        var autorun = IoCContainer.TryResolve<IAutorunManager>();
        var configuration = IoCContainer.TryResolve<IConfigurationStore>();

        var platformName = platformServices?.PlatformName
            ?? (OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "macos" : "unknown");

        object? device = null;
        if (deviceAdapter is not null)
        {
            try
            {
                var snapshot = await deviceAdapter.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
                device = new
                {
                    platform = snapshot.Identity.Platform,
                    architecture = snapshot.Identity.Architecture,
                    vendor = EmptyToNull(snapshot.Identity.Vendor),
                    model = EmptyToNull(snapshot.Identity.Model),
                    productName = EmptyToNull(snapshot.Identity.ProductName),
                    biosVersion = EmptyToNull(snapshot.Identity.BiosVersion),
                    serialNumber = EmptyToNull(snapshot.Identity.SerialNumber),
                    machineType = EmptyToNull(snapshot.Identity.MachineType),
                    source = snapshot.Source,
                    supportLevel = snapshot.Support.SupportLevel,
                    powerStatus = snapshot.PowerStatus,
                    capabilities = snapshot.Capabilities.Select(item => new
                    {
                        id = item.Id,
                        available = item.IsAvailable,
                        canWrite = item.CanWrite,
                        readOnly = item.IsReadOnly,
                        source = item.Source,
                        reason = item.Reason,
                    }).ToArray(),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                device = null;
            }
        }

        var sensorAvailable = sensorBackend is { IsAvailable: true }
            || platformServices is { SupportsSystemTelemetry: true } && sensorBackend is not null;
        var gpuAvailable = gpuBackend is { IsAvailable: true };
        var powerAvailable = powerProfile is { IsAvailable: true }
            || platformServices is { SupportsPowerProfile: true } && powerProfile is not null;
        var autorunAvailable = autorun is not null;
        var configurationAvailable = configuration is not null;
        var systemInfoAvailable = deviceAdapter is not null;

        IReadOnlyList<string>? powerProfiles = null;
        string? activePowerProfile = null;
        if (powerProfile is { IsAvailable: true })
        {
            try
            {
                powerProfiles = powerProfile.GetAvailableProfiles();
                activePowerProfile = powerProfile.GetActiveProfile();
            }
            catch (Exception)
            {
                powerProfiles = Array.Empty<string>();
            }
        }

        return new
        {
            platform = platformName,
            portable = true,
            vendorHardware = false,
            capabilities = new
            {
                settings = configurationAvailable,
                sensors = sensorAvailable,
                sensorsWrite = false,
                dashboard = configurationAvailable,
                autorun = autorunAvailable,
                systemInfo = systemInfoAvailable,
                features = true,
                automation = configurationAvailable,
                optimization = false,
                godMode = false,
                keyboard = false,
                rgb = false,
                spectrum = false,
                bootLogo = false,
                network = false,
                ai = false,
                driver = false,
                cleanup = false,
                macro = configurationAvailable,
                updates = false,
                fps = false,
                accentColor = false,
                gpuManagement = platformServices?.SupportsGpuManagement == true && gpuAvailable,
                fanControl = platformServices?.SupportsFanControl == true,
                keyboardBacklight = platformServices?.SupportsKeyboardBacklight == true,
                batteryManagement = platformServices?.SupportsBatteryManagement == true,
                displayControl = platformServices?.SupportsDisplayControl == true,
                powerProfile = powerAvailable,
                systemTelemetry = platformServices?.SupportsSystemTelemetry == true || sensorAvailable,
            },
            backends = new
            {
                platformServices = platformServices is not null,
                deviceAdapter = deviceAdapter is not null,
                sensorBackend = sensorBackend is { IsAvailable: true },
                gpuBackend = gpuAvailable,
                powerProfile = powerProfile is { IsAvailable: true },
                autorun = autorunAvailable,
                configuration = configurationAvailable,
            },
            powerProfiles = new
            {
                available = powerAvailable,
                profiles = powerProfiles ?? Array.Empty<string>(),
                active = activePowerProfile,
            },
            device,
            implementedMethods = RpcMethodNames.PortableCapable,
            unsupportedMethods = RpcMethodNames.WindowsOnly,
        };
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
#endif
}
