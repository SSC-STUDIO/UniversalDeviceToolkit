using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Guard)]
public sealed class KeyboardBacklightHandlersTests
{
    private static string Source => RepositoryPaths.ReadFile(
        "Apps/Host", "Rpc", "Handlers", "KeyboardBacklightHandlers.cs");

    [Fact]
    public void SpectrumGetState_ShouldRejectUnsupportedInsteadOfEmptySuccess()
    {
        var source = Source;
        source.Should().Contain("HandleSpectrumGetStateAsync");
        source.Should().Contain("await EnsureSpectrumSupportedAsync()");
        source.Should().NotContain("keys = Array.Empty<object>()");
    }

    [Fact]
    public void KeyboardDetect_ShouldMapSpectrumRgbAndWhiteModesWithoutTreatingProbeFailuresAsNone()
    {
        var source = Source;
        source.Should().Contain("mode = \"spectrum\"");
        source.Should().Contain("mode = \"rgb\"");
        source.Should().Contain("mode = \"white\"");
        source.Should().Contain("mode = \"oneLevelWhite\"");
        source.Should().Contain("WhiteKeyboardBacklightFeature");
        source.Should().Contain("OneLevelWhiteKeyboardBacklightFeature");
        source.Should().Contain("ProbeSupportedAsync");
        source.Should().Contain("spectrum.Error ?? rgb.Error ?? white.Error ?? oneLevel.Error");
    }

    [Fact]
    public void RgbAndSpectrumWrites_ShouldRejectUndefinedEnumsAndInvalidPayloads()
    {
        var source = Source;
        source.Should().Contain("ParseDefinedEnum<RGBKeyboardBacklightPreset>");
        source.Should().Contain("Enum.IsDefined(value)");
        source.Should().Contain("ReadRgbState");
        source.Should().Contain("Parameter 'state' must be an object.");
        source.Should().Contain("Missing string property 'SelectedPreset'.");
        source.Should().Contain("Missing object property 'Presets'.");
        source.Should().Contain("ReadEffects");
        source.Should().Contain("GetRequiredProfile");
        source.Should().Contain("Invalid profile");
    }

    [Fact]
    public void DeserializationFailures_ShouldMapToInvalidParamsInsteadOfSuccessOrInternalErrorOnly()
    {
        var source = Source;
        source.Should().Contain("catch (JsonException ex)");
        source.Should().Contain("JsonException json => BridgeResult.Error(InvalidParams, json.Message)");
        source.Should().Contain("TryGetInt32");
        source.Should().Contain("PropertyNameCaseInsensitive = true");
        source.Should().NotContain("Enum.TryParse<RGBKeyboardBacklightPreset>(presetProp.GetString(), ignoreCase: true, out var preset)");
    }

    [Fact]
    public void RgbAndSpectrumStateResults_ShouldUseCompactEnumStringMapping()
    {
        var source = Source;
        source.Should().Contain("state = JsonSerializer.SerializeToElement(state, Options)");
        source.Should().Contain("spectrumLayout, keyboardLayout");
        source.Should().Contain("keys.OrderBy(k => k).ToArray()");
        source.Should().Contain("SerializeToElement(new { profile = resolvedProfile, effects }, Options)");
        source.Should().Contain("r = pair.Value.R, g = pair.Value.G, b = pair.Value.B");
    }
}
