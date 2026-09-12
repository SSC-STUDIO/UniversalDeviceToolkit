#if !WINDOWS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Lifecycle;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Host;

namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Portable implementations of the Electron core RPC surface. Each handler
/// talks only to abstractions that already exist (IDeviceAdapter, ISensorBackend,
/// IGpuBackend, IAutorunManager, IConfigurationStore). Missing backends answer
/// <see cref="BridgeErrorCodes.PlatformNotSupported"/>; they never report
/// success for work that was not performed.
/// </summary>
internal static class PortableCoreHandlers
{
    private const string SettingsSection = "udt.settings";
    private const string DashboardScope = "dashboard";

    private static readonly string[] FeatureKeys =
    [
        "alwaysOnUsb",
        "battery",
        "batteryNightCharge",
        "flipToStart",
        "fnLock",
        "gSync",
        "hdr",
        "hybridMode",
        "igpuMode",
        "itsMode",
        "instantBoot",
        "microphone",
        "overDrive",
        "panelLogo",
        "portsBacklight",
        "powerMode",
        "refreshRate",
        "resolution",
        "dpiScale",
        "speaker",
        "touchpadLock",
        "whiteKeyboard",
        "winKey",
        "oneLevelWhiteKeyboard",
    ];

    private static readonly string[] SettingsScopes =
    [
        "application",
        "osd",
        "hardwareSensors",
        "balanceMode",
        "godMode",
        "gpuOverclock",
        "integrations",
        "lampArray",
        "fanCurves",
        "packageDownloader",
        "rgbKeyboard",
        "spectrumKeyboard",
        "sunriseSunset",
        "updateCheck",
        "networkAcceleration",
        "batteryHealthAlerts",
        "dashboard",
    ];

    private static readonly object SubscribeLock = new();
    private static Timer? _sensorTimer;
    private static BridgeRpcServer? _sensorRpc;
    private static int _sensorIntervalMs = 1000;
    private static bool _uiActivityHooked;

    public static void Register(BridgeRpcServer rpc)
    {
        if (!_uiActivityHooked)
        {
            _uiActivityHooked = true;
            HostUiActivity.Changed += static _ => ApplySensorTimer();
        }
        rpc.RegisterHandler("system.info", HandleSystemInfoAsync);
        rpc.RegisterHandler("system.powerAdapterStatus", HandlePowerAdapterStatusAsync);

        rpc.RegisterHandler("sensors.getStatus", HandleSensorsStatusAsync);
        rpc.RegisterHandler("sensors.getSnapshot", HandleSensorsSnapshotAsync);
        rpc.RegisterHandler("sensors.getDetailed", HandleSensorsSnapshotAsync);
        rpc.RegisterHandler("sensors.subscribe", (request, cancellationToken) =>
            HandleSensorsSubscribeAsync(request, rpc, cancellationToken));
        rpc.RegisterHandler("sensors.unsubscribe", HandleSensorsUnsubscribeAsync);
        rpc.RegisterHandler("sensors.getSettings", HandleSensorsGetSettingsAsync);
        rpc.RegisterHandler("sensors.setSettings", HandleSensorsSetSettingsAsync);

        rpc.RegisterHandler("settings.getAll", HandleSettingsGetAllAsync);
        rpc.RegisterHandler("settings.get", HandleSettingsGetAsync);
        rpc.RegisterHandler("settings.set", HandleSettingsSetAsync);
        rpc.RegisterHandler("settings.save", (request, _) => HandleSettingsSaveAsync(request, rpc));
        rpc.RegisterHandler("settings.reload", HandleSettingsReloadAsync);

        rpc.RegisterHandler("dashboard.getConfig", HandleDashboardGetConfigAsync);
        rpc.RegisterHandler("dashboard.saveConfig", HandleDashboardSaveConfigAsync);

        rpc.RegisterHandler("app.getAutorun", HandleGetAutorunAsync);
        rpc.RegisterHandler("app.setAutorun", HandleSetAutorunAsync);

        rpc.RegisterHandler("feature.list", HandleFeatureListAsync);
        rpc.RegisterHandler("feature.getSupported", HandleFeatureGetSupportedAsync);
        rpc.RegisterHandler("feature.getStates", HandleFeatureGetStatesAsync);
        rpc.RegisterHandler("feature.getState", HandleFeatureGetStateAsync);
        rpc.RegisterHandler("feature.setState", HandleFeatureSetStateAsync);
        rpc.RegisterHandler("feature.isHdrBlocked", HandleFeatureHdrBlockedAsync);
    }

