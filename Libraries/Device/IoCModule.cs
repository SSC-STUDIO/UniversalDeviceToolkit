using System;
using System.Linq;
#if !WINDOWS
using System.Reflection;
#endif
using Autofac;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Abstractions.Utils;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
#if WINDOWS
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.GodMode;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.Acer;
using UniversalDeviceToolkit.Lib.Features.Asus;
using UniversalDeviceToolkit.Lib.Features.Clevo;
using UniversalDeviceToolkit.Lib.Features.CursorPointer;
using UniversalDeviceToolkit.Lib.Features.Dell;
using UniversalDeviceToolkit.Lib.Features.FlipToStart;
using UniversalDeviceToolkit.Lib.Features.Hp;
using UniversalDeviceToolkit.Lib.Features.Msi;
using UniversalDeviceToolkit.Lib.Features.Razer;
using UniversalDeviceToolkit.Lib.Features.Tongfang;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.Features.Hybrid.Notify;
using UniversalDeviceToolkit.Lib.Features.InstantBoot;
using UniversalDeviceToolkit.Lib.Features.OverDrive;
using UniversalDeviceToolkit.Lib.Features.PanelLogo;
using UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight;
using UniversalDeviceToolkit.Lib.GameDetection;
using UniversalDeviceToolkit.Lib.Integrations;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Overclocking.Amd;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Notifications;
using UniversalDeviceToolkit.Lib.Optimization;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using UniversalDeviceToolkit.Lib.ResourcesCatalog;
using UniversalDeviceToolkit.Lib.Services;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Driver;
using UniversalDeviceToolkit.Lib.System.EC;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.System.Razer;
#endif

namespace UniversalDeviceToolkit.Lib;

public class IoCModule : Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<HttpClientFactory>().SingleInstance();

        // Register default delay provider for production code so IDelayProvider can be injected
        builder.Register<DefaultDelayProvider>().As<IDelayProvider>().SingleInstance();

