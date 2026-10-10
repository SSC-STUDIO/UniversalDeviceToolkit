using Autofac;
#if WINDOWS
using UniversalDeviceToolkit.Host.Settings;
using UniversalDeviceToolkit.Lib.Automation.Optimization;
using UniversalDeviceToolkit.Lib.Controllers;
#endif
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Host-specific Autofac registrations: bridge infrastructure that the shared
/// Lib modules do not know about.
/// </summary>
public class BridgeModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<HeadlessMainThreadDispatcher>()
            .As<IMainThreadDispatcher>()
            .SingleInstance();

#if WINDOWS
        builder.RegisterType<HostDashboardSettings>()
            .SingleInstance();

        // UAC elevation channel for optimization mutations (Lib.Automation):
        // WindowsOptimizationElevationClient starts an elevated worker over a
        // private named pipe when the bridge host is un-elevated.
        builder.RegisterModule(new WindowsOptimizationElevationIoCModule());

        // AIController must be shared so the background initializer and the
        // ai.* RPC handlers drive the same controller instance.
        builder.RegisterType<AIController>()
            .AsSelf()
            .SingleInstance();
#endif
    }
}
