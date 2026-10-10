using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Features.WhiteKeyboardBacklight;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// RGB/Spectrum/white keyboard backlight bridge: capability detection, RGB preset
/// state and Spectrum profile control. Payloads use LltJson compact options
/// (PascalCase property names, enum values as strings).
/// </summary>
public static class KeyboardBacklightHandlers
{
    private const int NotSupported = BridgeErrorCodes.FeatureNotSupported;
    private const int InvalidParams = BridgeErrorCodes.InvalidParams;
    private const int InternalError = BridgeErrorCodes.InternalError;

    private static JsonSerializerOptions? _options;
    private static JsonSerializerOptions? _inputOptions;

    private static JsonSerializerOptions Options => _options ??= LltJson.CreateCompactOptions();

    private static JsonSerializerOptions InputOptions
    {
        get
        {
            if (_inputOptions is not null)
                return _inputOptions;

            _inputOptions = new JsonSerializerOptions(Options)
            {
                PropertyNameCaseInsensitive = true,
            };
            return _inputOptions;
        }
    }

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("keyboard.detect", (request, _) => HandleDetectAsync());

        rpc.RegisterHandler("rgb.isSupported", (request, _) => HandleRgbIsSupportedAsync());
        rpc.RegisterHandler("rgb.getState", (request, _) => HandleRgbGetStateAsync());
        rpc.RegisterHandler("rgb.setState", (request, _) => HandleRgbSetStateAsync(request));
        rpc.RegisterHandler("rgb.setPreset", (request, _) => HandleRgbSetPresetAsync(request));
        rpc.RegisterHandler("rgb.nextPreset", (request, _) => HandleRgbNextPresetAsync());
        rpc.RegisterHandler("rgb.takeOwnership", (request, _) => HandleRgbTakeOwnershipAsync(request));