    private static BridgeResult Missing(string backend) =>
        BridgeResult.Error(
            BridgeErrorCodes.PlatformNotSupported,
            $"{backend} is not available on this platform.");

    private static async Task<BridgeResult> HandleSystemInfoAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        var adapter = IoCContainer.TryResolve<IDeviceAdapter>();
        if (adapter is null)
            return Missing("Device identity");

        try
        {
            var snapshot = await adapter.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var identity = snapshot.Identity;
            var sensorBackend = IoCContainer.TryResolve<ISensorBackend>();
            var gpu = IoCContainer.TryResolve<IGpuBackend>();
            var readings = sensorBackend is { IsAvailable: true } ? sensorBackend.GetReadings() : Array.Empty<SensorReading>();
            var cpuName = FindCpuName(readings);
            var totalMb = FindValue(readings, "Total", "Memory");
            return BridgeResult.Ok(new
            {
                vendor = EmptyToNull(identity.Vendor),
                model = EmptyToNull(identity.Model) ?? EmptyToNull(identity.ProductName),
                machineType = EmptyToNull(identity.MachineType),
                biosVersion = EmptyToNull(identity.BiosVersion),
                serialNumber = EmptyToNull(identity.SerialNumber),
                architecture = EmptyToNull(identity.Architecture),
                platform = identity.Platform,
                source = snapshot.Source,
                isCompatible = true,
                hardware = new
                {
                    processor = string.IsNullOrWhiteSpace(cpuName)
                        ? null
                        : new
                        {
                            name = cpuName,
                            numberOfLogicalProcessors = Environment.ProcessorCount,
                        },
                    videoController = gpu is { IsAvailable: true } && !string.IsNullOrWhiteSpace(gpu.GetGpuName())
                        ? new { name = gpu.GetGpuName() }
                        : null,
                    memory = totalMb is > 0
                        ? new { totalCapacityBytes = (long)(totalMb.Value * 1024L * 1024L) }
                        : null,
                },
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandlePowerAdapterStatusAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        var adapter = IoCContainer.TryResolve<IDeviceAdapter>();
        if (adapter is null)
            return Missing("Power adapter status");

        try
        {
            var snapshot = await adapter.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(snapshot.PowerStatus))
                return Missing("Power adapter status");

            return BridgeResult.Ok(new { status = snapshot.PowerStatus });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Task<BridgeResult> HandleSensorsStatusAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        if (!TryGetSensorBackend(out var backend) || backend is null)
            return Task.FromResult(Missing("Sensors"));

        try
        {
            var gpu = IoCContainer.TryResolve<IGpuBackend>();
            var readings = backend.GetReadings();
            return Task.FromResult(BridgeResult.Ok(new
            {
                initialized = backend.IsAvailable,
                isHybrid = false,
                cpuName = FindCpuName(readings),
                gpuName = gpu is { IsAvailable: true } ? gpu.GetGpuName() : FindReadingName(readings, "GPU", "Temperature"),
                gpuIsIntegrated = false,
                initialState = backend.IsAvailable ? "Ready" : "Unavailable",
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static Task<BridgeResult> HandleSensorsSnapshotAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        if (!TryGetSensorBackend(out var backend) || backend is null)
            return Task.FromResult(Missing("Sensors"));

        try
        {
            return Task.FromResult(BridgeResult.Ok(BuildSensorSnapshot(backend)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static Task<BridgeResult> HandleSensorsSubscribeAsync(
        BridgeRequest request,
        BridgeRpcServer rpc,
        CancellationToken cancellationToken)
    {
        if (!TryGetSensorBackend(out _))
            return Task.FromResult(Missing("Sensors"));

        var intervalSec = 1.0;
        if (request.Parameters.ValueKind == JsonValueKind.Object &&
            request.Parameters.TryGetProperty("intervalSec", out var intervalProp) &&
            intervalProp.ValueKind == JsonValueKind.Number &&
            intervalProp.TryGetDouble(out var parsed) &&
            parsed > 0)
        {
            intervalSec = Math.Clamp(parsed, 0.25, 30);
        }

        var intervalMs = (int)Math.Round(intervalSec * 1000.0);
        lock (SubscribeLock)
        {
            _sensorRpc = rpc;
            _sensorIntervalMs = intervalMs;
            RestartSensorTimerLocked();
        }

        cancellationToken.Register(StopSensorTimer);
        return Task.FromResult(BridgeResult.Ok(new { subscribed = true, effectiveIntervalSec = intervalSec }));
    }

    private static Task<BridgeResult> HandleSensorsUnsubscribeAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        StopSensorTimer();
        return Task.FromResult(BridgeResult.Ok(new { unsubscribed = true }));
    }

    private static Task<BridgeResult> HandleSensorsGetSettingsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        var application = ReadScopeObject(store, "application");
        var osd = ReadScopeObject(store, "osd");
        var hardware = ReadScopeObject(store, "hardwareSensors");

        return Task.FromResult(BridgeResult.Ok(new
        {
            enableHardwareSensors = ReadBool(application, "EnableHardwareSensors", true),
            osdRefreshIntervalSec = ReadDouble(osd, "OsdRefreshInterval", 1),
            selectedGpuIsIgpu = ReadBool(hardware, "SelectedGpuIsIgpu", false),
            showCpuAverageFrequency = ReadBool(hardware, "ShowCpuAverageFrequency", false),
            displayMemoryInGigabytes = ReadBool(hardware, "DisplayMemoryInGigabytes", false),
            visibleSections = ReadStringArray(hardware, "VisibleSections", ["CPU", "Battery", "GPU"]),
            sectionOrder = ReadStringArray(hardware, "SectionOrder", ["CPU", "Battery", "GPU"]),
        }));
    }

    private static Task<BridgeResult> HandleSensorsSetSettingsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        if (request.Parameters.ValueKind != JsonValueKind.Object)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Expected a settings object."));

        var application = ReadScopeObject(store, "application");
        var osd = ReadScopeObject(store, "osd");
        var hardware = ReadScopeObject(store, "hardwareSensors");

        if (request.Parameters.TryGetProperty("enableHardwareSensors", out var enable) &&
            enable.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            application["EnableHardwareSensors"] = enable.GetBoolean();
        }

        if (request.Parameters.TryGetProperty("osdRefreshIntervalSec", out var osdInterval) &&
            osdInterval.ValueKind == JsonValueKind.Number &&
            osdInterval.TryGetDouble(out var osdValue))
        {
            osd["OsdRefreshInterval"] = osdValue;
        }

        CopyBool(request.Parameters, "selectedGpuIsIgpu", hardware, "SelectedGpuIsIgpu");
        CopyBool(request.Parameters, "showCpuAverageFrequency", hardware, "ShowCpuAverageFrequency");
        CopyBool(request.Parameters, "displayMemoryInGigabytes", hardware, "DisplayMemoryInGigabytes");
        CopyStringArray(request.Parameters, "visibleSections", hardware, "VisibleSections");
        CopyStringArray(request.Parameters, "sectionOrder", hardware, "SectionOrder");

        if (!TryWriteScope(store, "application", application) ||
            !TryWriteScope(store, "osd", osd) ||
            !TryWriteScope(store, "hardwareSensors", hardware))
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, "Failed to persist sensor settings."));
        }

        return Task.FromResult(BridgeResult.Ok(new { saved = true }));
    }

    private static Task<BridgeResult> HandleSettingsGetAllAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        var scopes = ReadScopeList(request);
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var scope in scopes)
        {
            if (!IsKnownScope(scope))
                return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown settings scope: {scope}"));
            result[scope] = ReadScopeObject(store, scope);
        }

        return Task.FromResult(BridgeResult.Ok(new { scopes = result }));
    }

    private static Task<BridgeResult> HandleSettingsGetAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        if (!TryGetString(request.Parameters, "scope", out var scope) || scope is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'scope'."));
        if (!IsKnownScope(scope))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown settings scope: {scope}"));

        JsonNode? node = ReadScopeObject(store, scope);
        if (TryGetString(request.Parameters, "path", out var path) && !string.IsNullOrWhiteSpace(path))
        {
            foreach (var segment in path.Split('.'))
            {
                if (node is not JsonObject obj || !obj.TryGetPropertyValue(segment, out node))
                    return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Path segment '{segment}' does not exist."));
            }
        }

        return Task.FromResult(BridgeResult.Ok(new { scope, value = node }));
    }

    private static Task<BridgeResult> HandleSettingsSetAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        if (!TryGetString(request.Parameters, "scope", out var scope) || scope is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'scope'."));
        if (!IsKnownScope(scope))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown settings scope: {scope}"));
        if (!request.Parameters.TryGetProperty("value", out var valueProp))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing 'value' parameter."));

        JsonObject merged;
        try
        {
            if (JsonNode.Parse(valueProp.GetRawText()) is not JsonObject parsed)
            {
                return Task.FromResult(BridgeResult.Error(
                    BridgeErrorCodes.InvalidParams,
                    "Settings value must be a JSON object."));
            }

            merged = parsed;
        }
        catch (JsonException ex)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Invalid settings value: {ex.Message}"));
        }

        if (!TryWriteScope(store, scope, merged))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, $"Failed to persist settings scope '{scope}'."));

