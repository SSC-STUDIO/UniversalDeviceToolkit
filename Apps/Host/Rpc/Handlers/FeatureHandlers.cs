using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.FlipToStart;
using UniversalDeviceToolkit.Lib.Features.Hybrid;
using UniversalDeviceToolkit.Lib.Features.InstantBoot;
using UniversalDeviceToolkit.Lib.Features.OverDrive;
using UniversalDeviceToolkit.Lib.Features.PanelLogo;
using UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Generic IFeature&lt;T&gt; bridge: capability probe, state list/current state and
/// state set for the 24 dashboard features. Enum states are encoded as enum
/// names (e.g. "Quiet"); struct states (RefreshRate/Resolution/DpiScale) are
/// encoded as objects with PascalCase property names.
/// </summary>
public static class FeatureHandlers
{
    private const int NotSupported = BridgeErrorCodes.FeatureNotSupported;
    private const int AcRequired = BridgeErrorCodes.AcPowerRequired;
    private const int UndefinedState = BridgeErrorCodes.UndefinedState;

    private static readonly Dictionary<string, IFeatureEntry> Features = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void Register(BridgeRpcServer rpc)
    {
        RegisterFeature("alwaysOnUsb", IoCContainer.Resolve<AlwaysOnUSBFeature>());
        RegisterFeature("battery", IoCContainer.Resolve<BatteryFeature>());
        RegisterFeature("batteryNightCharge", IoCContainer.Resolve<BatteryNightChargeFeature>());
        RegisterFeature("flipToStart", IoCContainer.Resolve<FlipToStartFeature>());
        RegisterFeature("fnLock", IoCContainer.Resolve<FnLockFeature>());
        RegisterFeature("gSync", IoCContainer.Resolve<GSyncFeature>());
        RegisterFeature("hdr", IoCContainer.Resolve<HDRFeature>());
        RegisterFeature("hybridMode", IoCContainer.Resolve<HybridModeFeature>());
        RegisterFeature("igpuMode", IoCContainer.Resolve<IGPUModeFeature>());
        RegisterFeature("itsMode", IoCContainer.Resolve<ITSModeFeature>());
        RegisterFeature("instantBoot", IoCContainer.Resolve<InstantBootFeature>());
        RegisterFeature("microphone", IoCContainer.Resolve<MicrophoneFeature>());
        RegisterFeature("overDrive", IoCContainer.Resolve<OverDriveFeature>());
        RegisterFeature("panelLogo", IoCContainer.Resolve<PanelLogoBacklightFeature>());
        RegisterFeature("portsBacklight", IoCContainer.Resolve<PortsBacklightFeature>());
        RegisterFeature("powerMode", IoCContainer.Resolve<PowerModeFeature>());
        RegisterFeature("refreshRate", IoCContainer.Resolve<RefreshRateFeature>());
        RegisterFeature("resolution", IoCContainer.Resolve<ResolutionFeature>());
        RegisterFeature("dpiScale", IoCContainer.Resolve<DpiScaleFeature>());
        RegisterFeature("speaker", IoCContainer.Resolve<SpeakerFeature>());
        RegisterFeature("touchpadLock", IoCContainer.Resolve<TouchpadLockFeature>());
        RegisterFeature("whiteKeyboard", IoCContainer.Resolve<WhiteKeyboardBacklightFeature>());
        RegisterFeature("winKey", IoCContainer.Resolve<WinKeyFeature>());
        RegisterFeature("oneLevelWhiteKeyboard", IoCContainer.Resolve<OneLevelWhiteKeyboardBacklightFeature>());

        rpc.RegisterHandler("feature.list", (_, cancellationToken) => HandleListAsync(cancellationToken));
        rpc.RegisterHandler("feature.getSupported", (request, cancellationToken) => HandleGetSupportedAsync(request, cancellationToken));
        rpc.RegisterHandler("feature.getStates", (request, cancellationToken) => HandleGetStatesAsync(request, cancellationToken));
        rpc.RegisterHandler("feature.getState", (request, cancellationToken) => HandleGetStateAsync(request, cancellationToken));
        rpc.RegisterHandler("feature.setState", (request, cancellationToken) => HandleSetStateAsync(request, cancellationToken));
        rpc.RegisterHandler("feature.isHdrBlocked", (_, cancellationToken) => HandleIsHdrBlockedAsync(cancellationToken));
    }

