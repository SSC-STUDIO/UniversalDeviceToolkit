#if WINDOWS
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Host.Rpc.Handlers;

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Explicit no-hardware RPC surface. Configuration remains usable; hardware
/// probes and mutations never reach the production Windows handler registry.
/// </summary>
internal static class HardwareDisabledHandlers
{
    private static readonly string[] MacroMethods =
    [
        "macro.getState", "macro.setEnabled", "macro.play", "macro.startRecording",
        "macro.stopRecording", "macro.saveSequence", "macro.clearSequence",
    ];

    private static readonly string[] ConfigurationMethods =
    [
        "settings.getAll", "settings.get", "settings.set", "settings.save", "settings.reload",
        "dashboard.getConfig", "dashboard.saveConfig",
    ];

    private static readonly string[] ReadOnlyMethods =
    [
        "system.info", "sensors.getStatus", "sensors.getSnapshot", "sensors.getDetailed",
        "sensors.getSettings", "sensors.unsubscribe", "sensors.unsubscribeFps", "feature.list",
        "feature.getSupported", "feature.getStates", "feature.isHdrBlocked",
        "dashboardHardware.getState", "keyboard.detect", "rgb.isSupported", "spectrum.isSupported",
    ];

    public static void Register(BridgeRpcServer rpc)
    {
        foreach (var method in RpcMethodNames.PortableCapable.Concat(RpcMethodNames.WindowsOnly).Concat(MacroMethods))
            rpc.RegisterHandler(method, HandleRequestAsync);

        SettingsHandlers.Register(rpc, applyRuntimeChanges: false);
        DashboardHandlers.Register(rpc);
        rpc.RegisterHandler("host.getCapabilities", (_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BridgeResult.Ok(BuildCapabilities()));
        });
    }

    internal static Task<BridgeResult> HandleRequestAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = request.Method switch
        {
            "system.info" => BridgeResult.Ok(new
            {
                platform = "windows",
                architecture = RuntimeInformation.OSArchitecture.ToString(),
                isCompatible = false,
                source = "hardware-disabled",
                hardware = (object?)null,
            }),
            "sensors.getStatus" => BridgeResult.Ok(new
            {
                initialized = false,
                isHybrid = false,
                cpuName = (string?)null,
                gpuName = (string?)null,
                gpuIsIntegrated = false,
                initialState = "Unavailable",
            }),
            "sensors.getSnapshot" or "sensors.getDetailed" => BridgeResult.Ok(SensorsHandlers.CreateDisabledSnapshot()),
            "sensors.getSettings" => null,
            "sensors.unsubscribe" or "sensors.unsubscribeFps" => BridgeResult.Ok(new { unsubscribed = true }),
            "feature.list" => BridgeResult.Ok(new { features = Array.Empty<object>() }),
            "feature.getSupported" or "rgb.isSupported" or "spectrum.isSupported" => BridgeResult.Ok(new { supported = false }),
            "feature.getStates" => BridgeResult.Ok(new { states = Array.Empty<object>() }),
            "feature.isHdrBlocked" => BridgeResult.Ok(new { blocked = false }),
            "keyboard.detect" => BridgeResult.Ok(new { mode = "none" }),
            "dashboardHardware.getState" => BridgeResult.Ok(new
            {
                discreteGpu = new { supported = false, state = "Unknown", performanceState = (int?)null, processes = Array.Empty<string>() },
                overclockDiscreteGpu = new
                {
                    supported = false, enabled = false, coreDeltaMhz = 0, memoryDeltaMhz = 0,
                    maxCoreDeltaMhz = 0, maxMemoryDeltaMhz = 0,
                },
                turnOffMonitors = new { supported = false },
            }),
            _ => BridgeResult.Error(BridgeErrorCodes.PlatformNotSupported, "Hardware and system actions are disabled for this host session."),
        };
        return result is null
            ? SensorsHandlers.HandleGetSettingsAsync(cancellationToken)
            : Task.FromResult(result);
    }

    internal static object BuildCapabilities()
    {
        var implementedMethods = RpcMethodNames.AlwaysOn.Concat(ConfigurationMethods).Concat(ReadOnlyMethods).ToArray();
        var unsupportedMethods = RpcMethodNames.PortableCapable.Concat(RpcMethodNames.WindowsOnly)
            .Except(implementedMethods, StringComparer.Ordinal).Concat(MacroMethods).ToArray();
        return new
        {
            platform = "windows",
            portable = false,
            vendorHardware = false,
            capabilities = new
            {
                settings = true, dashboard = true, systemInfo = true,
                sensors = false, sensorsWrite = false, autorun = false, features = false,
                automation = false, optimization = false, godMode = false, keyboard = false,
                rgb = false, spectrum = false, bootLogo = false, network = false, ai = false,
                driver = false, cleanup = false, macro = false, updates = false, fps = false,
                accentColor = false, gpuManagement = false, fanControl = false,
                keyboardBacklight = false, batteryManagement = false, displayControl = false,
                powerProfile = false, systemTelemetry = false,
            },
            backends = new
            {
                platformServices = false, deviceAdapter = false, sensorBackend = false,
                gpuBackend = false, powerProfile = false, autorun = false, configuration = true,
            },
            device = (object?)null,
            implementedMethods,
            unsupportedMethods,
        };
    }
}
#endif