#if WINDOWS
        builder.Register<OnlineResourceCatalogClient>();
        builder.RegisterType<AppNotificationService>().As<IAppNotificationService>().SingleInstance();

        // Register compatibility service
        builder.Register<CompatibilityService>().As<ICompatibilityService>().SingleInstance();
        builder.RegisterInstance(LenovoDeviceSupportProvider.Instance)
            .As<IDeviceSupportProvider>()
            .As<IInstalledDeviceSupportProvider>()
            .SingleInstance();
        builder.Register<DevicePackManager>();

        // Register hardware abstraction wrappers
        builder.Register<WMIWrapper>().As<IWMIWrapper>().SingleInstance();
        builder.Register<DriverWrapper>().As<IDriverWrapper>().SingleInstance();
        builder.Register<AsusAtkDriver>().As<IAsusAtkDriver>().SingleInstance();
        builder.Register<HpWmiBios>().As<IHpWmiBios>().SingleInstance();
        builder.Register<AlienwareWmi>().As<IAlienwareWmi>().SingleInstance();
        builder.Register<AcerWmi>().As<IAcerWmi>().SingleInstance();
        builder.Register<GigabyteWmi>().As<IGigabyteWmi>().SingleInstance();
        builder.Register<PawnIoEcChannel>().As<IEcChannel>().SingleInstance();
        builder.Register<RazerHidDriver>().As<IRazerHid>().SingleInstance();
        builder.Register<RazerHidController>().As<IRazerHidController>().SingleInstance();

        builder.Register<FnKeysDisabler>();
        builder.Register<LegionZoneDisabler>();
        builder.Register<VantageDisabler>();

        // ApplicationSettings is registered via pre-build action in StartupOrchestrator
        // to reuse the pre-created instance and avoid double-instantiation.
        builder.Register<OsdSettings>();
        builder.Register<HardwareSensorSettings>().SingleInstance();
        builder.Register<BalanceModeSettings>();
        builder.Register<GodModeSettings>();
        builder.Register<GPUOverclockSettings>();
        builder.Register<IntegrationsSettings>();
        builder.Register<LampArraySettings>();
        builder.Register<FanCurveSettings>().SingleInstance();
        builder.Register<PackageDownloaderSettings>();
        builder.Register<RGBKeyboardSettings>();
        builder.Register<SpectrumKeyboardSettings>();
        builder.Register<SunriseSunsetSettings>();
        builder.Register<UpdateCheckSettings>();
        builder.Register<GameBoostSettings>();
        builder.Register<CursorPointerSettings>().SingleInstance();
        builder.Register<CursorPointerService>().SingleInstance();

        builder.Register<AlwaysOnUSBFeature>();
        builder.Register<HardwareSensorsFeature>();
        builder.Register<BatteryFeature>();
        builder.Register<BatteryNightChargeFeature>();
        builder.Register<DpiScaleFeature>();
        builder.Register<FlipToStartFeature>();
        builder.Register<FlipToStartCapabilityFeature>(true);
        builder.Register<FlipToStartUEFIFeature>(true);
        builder.Register<FnLockFeature>();
        builder.Register<GSyncFeature>();
        builder.Register<HDRFeature>();
        builder.Register<HybridModeFeature>();
        builder.Register<IGPUModeFeature>();
        builder.Register<IGPUModeCapabilityFeature>(true);
        builder.Register<IGPUModeFeatureFlagsFeature>(true);
        builder.Register<IGPUModeGamezoneFeature>(true);
        builder.Register<ITSModeFeature>();
        builder.Register<InstantBootFeature>();
        builder.Register<InstantBootFeatureFlagsFeature>(true);
        builder.Register<InstantBootCapabilityFeature>(true);
        builder.Register<MicrophoneFeature>();
        builder.Register<OneLevelWhiteKeyboardBacklightFeature>();
        builder.Register<OverDriveFeature>();
        builder.Register<OverDriveGameZoneFeature>(true);
        builder.Register<OverDriveCapabilityFeature>(true);
        builder.Register<PanelLogoBacklightFeature>();
        builder.Register<PanelLogoSpectrumBacklightFeature>(true);
        builder.Register<PanelLogoLenovoLightingBacklightFeature>(true);
        builder.Register<PortsBacklightFeature>();
        builder.Register<PowerModeFeature>();
        builder.Register<LenovoPowerModeFeature>(true);
        builder.Register<AsusPowerModeFeature>(true);
        builder.Register<AsusBatteryChargeLimitFeature>(true);
        builder.Register<HpPowerModeFeature>(true);
        builder.Register<RazerPowerModeFeature>(true);
        builder.Register<AlienwarePowerModeFeature>(true);
        builder.Register<AcerPowerModeFeature>(true);
        builder.Register<MsiPowerModeFeature>(true);
        builder.Register<MsiBatteryChargeLimitFeature>(true);
        builder.Register<MsiCoolerBoostFeature>(true);
        builder.Register<TongfangPowerModeFeature>(true);
        builder.Register<ClevoPowerModeFeature>(true);
        builder.Register<RefreshRateFeature>();
        builder.Register<ResolutionFeature>();
        builder.Register<SpeakerFeature>();
        builder.Register<TouchpadLockFeature>();
        builder.Register<WhiteKeyboardBacklightFeature>();
        builder.Register<WhiteKeyboardDriverBacklightFeature>(true);
        builder.Register<WhiteKeyboardLenovoLightingBacklightFeature>(true);
        builder.Register<WinKeyFeature>();

        builder.Register<DGPUNotify>();
        builder.Register<DGPUCapabilityNotify>(true);
        builder.Register<DGPUFeatureFlagsNotify>(true);
        builder.Register<DGPUGamezoneNotify>(true);

        builder.Register<DisplayBrightnessListener>().AutoActivateListener();
        builder.Register<DisplayConfigurationListener>().AutoActivateListener();
        builder.Register<DriverKeyListener>().AutoActivateListener();
        builder.Register<LightingChangeListener>().AutoActivateListener();
        builder.Register<NativeWindowsMessageListener>().AutoActivateListener();
        builder.Register<PowerModeListener>().AutoActivateListener();
        builder.Register<PowerStateListener>().AutoActivateListener();
        builder.Register<RGBKeyboardBacklightListener>().AutoActivateListener();
        builder.Register<SessionLockUnlockListener>().AutoActivateListener();
        builder.Register<SpecialKeyListener>().AutoActivateListener();
        builder.Register<SystemThemeListener>().AutoActivateListener();
        builder.Register<ThermalModeListener>().AutoActivateListener();
        builder.Register<WinKeyListener>().AutoActivateListener();

        builder.Register<GameAutoListener>();
        builder.Register<GameBoostService>().SingleInstance();
        builder.Register<InstanceStartedEventAutoAutoListener>();
        builder.Register<InstanceStoppedEventAutoAutoListener>();
        builder.Register<ProcessAutoListener>();
        builder.Register<TimeAutoListener>();
        builder.Register<UserInactivityAutoListener>();
        builder.Register<WiFiAutoListener>();

        builder.Register<AIController>();
        builder.Register<DisplayBrightnessController>();
        builder.Register<GodModeController>();
        builder.Register<GodModeControllerV1>(true);
        builder.Register<GodModeControllerV2>(true);
        builder.Register<GPUHardwareManager>().As<IGPUHardwareManager>();
        builder.Register<GPUProcessManager>().As<IGPUProcessManager>();
        builder.Register<GPUController>();
        builder.Register<GPUOverclockController>();
        builder.Register<LampArrayController>();
        builder.Register<RGBKeyboardBacklightController>();
        builder.Register<KeyboardBacklightDetectionService>()
            .As<IKeyboardBacklightDetectionService>()
            .SingleInstance();
        builder.Register<SensorsController>();
        builder.Register<SensorsControllerV1>(true);
        builder.Register<SensorsControllerV2>(true);
        builder.Register<SensorsControllerV3>(true);
        builder.Register<SensorsControllerV4>(true);
        builder.Register<SensorsControllerV5>(true);
        builder.Register<AsusSensorsController>(true);
        builder.Register<HpSensorsController>(true);
        builder.Register<RazerSensorsController>(true);
        builder.Register<AlienwareSensorsController>(true);
        builder.Register<AcerSensorsController>(true);
        builder.Register<GigabyteSensorsController>(true);
        builder.Register<MsiSensorsController>(true);
        builder.Register<TongfangSensorsController>(true);
        builder.Register<ClevoSensorsController>(true);
        builder.Register<GenericSensorsController>(true);
        builder.Register<SensorsGroupController>(true);
        builder.Register<FpsSensorController>();
        builder.Register<SmartFnLockController>();
        builder.Register<SpectrumKeyboardBacklightController>();
        builder.Register<SpectrumScreenCapture>()
            .As<SpectrumKeyboardBacklightController.ISpectrumScreenCapture>()
            .SingleInstance();
        builder.Register<WindowsPowerModeController>();
        builder.Register<WindowsPowerPlanController>();

        builder.Register<UpdateChecker>();
        builder.Register<WarrantyChecker>();

        builder.Register<PackageDownloaderFactory>();
        builder.Register<PCSupportPackageDownloader>();
        builder.Register<VantagePackageDownloader>();

        builder.Register<HWiNFOIntegration>();

        builder.Register<SunriseSunset>();

        builder.Register<BatteryHealthAlertSettings>().SingleInstance();
        builder.Register<BatteryDischargeRateMonitorService>().SingleInstance();
        builder.Register<AmdOverclockingController>();
        builder.Register<FanCurveManager>(c => new FanCurveManager(
            c.Resolve<SensorsGroupController>(),
            c.Resolve<PowerModeListener>(),
            c.Resolve<PowerModeFeature>())).SingleInstance();
        builder.Register<WindowsCleanupService>();
        builder.Register<WindowsOptimizationService>();

        // Network acceleration (Phase 1 stubs — default off, no auto-start)
        builder.Register<NetworkAccelerationSettings>().SingleInstance();
        builder.Register<NetworkAccelerationService>().As<INetworkAccelerationService>().SingleInstance();
        builder.Register<NetworkDiagnosticsService>().As<INetworkDiagnosticsService>().SingleInstance();
        builder.Register<NetworkStateRecoveryService>().As<INetworkStateRecoveryService>().SingleInstance();