    /// <summary>
    /// Mirrors HDRControl.OnRefreshAsync: HDR is disabled (with a warning) while
    /// Windows settings block it (e.g. display configuration conflicts).
    /// </summary>
    internal static async Task<BridgeResult> HandleIsHdrBlockedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hdr = IoCContainer.Resolve<HDRFeature>();
            var blocked = await hdr.IsHdrBlockedAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { blocked });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // WPF catches and re-enables the toggle — treat as not blocked.
            return BridgeResult.Ok(new { blocked = false });
        }
    }

    internal static void ResetFeaturesForTests() => Features.Clear();

    internal static void RegisterFeatureForTests<T>(string key, IFeature<T> feature) where T : struct
        => RegisterFeature(key, feature);

    private static void RegisterFeature<T>(string key, IFeature<T> feature) where T : struct
    {
        Features[key] = new FeatureEntry<T>(feature);
    }

    /// <summary>
    /// Non-generic view over a concrete <see cref="IFeature{T}"/> so the registry
    /// can hold all 24 features in one dictionary.
    /// </summary>
    public interface IFeatureEntry
    {
        Type FeatureType { get; }
        object? Instance { get; }
        Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default);
        Task<object> GetStateAsObjectAsync(CancellationToken cancellationToken = default);
        Task<object[]> GetAllStatesAsObjectsAsync(CancellationToken cancellationToken = default);
        Task SetStateFromJsonAsync(JsonElement state, CancellationToken cancellationToken = default);
    }

    private sealed class FeatureEntry<T> : IFeatureEntry where T : struct
    {
        private readonly IFeature<T> _feature;

        public FeatureEntry(IFeature<T> feature) => _feature = feature;

        public Type FeatureType => typeof(T);

        public object? Instance => _feature;

        public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _feature.IsSupportedAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<object> GetStateAsObjectAsync(CancellationToken cancellationToken = default)
        {
            var state = await _feature.GetStateAsync(cancellationToken).ConfigureAwait(false);
            return ToWireObject(state);
        }

        public async Task<object[]> GetAllStatesAsObjectsAsync(CancellationToken cancellationToken = default)
        {
            var states = await _feature.GetAllStatesAsync(cancellationToken).ConfigureAwait(false);
            var result = new object[states.Length];
            for (var i = 0; i < states.Length; i++)
                result[i] = ToWireObject(states[i]);
            return result;
        }

        public async Task SetStateFromJsonAsync(JsonElement state, CancellationToken cancellationToken = default)
        {
            var parsed = FromWireObject(state);
            await _feature.SetStateAsync(parsed, cancellationToken).ConfigureAwait(false);
            _feature.InvalidateResolution();
        }

        private static object ToWireObject(T state)
        {
            if (typeof(T).IsEnum)
                return Enum.GetName(typeof(T), state) ?? state.ToString()!;
            return JsonSerializer.SerializeToElement(state);
        }

        internal static T FromWireObject(JsonElement state)
        {
            if (typeof(T).IsEnum)
            {
                if (state.ValueKind != JsonValueKind.String)
                    throw new ArgumentException($"State for {typeof(T).Name} must be a string (enum name), got {state.ValueKind}.");
                var name = state.GetString();
                if (string.IsNullOrWhiteSpace(name) ||
                    !Enum.TryParse(typeof(T), name, ignoreCase: true, out var parsed) ||
                    parsed is null ||
                    !Enum.IsDefined(typeof(T), parsed))
                    throw new ArgumentException($"State for {typeof(T).Name} is not a defined enum name.");
                return (T)parsed;
            }

            if (state.ValueKind != JsonValueKind.Object)
                throw new ArgumentException($"State for {typeof(T).Name} must be an object, got {state.ValueKind}.");

            T deserialized;
            try
            {
                deserialized = JsonSerializer.Deserialize<T>(state.GetRawText(), WireOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"State for {typeof(T).Name} could not be deserialized.", ex);
            }

            if (deserialized.Equals(default(T)))
                throw new ArgumentException($"State for {typeof(T).Name} could not be deserialized.");
            return deserialized;
        }
    }

    internal static async Task<BridgeResult> HandleListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tasks = Features.Select(async kv => new
            {
                key = kv.Key,
                supported = await kv.Value.IsSupportedAsync(cancellationToken).ConfigureAwait(false),
                stateType = kv.Value.FeatureType.Name,
            });
            var features = await Task.WhenAll(tasks).ConfigureAwait(false);

            return BridgeResult.Ok(new { features });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static async Task<BridgeResult> HandleGetSupportedAsync(BridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = GetFeature(request);
            return BridgeResult.Ok(new { supported = await entry.IsSupportedAsync(cancellationToken).ConfigureAwait(false) });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static async Task<BridgeResult> HandleGetStatesAsync(BridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = GetFeature(request);
            var states = await entry.GetAllStatesAsObjectsAsync(cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { states });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (NotSupportedException)
        {
            return BridgeResult.Error(NotSupported, "NOT_SUPPORTED");
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static async Task<BridgeResult> HandleGetStateAsync(BridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = GetFeature(request);
            var state = await entry.GetStateAsObjectAsync(cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { state });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (NotSupportedException)
        {
            return BridgeResult.Error(NotSupported, "NOT_SUPPORTED");
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static async Task<BridgeResult> HandleSetStateAsync(BridgeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = GetFeature(request);

            if (!request.Parameters.TryGetProperty("state", out var stateProp))
                throw new BridgeErrorException(-32602, "Missing 'state' parameter.");

            await entry.SetStateFromJsonAsync(stateProp, cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { ok = true, partial = false });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return MapSetStateException(ex);
        }
    }

    internal static BridgeResult MapSetStateException(Exception ex) => ex switch
    {
        BridgeErrorException bridge => BridgeResult.Error(bridge.Code, bridge.Message),
        PowerModeUnavailableWithoutACException => BridgeResult.Error(AcRequired, "AC_REQUIRED: PowerMode state requires AC power."),
        IGPUModeChangeException igpu => BridgeResult.Error(-32603, $"{igpu.GetType().Name}: {igpu.Message}"),
        NotSupportedException => BridgeResult.Error(NotSupported, "NOT_SUPPORTED"),
        ArgumentException => BridgeResult.Error(UndefinedState, "UNDEFINED_STATE"),
        WmiWriteIndeterminateException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        WmiWriteBusyException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        WmiWriteUnavailableException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        WmiWriteFailedIndeterminateException wmi => BridgeResult.Error(-32603, $"{wmi.GetType().Name}: {wmi.Message}"),
        _ => BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"),
    };

    internal static IFeatureEntry GetFeature(BridgeRequest request)
    {
        if (!request.Parameters.TryGetProperty("feature", out var featureProp) ||
            featureProp.ValueKind != JsonValueKind.String)
            throw new BridgeErrorException(-32602, "Missing string parameter 'feature'.");

        var key = featureProp.GetString();
        if (string.IsNullOrWhiteSpace(key) || !Features.TryGetValue(key, out var entry))
            throw new BridgeErrorException(-32602, $"Unknown feature '{key}'.");

        return entry;
    }
}