        return Task.FromResult(BridgeResult.Ok(new { scope, applied = true }));
    }

    private static Task<BridgeResult> HandleSettingsSaveAsync(BridgeRequest request, BridgeRpcServer rpc)
    {
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        var scopes = ReadScopeList(request);
        var saved = new List<string>();
        foreach (var scope in scopes)
        {
            if (!IsKnownScope(scope))
                return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown settings scope: {scope}"));

            var current = ReadScopeObject(store, scope);
            if (!TryWriteScope(store, scope, current))
                return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, $"Failed to persist settings scope '{scope}'."));

            saved.Add(scope);
            rpc.Publish("settings.changed", new { scope, reason = "save" });
        }

        return Task.FromResult(BridgeResult.Ok(new { saved }));
    }

    private static Task<BridgeResult> HandleSettingsReloadAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        if (!TryGetString(request.Parameters, "scope", out var scope) || scope is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'scope'."));
        if (!IsKnownScope(scope))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown settings scope: {scope}"));

        _ = ReadScopeObject(store, scope);
        return Task.FromResult(BridgeResult.Ok(new { reloaded = true }));
    }

    private static Task<BridgeResult> HandleDashboardGetConfigAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        var node = ReadScopeObject(store, DashboardScope);
        return Task.FromResult(BridgeResult.Ok(node));
    }

    private static Task<BridgeResult> HandleDashboardSaveConfigAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var store = IoCContainer.TryResolve<IConfigurationStore>();
        if (store is null)
            return Task.FromResult(Missing("Configuration"));

        if (!request.Parameters.TryGetProperty("config", out var configProp))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing 'config' parameter."));

        JsonObject config;
        try
        {
            config = JsonNode.Parse(configProp.GetRawText()) as JsonObject
                ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Invalid dashboard config: {ex.Message}"));
        }

        NormalizeDashboardConfig(config);
        if (!TryWriteScope(store, DashboardScope, config))
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InternalError, "Failed to persist dashboard config."));

        return Task.FromResult(BridgeResult.Ok(new { saved = true }));
    }

    private static async Task<BridgeResult> HandleGetAutorunAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var manager = IoCContainer.TryResolve<IAutorunManager>();
        if (manager is null)
            return Missing("Autorun");

        try
        {
            var enabled = await manager.IsEnabledAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { state = enabled ? "Enabled" : "Disabled" });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleSetAutorunAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var manager = IoCContainer.TryResolve<IAutorunManager>();
        if (manager is null)
            return Missing("Autorun");

        if (!TryGetString(request.Parameters, "state", out var state) || state is null)
            return BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'state'.");

        if (string.Equals(state, "EnabledDelayed", StringComparison.OrdinalIgnoreCase))
        {
            return BridgeResult.Error(
                BridgeErrorCodes.InvalidParams,
                "Delayed autorun is not supported on this platform.");
        }

        var enable = string.Equals(state, "Enabled", StringComparison.OrdinalIgnoreCase);
        var disable = string.Equals(state, "Disabled", StringComparison.OrdinalIgnoreCase);
        if (!enable && !disable)
            return BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown AutorunState '{state}'.");

        try
        {
            if (enable)
                await manager.EnableAsync().ConfigureAwait(false);
            else
                await manager.DisableAsync().ConfigureAwait(false);

            var actuallyEnabled = await manager.IsEnabledAsync().ConfigureAwait(false);
            if (actuallyEnabled != enable)
            {
                return BridgeResult.Error(
                    BridgeErrorCodes.InternalError,
                    "Autorun change did not persist on this platform.");
            }

            return BridgeResult.Ok(new { ok = true, state = actuallyEnabled ? "Enabled" : "Disabled" });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(BridgeErrorCodes.InternalError, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Task<BridgeResult> HandleFeatureListAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        var features = FeatureKeys.Select(key =>
        {
            var supported = PortableFeatureSupport.IsSupported(key);
            return new
            {
                key,
                supported,
                stateType = supported ? PortableFeatureSupport.StateType(key) : "Unsupported",
            };
        }).ToArray();
        return Task.FromResult(BridgeResult.Ok(new { features }));
    }

    private static Task<BridgeResult> HandleFeatureGetSupportedAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!TryGetString(request.Parameters, "feature", out var feature) || feature is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'feature'."));
        if (Array.IndexOf(FeatureKeys, feature) < 0)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown feature '{feature}'."));
        return Task.FromResult(BridgeResult.Ok(new { supported = PortableFeatureSupport.IsSupported(feature) }));
    }

    private static Task<BridgeResult> HandleFeatureGetStatesAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetString(request.Parameters, "feature", out var feature) || feature is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'feature'."));
        if (Array.IndexOf(FeatureKeys, feature) < 0)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown feature '{feature}'."));
        return PortableFeatureSupport.GetStatesAsync(feature, cancellationToken);
    }

    private static Task<BridgeResult> HandleFeatureGetStateAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetString(request.Parameters, "feature", out var feature) || feature is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'feature'."));
        if (Array.IndexOf(FeatureKeys, feature) < 0)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown feature '{feature}'."));
        return PortableFeatureSupport.GetStateAsync(feature, cancellationToken);
    }

    private static Task<BridgeResult> HandleFeatureSetStateAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetString(request.Parameters, "feature", out var feature) || feature is null)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, "Missing string parameter 'feature'."));
        if (Array.IndexOf(FeatureKeys, feature) < 0)
            return Task.FromResult(BridgeResult.Error(BridgeErrorCodes.InvalidParams, $"Unknown feature '{feature}'."));
        return PortableFeatureSupport.SetStateAsync(feature, request.Parameters, cancellationToken);
    }

    private static Task<BridgeResult> HandleFeatureHdrBlockedAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(BridgeResult.Ok(new { blocked = false }));
    }

    private static bool TryGetSensorBackend(out ISensorBackend? backend)
    {
        backend = IoCContainer.TryResolve<ISensorBackend>();
        return backend is { IsAvailable: true };
    }

    private static object BuildSensorSnapshot(ISensorBackend backend)
    {
        var readings = backend.GetReadings();
        var gpu = IoCContainer.TryResolve<IGpuBackend>();
        var cpuTemp = FindValue(readings, "Temperature", "CPU");
        var cpuUsage = FindValue(readings, "Usage", "CPU");
        var cpuFan = FindValue(readings, "Fan", "CPU") ?? FindValue(readings, "Fan", null);
        var cpuPower = FindValue(readings, "Power", "CPU");
        var cpuVoltage = FindValue(readings, "Voltage", "CPU");
        var cpuClockMax = FindValue(readings, "Frequency", "CPU");
        var cpuClockAvg = FindValue(readings, "FrequencyAvg", "CPU");
        var memoryUsage = FindValue(readings, "Usage", "Memory");
        var memoryUsed = FindValue(readings, "Used", "Memory");
        var memoryTotal = FindValue(readings, "Total", "Memory");
        var gpuTemp = gpu is { IsAvailable: true } ? (double?)gpu.GetTemperatureCelsius() : FindValue(readings, "Temperature", "GPU");
        var gpuUsage = gpu is { IsAvailable: true } ? (double?)gpu.GetUsagePercent() : FindValue(readings, "Usage", "GPU");
        var gpuClock = gpu is { IsAvailable: true } ? (double?)gpu.GetCurrentClockMhz() : null;
        var gpuVramUsed = gpu is { IsAvailable: true } ? (double?)gpu.GetMemoryUsedMb() : null;
        var gpuVramTotal = gpu is { IsAvailable: true } ? (double?)gpu.GetMemoryTotalMb() : null;
        var gpuPower = FindValue(readings, "Power", "GPU");
        var gpuFan = FindValue(readings, "Fan", "GPU");
        var storageTemps = readings
            .Where(reading => string.Equals(reading.Category, "Temperature", StringComparison.OrdinalIgnoreCase)
                && string.Equals(reading.Name, "Storage", StringComparison.OrdinalIgnoreCase))
            .Select(reading => (double?)reading.Value)
            .ToArray();

        return new
        {
            ts = DateTime.UtcNow,
            source = "platform",
            initialized = true,
            isHybrid = false,
            info = new
            {
                cpuName = FindCpuName(readings),
                gpuName = gpu is { IsAvailable: true } ? gpu.GetGpuName() : FindReadingName(readings, "GPU", "Temperature"),
                gpuIsIntegrated = false,
            },
            cpu = new
            {
                temperature = cpuTemp,
                usage = cpuUsage,
                fanSpeed = cpuFan,
                power = cpuPower,
                voltage = cpuVoltage,
                coreClockMax = cpuClockMax,
                coreClockAvg = cpuClockAvg,
            },
            gpu = new
            {
                usage = gpuUsage,
                temperature = gpuTemp,
                coreClock = gpuClock,
                memoryClock = (double?)null,
                power = gpuPower,
                vramUsedMb = gpuVramUsed,
                vramTotalMb = gpuVramTotal,
                fanSpeed = gpuFan,
            },
            memory = new
            {
                usage = memoryUsage,
                usedMb = memoryUsed,
                totalMb = memoryTotal,
                highestTemperature = FindValue(readings, "Temperature", "Memory"),
            },
            battery = BuildBattery(readings),
            motherboard = new { highestTemperature = FindValue(readings, "Temperature", "Motherboard") },
            storage = new { temperatures = storageTemps },
        };
    }

    private static object? BuildBattery(IReadOnlyList<SensorReading> readings)
    {
        var charge = FindValue(readings, "Charge", "Battery");
        var health = FindValue(readings, "Health", "Battery");
        var temperature = FindValue(readings, "Temperature", "Battery");
        var power = FindValue(readings, "Power", "Battery");
        var voltage = FindValue(readings, "Voltage", "Battery");
        var design = FindValue(readings, "DesignCapacity", "Battery");
        var full = FindValue(readings, "FullChargeCapacity", "Battery");
        var cycles = FindValue(readings, "CycleCount", "Battery");
        var charging = FindValue(readings, "Charging", "Battery");
        var model = FindReadingName(readings, nameContains: null, category: "BatteryIdentity");
        if (charge is null && health is null && power is null && design is null)
            return null;

        return new
        {
            chargeLevel = charge is null ? null : (int?)Math.Round(charge.Value),
            health = health is null ? null : health / 100.0,
            temperature,
            avgTemperature = (double?)null,
            chargeRate = power,
            minDischargeRate = (int?)null,
            maxDischargeRate = (int?)null,
            voltage,
            designCapacity = design is null ? null : (int?)Math.Round(design.Value),
            fullChargeCapacity = full is null ? null : (int?)Math.Round(full.Value),
            cycleCount = cycles is null ? null : (int?)Math.Round(cycles.Value),
            manufactureDate = (string?)null,
            firstUseDate = (string?)null,
            isCharging = charging is > 0,
            isLowBattery = charge is < 20,
            isLowPowerAdapter = false,
            modelName = model,
        };
    }

    private static void ApplySensorTimer()
    {
        lock (SubscribeLock)
            RestartSensorTimerLocked();
    }

    private static void RestartSensorTimerLocked()
    {
        _sensorTimer?.Dispose();
        _sensorTimer = null;
        if (_sensorRpc is null || !HostUiActivity.IsActive)
            return;
        _sensorTimer = new Timer(static _ => PublishSensorSnapshot(), null, 0, _sensorIntervalMs);
    }

    private static void PublishSensorSnapshot()
    {
        ISensorBackend? backend;
        BridgeRpcServer? rpc;
        lock (SubscribeLock)
        {
            if (!HostUiActivity.IsActive)
                return;
            rpc = _sensorRpc;
            backend = IoCContainer.TryResolve<ISensorBackend>();
        }

        if (rpc is null || backend is not { IsAvailable: true })
            return;

        try
        {
            rpc.Publish("sensors.updated", BuildSensorSnapshot(backend));
        }
        catch (Exception)
        {
            // Subscriber teardown races are ignored.
        }
    }

    private static void StopSensorTimer()
    {
        lock (SubscribeLock)
        {
            _sensorTimer?.Dispose();
            _sensorTimer = null;
            _sensorRpc = null;
        }
    }

    private static double? FindValue(IReadOnlyList<SensorReading> readings, string category, string? nameContains)
    {
        foreach (var reading in readings)
        {
            if (!string.Equals(reading.Category, category, StringComparison.OrdinalIgnoreCase))
                continue;
            if (nameContains is not null &&
                reading.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            return reading.Value;
        }

        return null;
    }

    private static string? FindCpuName(IReadOnlyList<SensorReading> readings) =>
        FindReadingName(readings, nameContains: null, category: "Identity")
        ?? FindReadingName(readings, "CPU", "Usage")
        ?? FindReadingName(readings, "CPU", "Temperature");

    private static string? FindReadingName(IReadOnlyList<SensorReading> readings, string? nameContains, string? category)
    {
        foreach (var reading in readings)
        {
            if (category is not null &&
                !string.Equals(reading.Category, category, StringComparison.OrdinalIgnoreCase))
                continue;
            if (nameContains is not null &&
                reading.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (string.IsNullOrWhiteSpace(reading.Name))
                continue;
            return reading.Name;
        }

        return null;
    }

    private static List<string> ReadScopeList(BridgeRequest request)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Object &&
            request.Parameters.TryGetProperty("scopes", out var scopesProp) &&
            scopesProp.ValueKind == JsonValueKind.Array)
        {
            return scopesProp.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
        }

        return SettingsScopes.ToList();
    }

    private static bool IsKnownScope(string scope) =>
        Array.IndexOf(SettingsScopes, scope) >= 0;

    private static JsonObject ReadScopeObject(IConfigurationStore store, string scope)
    {
        var json = store.GetValue(SettingsSection, scope);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonNode.Parse(json) is JsonObject parsed)
                    return parsed;
            }
            catch (JsonException)
            {
                // Fall through to defaults.
            }
        }

        return CreateDefaultScope(scope);
    }

    private static bool TryWriteScope(IConfigurationStore store, string scope, JsonObject value)
    {
        var json = value.ToJsonString();
        store.SetValue(SettingsSection, scope, json);
        var roundTrip = store.GetValue(SettingsSection, scope);
        return string.Equals(roundTrip, json, StringComparison.Ordinal);
    }

    private static JsonObject CreateDefaultScope(string scope)
    {
        return scope switch
        {
            "application" => new JsonObject
            {
                ["Theme"] = "System",
                ["ThemeStylePreset"] = "Default",
                ["AccentColorSource"] = "Custom",
                ["ApplyAccentColorToSystem"] = false,
                ["ApplyAccentColorToTheme"] = true,
                ["WindowBackdropStyle"] = "Windows",
                ["AppFontStyle"] = "Default",
                ["AppTextSize"] = "Standard",
                ["AppScale"] = "Standard",
                ["MinimizeToTray"] = true,
                ["MinimizeOnClose"] = false,
                ["DontShowNotifications"] = false,
                ["NotificationPosition"] = "BottomRight",
                ["NotificationDuration"] = "Normal",
                ["AnimationsEnabled"] = true,
                ["AnimationSpeed"] = 2.0,
                ["NavigationPaneExpanded"] = true,
                ["TemperatureUnit"] = "C",
                ["EnableHardwareSensors"] = true,
                ["ForceSoftwareRendering"] = false,
                ["Notifications"] = new JsonObject
                {
                    ["UpdateAvailable"] = true,
                    ["SuccessNotifications"] = true,
                    ["NotificationSound"] = false,
                },
                ["NavigationItemsVisibility"] = new JsonObject
                {
                    ["keyboard"] = true,
                    ["battery"] = true,
                    ["automation"] = true,
                    ["macro"] = true,
                    ["windowsOptimization"] = true,
                    ["about"] = true,
                },
            },
            "osd" => new JsonObject
            {
                ["ShowOsd"] = false,
                ["OsdRefreshInterval"] = 1,
            },
            "hardwareSensors" => new JsonObject
            {
                ["SelectedGpuIsIgpu"] = false,
                ["ShowCpuAverageFrequency"] = false,
                ["DisplayMemoryInGigabytes"] = false,
                ["VisibleSections"] = new JsonArray(
                    JsonValue.Create("CPU"),
                    JsonValue.Create("Battery"),
                    JsonValue.Create("GPU")),
                ["SectionOrder"] = new JsonArray(
                    JsonValue.Create("CPU"),
                    JsonValue.Create("Battery"),
                    JsonValue.Create("GPU")),
            },
            "dashboard" => new JsonObject
            {
                ["showSensors"] = true,
                ["sensorsRefreshIntervalSeconds"] = 1,
                ["groups"] = new JsonArray(),
            },
            "updateCheck" => new JsonObject
            {
                ["LastUpdateCheckDateTime"] = null,
                ["UpdateCheckFrequency"] = "PerDay",
                ["IncludePrereleaseUpdates"] = false,
                ["UpdateRepositoryOwner"] = null,
                ["UpdateRepositoryName"] = null,
            },
            _ => new JsonObject(),
        };
    }

    private static void NormalizeDashboardConfig(JsonObject config)
    {
        if (!config.ContainsKey("showSensors"))
            config["showSensors"] = true;
        if (!config.ContainsKey("sensorsRefreshIntervalSeconds"))
            config["sensorsRefreshIntervalSeconds"] = 1;
        if (config["groups"] is not JsonArray)
            config["groups"] = new JsonArray();
    }

    private static bool ReadBool(JsonObject obj, string key, bool fallback)
        => obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<bool>(out var parsed)
            ? parsed
            : fallback;

    private static double ReadDouble(JsonObject obj, string key, double fallback)
        => obj.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<double>(out var parsed)
            ? parsed
            : fallback;

    private static string[] ReadStringArray(JsonObject obj, string key, string[] fallback)
    {
        if (obj.TryGetPropertyValue(key, out var node) && node is JsonArray array)
        {
            return array
                .Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray();
        }

        return fallback;
    }

    private static void CopyBool(JsonElement source, string sourceKey, JsonObject target, string targetKey)
    {
        if (source.TryGetProperty(sourceKey, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
            target[targetKey] = prop.GetBoolean();
    }

    private static void CopyStringArray(JsonElement source, string sourceKey, JsonObject target, string targetKey)
    {
        if (!source.TryGetProperty(sourceKey, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return;

        var array = new JsonArray();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
                array.Add(JsonValue.Create(text));
        }

        target[targetKey] = array;
    }

    private static bool TryGetString(JsonElement parameters, string name, out string? value)
    {
        value = null;
        if (parameters.ValueKind != JsonValueKind.Object)
            return false;
        if (!parameters.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        value = prop.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
#endif