#else
        RegisterPlatformServices(builder);
#endif
    }

#if !WINDOWS
    /// <summary>
    /// Registers the platform backends (Platform.Linux / Platform.MacOS) behind
    /// the Lib.Abstractions interfaces. Implementations are referenced directly
    /// by the csproj (conditional ItemGroup); discovery by name keeps this module
    /// free of per-platform compile-time dependencies.
    /// </summary>
    private static void RegisterPlatformServices(ContainerBuilder builder)
    {
        var platform = OperatingSystem.IsLinux() ? "Linux"
            : OperatingSystem.IsMacOS() ? "MacOS"
            : null;

        if (platform is null)
        {
            Log.Instance.Warning("No platform backend available for this operating system.");
            return;
        }

        var assemblyName = $"UniversalDeviceToolkit.Platform.{platform}";
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(assemblyName);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Platform backend '{assemblyName}' could not be loaded.", ex);
            return;
        }

        var registrations = new (string TypeName, Type ServiceType)[]
        {
            ($"{platform}PlatformServices", typeof(IPlatformServices)),
            ($"{platform}DeviceAdapter", typeof(IDeviceAdapter)),
            ($"{platform}SensorBackend", typeof(ISensorBackend)),
            ($"{platform}PowerProfileProvider", typeof(IPowerProfileProvider)),
            ($"{platform}GpuBackend", typeof(IGpuBackend)),
            ($"{platform}AutorunManager", typeof(IAutorunManager)),
            ($"{platform}SingleInstanceManager", typeof(ISingleInstanceManager)),
            ($"{platform}ConfigurationStore", typeof(IConfigurationStore)),
        };

        foreach (var (typeName, serviceType) in registrations)
        {
            // Implementations live in different sub-namespaces per area
            // (e.g. UniversalDeviceToolkit.Platform.Linux.Hardware.LinuxSensorBackend),
            // so resolve by simple name within the platform assembly.
            var implementationType = assembly.GetTypes()
                .FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal) && !t.IsAbstract && !t.IsInterface);
            if (implementationType is null)
            {
                // Name-based discovery fails silently on a rename; warn loudly
                // so a missing backend service is visible in the default log.
                Log.Instance.Warning($"Platform backend type '{typeName}' not found in {assemblyName}; {serviceType.Name} stays unregistered.");
                continue;
            }

            builder.RegisterType(implementationType).As(serviceType).SingleInstance();
        }
    }
#endif
}