        rpc.RegisterHandler("spectrum.isSupported", (request, _) => HandleSpectrumIsSupportedAsync());
        rpc.RegisterHandler("spectrum.getLayout", (request, _) => HandleSpectrumGetLayoutAsync());
        rpc.RegisterHandler("spectrum.getState", (request, _) => HandleSpectrumGetStateAsync());
        rpc.RegisterHandler("spectrum.getBrightness", (request, _) => HandleSpectrumGetBrightnessAsync());
        rpc.RegisterHandler("spectrum.setBrightness", (request, _) => HandleSpectrumSetBrightnessAsync(request));
        rpc.RegisterHandler("spectrum.getLogoStatus", (request, _) => HandleSpectrumGetLogoStatusAsync());
        rpc.RegisterHandler("spectrum.setLogoStatus", (request, _) => HandleSpectrumSetLogoStatusAsync(request));
        rpc.RegisterHandler("spectrum.getProfile", (request, _) => HandleSpectrumGetProfileAsync());
        rpc.RegisterHandler("spectrum.setProfile", (request, _) => HandleSpectrumSetProfileAsync(request));
        rpc.RegisterHandler("spectrum.getProfileDescription", (request, _) => HandleSpectrumGetProfileDescriptionAsync(request));
        rpc.RegisterHandler("spectrum.setProfileDescription", (request, _) => HandleSpectrumSetProfileDescriptionAsync(request));
    }

    private static RGBKeyboardBacklightController GetRgb()
        => IoCContainer.Resolve<RGBKeyboardBacklightController>();

    private static SpectrumKeyboardBacklightController GetSpectrum()
        => IoCContainer.Resolve<SpectrumKeyboardBacklightController>();

    // ── detect ──────────────────────────────────────────────────────────────

    private static async Task<BridgeResult> HandleDetectAsync()
    {
        try
        {
            var spectrum = await ProbeSupportedAsync(() => GetSpectrum().IsSupportedAsync()).ConfigureAwait(false);
            if (spectrum.Supported)
                return BridgeResult.Ok(new { mode = "spectrum" });

            var rgb = await ProbeSupportedAsync(() => GetRgb().IsSupportedAsync()).ConfigureAwait(false);
            if (rgb.Supported)
                return BridgeResult.Ok(new { mode = "rgb" });

            var white = await ProbeSupportedAsync(
                () => IoCContainer.Resolve<WhiteKeyboardBacklightFeature>().IsSupportedAsync()).ConfigureAwait(false);
            if (white.Supported)
                return BridgeResult.Ok(new { mode = "white" });

            var oneLevel = await ProbeSupportedAsync(
                () => IoCContainer.Resolve<OneLevelWhiteKeyboardBacklightFeature>().IsSupportedAsync()).ConfigureAwait(false);
            if (oneLevel.Supported)
                return BridgeResult.Ok(new { mode = "oneLevelWhite" });

            var error = spectrum.Error ?? rgb.Error ?? white.Error ?? oneLevel.Error;
            if (error is not null)
                return Fail(error);

            return BridgeResult.Ok(new { mode = "none" });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    // ── RGB ─────────────────────────────────────────────────────────────────

    private static async Task<BridgeResult> HandleRgbIsSupportedAsync()
    {
        try
        {
            return BridgeResult.Ok(new { supported = await GetRgb().IsSupportedAsync().ConfigureAwait(false) });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleRgbGetStateAsync()
    {
        try
        {
            await EnsureRgbSupportedAsync().ConfigureAwait(false);
            var state = await GetRgb().GetStateAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { state = JsonSerializer.SerializeToElement(state, Options) });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleRgbSetStateAsync(BridgeRequest request)
    {
        try
        {
            await EnsureRgbSupportedAsync().ConfigureAwait(false);

            if (!request.Parameters.TryGetProperty("state", out var stateProp))
                throw new BridgeErrorException(InvalidParams, "Missing 'state' parameter.");

            var state = ReadRgbState(stateProp);
            await GetRgb().SetStateAsync(state).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleRgbSetPresetAsync(BridgeRequest request)
    {
        try
        {
            await EnsureRgbSupportedAsync().ConfigureAwait(false);

            if (!request.Parameters.TryGetProperty("preset", out var presetProp) ||
                presetProp.ValueKind != JsonValueKind.String)
                throw new BridgeErrorException(InvalidParams, "Missing string parameter 'preset'.");

            var preset = ParseDefinedEnum<RGBKeyboardBacklightPreset>(presetProp.GetString(), "preset");

            var rgb = GetRgb();
            await rgb.SetPresetAsync(preset).ConfigureAwait(false);
            var state = await rgb.GetStateAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { state = JsonSerializer.SerializeToElement(state, Options) });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleRgbNextPresetAsync()
    {
        try
        {
            await EnsureRgbSupportedAsync().ConfigureAwait(false);

            var rgb = GetRgb();
            await rgb.SetNextPresetAsync().ConfigureAwait(false);
            var state = await rgb.GetStateAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { state = JsonSerializer.SerializeToElement(state, Options) });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleRgbTakeOwnershipAsync(BridgeRequest request)
    {
        try
        {
            await EnsureRgbSupportedAsync().ConfigureAwait(false);

            if (!request.Parameters.TryGetProperty("enable", out var enableProp) ||
                enableProp.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new BridgeErrorException(InvalidParams, "Missing boolean parameter 'enable'.");

            var enable = enableProp.GetBoolean();
            var restorePreset = false;
            if (request.Parameters.TryGetProperty("restorePreset", out var restoreProp))
            {
                if (restoreProp.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new BridgeErrorException(InvalidParams, "Parameter 'restorePreset' must be a boolean.");
                restorePreset = restoreProp.GetBoolean();
            }

            await GetRgb().SetLightControlOwnerAsync(enable, restorePreset).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task EnsureRgbSupportedAsync()
    {
        if (!await GetRgb().IsSupportedAsync().ConfigureAwait(false))
            throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");
    }

    // ── Spectrum ────────────────────────────────────────────────────────────

    private static async Task<BridgeResult> HandleSpectrumIsSupportedAsync()
    {
        try
        {
            return BridgeResult.Ok(new { supported = await GetSpectrum().IsSupportedAsync().ConfigureAwait(false) });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumGetLayoutAsync()
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            var (spectrumLayout, keyboardLayout, keys) = await GetSpectrum().GetKeyboardLayoutAsync().ConfigureAwait(false);
            var result = JsonSerializer.SerializeToElement(
                new { spectrumLayout, keyboardLayout, keys = keys.OrderBy(k => k).ToArray() },
                Options);
            return BridgeResult.Ok(result);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumGetStateAsync()
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            var state = await GetSpectrum().GetStateAsync().ConfigureAwait(false);
            var keys = state
                .OrderBy(pair => pair.Key)
                .Select(pair => new { key = pair.Key, r = pair.Value.R, g = pair.Value.G, b = pair.Value.B })
                .ToArray();
            return BridgeResult.Ok(new { keys });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumGetBrightnessAsync()
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);
            var brightness = await GetSpectrum().GetBrightnessAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { brightness });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumSetBrightnessAsync(BridgeRequest request)
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            var brightness = GetRequiredInt(request, "brightness");
            if (brightness is < 0 or > 9)
                throw new BridgeErrorException(InvalidParams, $"Invalid brightness '{brightness}' (expected 0..9).");

            await GetSpectrum().SetBrightnessAsync(brightness).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumGetLogoStatusAsync()
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);
            var isOn = await GetSpectrum().GetLogoStatusAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { isOn });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumSetLogoStatusAsync(BridgeRequest request)
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            if (!request.Parameters.TryGetProperty("isOn", out var isOnProp) ||
                isOnProp.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new BridgeErrorException(InvalidParams, "Missing boolean parameter 'isOn'.");

            await GetSpectrum().SetLogoStatusAsync(isOnProp.GetBoolean()).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumGetProfileAsync()
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);
            var profile = await GetSpectrum().GetProfileAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { profile });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumSetProfileAsync(BridgeRequest request)
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            var profile = GetRequiredProfile(request);
            await GetSpectrum().SetProfileAsync(profile).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumGetProfileDescriptionAsync(BridgeRequest request)
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            var profile = GetRequiredProfile(request);
            var (resolvedProfile, effects) = await GetSpectrum().GetProfileDescriptionAsync(profile).ConfigureAwait(false);
            var result = JsonSerializer.SerializeToElement(new { profile = resolvedProfile, effects }, Options);
            return BridgeResult.Ok(result);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task<BridgeResult> HandleSpectrumSetProfileDescriptionAsync(BridgeRequest request)
    {
        try
        {
            await EnsureSpectrumSupportedAsync().ConfigureAwait(false);

            var profile = GetRequiredProfile(request);
            if (!request.Parameters.TryGetProperty("effects", out var effectsProp) ||
                effectsProp.ValueKind != JsonValueKind.Array)
                throw new BridgeErrorException(InvalidParams, "Missing array parameter 'effects'.");

            var effects = ReadEffects(effectsProp);
            await GetSpectrum().SetProfileDescriptionAsync(profile, effects).ConfigureAwait(false);
            return BridgeResult.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static async Task EnsureSpectrumSupportedAsync()
    {
        if (!await GetSpectrum().IsSupportedAsync().ConfigureAwait(false))
            throw new BridgeErrorException(NotSupported, "NOT_SUPPORTED");
    }

    private static int GetRequiredProfile(BridgeRequest request)
    {
        var profile = GetRequiredInt(request, "profile");
        if (profile is < 0 or > 6)
            throw new BridgeErrorException(InvalidParams, $"Invalid profile '{profile}' (expected 0..6).");
        return profile;
    }

    private static int GetRequiredInt(BridgeRequest request, string property)
    {
        if (!request.Parameters.TryGetProperty(property, out var prop) ||
            prop.ValueKind != JsonValueKind.Number ||
            !prop.TryGetInt32(out var value))
            throw new BridgeErrorException(InvalidParams, $"Missing number parameter '{property}'.");
        return value;
    }

    private static RGBKeyboardBacklightState ReadRgbState(JsonElement stateProp)
    {
        if (stateProp.ValueKind != JsonValueKind.Object)
            throw new BridgeErrorException(InvalidParams, "Parameter 'state' must be an object.");

        if (!TryGetProperty(stateProp, "SelectedPreset", out var presetProp) ||
            presetProp.ValueKind != JsonValueKind.String)
            throw new BridgeErrorException(InvalidParams, "Missing string property 'SelectedPreset'.");

        var selectedPreset = ParseDefinedEnum<RGBKeyboardBacklightPreset>(presetProp.GetString(), "SelectedPreset");

        if (!TryGetProperty(stateProp, "Presets", out var presetsProp) ||
            presetsProp.ValueKind != JsonValueKind.Object)
            throw new BridgeErrorException(InvalidParams, "Missing object property 'Presets'.");

        RGBKeyboardBacklightState state;
        try
        {
            state = JsonSerializer.Deserialize<RGBKeyboardBacklightState>(stateProp.GetRawText(), InputOptions);
        }
        catch (JsonException ex)
        {
            throw new BridgeErrorException(InvalidParams, $"Invalid 'state' parameter. {ex.Message}");
        }

        if (!Enum.IsDefined(state.SelectedPreset) || state.SelectedPreset != selectedPreset)
            throw new BridgeErrorException(InvalidParams, $"Invalid SelectedPreset '{state.SelectedPreset}'.");

        if (state.Presets is null)
            throw new BridgeErrorException(InvalidParams, "Missing object property 'Presets'.");

        foreach (var (preset, description) in state.Presets)
        {
            if (!Enum.IsDefined(preset))
                throw new BridgeErrorException(InvalidParams, $"Invalid preset key '{preset}'.");
            if (!Enum.IsDefined(description.Effect) ||
                !Enum.IsDefined(description.Speed) ||
                !Enum.IsDefined(description.Brightness))
                throw new BridgeErrorException(InvalidParams, $"Invalid description for preset '{preset}'.");
        }

        return state;
    }

    private static SpectrumKeyboardBacklightEffect[] ReadEffects(JsonElement effectsProp)
    {
        SpectrumKeyboardBacklightEffect[]? effects;
        try
        {
            effects = JsonSerializer.Deserialize<SpectrumKeyboardBacklightEffect[]>(effectsProp.GetRawText(), InputOptions);
        }
        catch (JsonException ex)
        {
            throw new BridgeErrorException(InvalidParams, $"Invalid 'effects' parameter. {ex.Message}");
        }

        if (effects is null)
            throw new BridgeErrorException(InvalidParams, "Invalid 'effects' parameter.");

        for (var i = 0; i < effects.Length; i++)
        {
            var effect = effects[i];
            if (!Enum.IsDefined(effect.Type) ||
                !Enum.IsDefined(effect.Speed) ||
                !Enum.IsDefined(effect.Direction) ||
                !Enum.IsDefined(effect.ClockwiseDirection))
                throw new BridgeErrorException(InvalidParams, $"Invalid effect at index {i}.");
            if (effect.Colors is null)
                throw new BridgeErrorException(InvalidParams, $"Effect at index {i} is missing 'Colors'.");
            if (effect.Keys is null)
                throw new BridgeErrorException(InvalidParams, $"Effect at index {i} is missing 'Keys'.");
        }

        return effects;
    }

    private static T ParseDefinedEnum<T>(string? text, string parameter) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !Enum.TryParse<T>(text, ignoreCase: true, out var value) ||
            !Enum.IsDefined(value) ||
            !string.Equals(Enum.GetName(value), text, StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeErrorException(InvalidParams, $"Invalid {parameter} '{text}'.");
        }

        return value;
    }

    private static bool TryGetProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
            return true;

        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static async Task<(bool Supported, Exception? Error)> ProbeSupportedAsync(Func<Task<bool>> probe)
    {
        try
        {
            return (await probe().ConfigureAwait(false), null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    private static BridgeResult Fail(Exception ex) => ex switch
    {
        BridgeErrorException bridge => BridgeResult.Error(bridge.Code, bridge.Message),
        JsonException json => BridgeResult.Error(InvalidParams, json.Message),
        FormatException format => BridgeResult.Error(InvalidParams, format.Message),
        _ => BridgeResult.Error(InternalError, $"{ex.GetType().Name}: {ex.Message}"),
    };
}
