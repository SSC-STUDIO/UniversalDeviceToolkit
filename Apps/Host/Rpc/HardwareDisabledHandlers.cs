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

    private static readonly string[] SystemMethods = RpcMethodNames.PortableCapable.Concat(RpcMethodNames.WindowsOnly)
        .Where(method => method.StartsWith("optimization.", StringComparison.Ordinal)
            || method.StartsWith("cleanup.", StringComparison.Ordinal)
            || method.StartsWith("network.", StringComparison.Ordinal)
            || method.StartsWith("driver.", StringComparison.Ordinal)
            || method.StartsWith("mouse.", StringComparison.Ordinal)
            || method.StartsWith("app.update.", StringComparison.Ordinal)
            || method.StartsWith("automation.", StringComparison.Ordinal)
            || method is "app.getAutorun" or "app.setAutorun")
        .Concat(MacroMethods).Distinct(StringComparer.Ordinal).ToArray();

    public static void Register(BridgeRpcServer rpc, bool allowSystemTools = false)
    {
        foreach (var method in RpcMethodNames.PortableCapable.Concat(RpcMethodNames.WindowsOnly).Concat(MacroMethods))
            rpc.RegisterHandler(method, HandleRequestAsync);

        SettingsHandlers.Register(rpc, applyRuntimeChanges: allowSystemTools);
        DashboardHandlers.Register(rpc);
        if (allowSystemTools)
        {
            OptimizationHandlers.Register(rpc);
            CleanupHandlers.Register(rpc);
            NetworkAccelerationHandlers.Register(rpc);
            DriverDownloadHandlers.Register(rpc);
            StartupHandlers.Register(rpc);
            AppIntegrationHandlers.Register(rpc);
            MouseHandlers.Register(rpc);
            MacroHandlers.Register(rpc);
            AutomationHandlers.Register(rpc);
        }
        rpc.RegisterHandler("host.getCapabilities", (_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(BridgeResult.Ok(BuildCapabilities(allowSystemTools)));
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
            "sensors.unsubscribe" => BridgeResult.Ok(new { unsubscribed = true }),
            "sensors.unsubscribeFps" => BridgeResult.Ok(new { monitoring = false }),
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

    internal static object BuildCapabilities(bool allowSystemTools = false)
    {
        var implementedMethods = RpcMethodNames.AlwaysOn.Concat(ConfigurationMethods).Concat(ReadOnlyMethods)
            .Concat(allowSystemTools ? SystemMethods : []).ToArray();
        var unsupportedMethods = RpcMethodNames.PortableCapable.Concat(RpcMethodNames.WindowsOnly)
            .Concat(MacroMethods).Except(implementedMethods, StringComparer.Ordinal).ToArray();
        return new
        {
            platform = "windows",
            portable = false,
            executionMode = allowSystemTools ? "basic" : "diagnostic",
            vendorHardware = false,
            capabilities = new
            {
                settings = true, dashboard = true, systemInfo = true,
                sensors = false, sensorsWrite = false, autorun = allowSystemTools, features = false,
                automation = allowSystemTools, optimization = allowSystemTools, godMode = false, keyboard = false,
                rgb = false, spectrum = false, bootLogo = false, network = allowSystemTools, ai = false,
                driver = allowSystemTools, cleanup = allowSystemTools, macro = allowSystemTools, updates = allowSystemTools, fps = false,
                accentColor = false, gpuManagement = false, fanControl = false,
                keyboardBacklight = false, batteryManagement = false, displayControl = false,
                powerProfile = false, systemTelemetry = false,
            },
            backends = new
            {
                platformServices = false, deviceAdapter = false, sensorBackend = false,
                gpuBackend = false, powerProfile = false, autorun = allowSystemTools, configuration = true,
            },
            device = (object?)null,
            implementedMethods,
            unsupportedMethods,
        };
    }
}
#endif
