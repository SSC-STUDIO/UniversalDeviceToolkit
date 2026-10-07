#if WINDOWS
using Autofac;
using UniversalDeviceToolkit.Host.Settings;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.GameDetection;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// The no-hardware host can read and save configuration without registering
/// drivers, controllers, input hooks or auto-activated hardware listeners.
/// </summary>
internal sealed class HardwareDisabledModule : Module
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
    }
}
#endif
