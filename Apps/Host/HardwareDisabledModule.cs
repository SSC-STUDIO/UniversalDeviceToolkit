#if WINDOWS
using Autofac;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Abstractions.Utils;
using UniversalDeviceToolkit.Host.Settings;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.CLI;
using UniversalDeviceToolkit.Lib.Automation.Optimization;
using UniversalDeviceToolkit.Lib.Automation.Settings;
using UniversalDeviceToolkit.Lib.Features.CursorPointer;
using UniversalDeviceToolkit.Lib.GameDetection;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Notifications;
using UniversalDeviceToolkit.Lib.Optimization;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// The no-hardware host never registers vendor hardware or its listeners.
/// Diagnostics only expose configuration; basic mode retains system tools.
/// </summary>
internal sealed class HardwareDisabledModule(bool allowSystemTools = false) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<HttpClientFactory>().SingleInstance();
        builder.RegisterType<HeadlessMainThreadDispatcher>().As<IMainThreadDispatcher>().SingleInstance();
        builder.RegisterType<OsdSettings>().SingleInstance();
        builder.RegisterType<HardwareSensorSettings>().SingleInstance();
        builder.RegisterType<BalanceModeSettings>().SingleInstance();
        builder.RegisterType<GodModeSettings>().SingleInstance();
        builder.RegisterType<GPUOverclockSettings>().SingleInstance();
        builder.RegisterType<IntegrationsSettings>().SingleInstance();
        builder.RegisterType<LampArraySettings>().SingleInstance();
        builder.RegisterType<FanCurveSettings>().SingleInstance();
        builder.RegisterType<PackageDownloaderSettings>().SingleInstance();
        builder.RegisterType<RGBKeyboardSettings>().SingleInstance();
        builder.RegisterType<SpectrumKeyboardSettings>().SingleInstance();
        builder.RegisterType<SunriseSunsetSettings>().SingleInstance();
        builder.RegisterType<UpdateCheckSettings>().SingleInstance();
        builder.RegisterType<NetworkAccelerationSettings>().SingleInstance();
        builder.RegisterType<BatteryHealthAlertSettings>().SingleInstance();
        builder.RegisterType<HostDashboardSettings>().SingleInstance();
        builder.RegisterType<GameBoostSettings>().SingleInstance();

        if (!allowSystemTools)
            return;

        builder.RegisterType<DefaultDelayProvider>().As<IDelayProvider>().SingleInstance();
        builder.RegisterType<WindowsCleanupService>().SingleInstance();
        builder.RegisterType<WindowsOptimizationService>().SingleInstance();
        builder.RegisterModule(new WindowsOptimizationElevationIoCModule());
        builder.RegisterType<NetworkAccelerationService>().As<INetworkAccelerationService>().SingleInstance()
            .OnRelease(service => service.DisposeAsync().AsTask().GetAwaiter().GetResult());
        builder.RegisterType<NetworkDiagnosticsService>().As<INetworkDiagnosticsService>().SingleInstance();
        builder.RegisterType<NetworkStateRecoveryService>().As<INetworkStateRecoveryService>().SingleInstance();
        builder.RegisterType<PCSupportPackageDownloader>().SingleInstance();
        builder.RegisterType<VantagePackageDownloader>().SingleInstance();
        builder.RegisterType<PackageDownloaderFactory>().SingleInstance();
        builder.RegisterType<UpdateChecker>().SingleInstance();
        builder.RegisterType<AppNotificationService>().As<IAppNotificationService>().SingleInstance();
        builder.RegisterType<CursorPointerService>().SingleInstance();
        builder.RegisterModule(new UniversalDeviceToolkit.Lib.Macro.IoCModule());
        builder.RegisterType<AutomationSettings>().SingleInstance();
        builder.RegisterType<SunriseSunset>().SingleInstance();
        builder.RegisterType<GameAutoListener>().SingleInstance();
        builder.RegisterType<InstanceStartedEventAutoAutoListener>().SingleInstance();
        builder.RegisterType<InstanceStoppedEventAutoAutoListener>().SingleInstance();
        builder.RegisterType<ProcessAutoListener>().SingleInstance();
        builder.RegisterType<SessionLockUnlockListener>().SingleInstance();
        builder.RegisterType<TimeAutoListener>().SingleInstance();
        builder.RegisterType<UserInactivityAutoListener>().SingleInstance();
        builder.RegisterType<WiFiAutoListener>().SingleInstance();
        builder.Register(context => new AutomationProcessor(
            context.Resolve<AutomationSettings>(), null, null, null, null, null,
            context.Resolve<GameAutoListener>(), context.Resolve<ProcessAutoListener>(),
            context.Resolve<SessionLockUnlockListener>(), context.Resolve<TimeAutoListener>(),
            context.Resolve<UserInactivityAutoListener>(), context.Resolve<WiFiAutoListener>(),
            hardwareEnabled: false)).SingleInstance();
        builder.Register(context => new IpcServer(
            context.Resolve<AutomationProcessor>(), null, null, context.Resolve<IntegrationsSettings>(),
            context.Resolve<UpdateChecker>(), context.Resolve<UpdateCheckSettings>(),
            context.Resolve<INetworkAccelerationService>(), context.Resolve<INetworkDiagnosticsService>(),
            context.Resolve<INetworkStateRecoveryService>()))
            .AsSelf().As<ICliHostLifecycle>().SingleInstance();
    }
}
#endif
