using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class OsdSubscriptionPolicyTests
{
    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    [Theory]
    [InlineData("{}", true, true)]
    [InlineData("null", true, true)]
    [InlineData("{\"capabilities\":{\"settings\":true}}", true, true)]
    [InlineData("{\"capabilities\":{\"sensors\":false}}", false, true)]
    [InlineData("{\"backends\":{\"sensorBackend\":false}}", false, true)]
    [InlineData("{\"capabilities\":{\"sensors\":true,\"fps\":true},\"backends\":{\"sensorBackend\":false}}", false, true)]
    [InlineData("{\"capabilities\":{\"sensors\":false},\"backends\":{\"sensorBackend\":true}}", false, true)]
    [InlineData("{\"capabilities\":{\"fps\":false}}", true, false)]
    [InlineData("{\"capabilities\":{\"sensors\":true,\"fps\":false},\"backends\":{\"sensorBackend\":true}}", true, false)]
    [InlineData("{\"capabilities\":{\"sensors\":true,\"fps\":true},\"backends\":{\"sensorBackend\":true}}", true, true)]
    public void ExplicitHostDenials_TakePrecedenceWithoutInventingMissingCapabilities(string payload, bool sensors, bool fps)
    {
        var policy = OsdSubscriptionPolicy.FromCapabilities(Json(payload));

        Assert.Equal(new OsdSubscriptionPolicy(sensors, fps), policy);
    }

    [Fact]
    public void DiagnosticHost_WithSelectedTelemetryItems_DoesNotRequestEitherSubscription()
    {
        var capabilities = Json("""
            {"executionMode":"diagnostic","capabilities":{"sensors":false,"fps":false},"backends":{"sensorBackend":false}}
            """);
        var settings = Json("""{"Items":["Fps","CpuTemperature","GpuTemperature"]}""");

        Assert.Equal(new OsdSubscriptionPolicy(false, false),
            OsdSubscriptionPolicy.FromCapabilities(capabilities).ForView(true, settings));
    }

    [Theory]
    [InlineData("Fps")]
    [InlineData("LowFps")]
    [InlineData("FrameTime")]
    public void FpsCanRemainAvailable_WhenHardwareSensorBackendIsDisabled(string item)
    {
        var capabilities = Json("""{"capabilities":{"sensors":false,"fps":true},"backends":{"sensorBackend":false}}""");
        var settings = JsonSerializer.SerializeToElement(new { Items = new[] { item } });

        Assert.Equal(new OsdSubscriptionPolicy(false, true),
            OsdSubscriptionPolicy.FromCapabilities(capabilities).ForView(true, settings));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Items\":null}")]
    [InlineData("{\"Items\":\"Fps\"}")]
    [InlineData("{\"Items\":[null,5,\"CpuTemperature\"]}")]
    public void UnselectedOrInvalidFpsItems_DoNotStartFpsMonitoring(string settings)
    {
        Assert.Equal(new OsdSubscriptionPolicy(true, false),
            OsdSubscriptionPolicy.Compatible.ForView(true, Json(settings)));
    }

    [Fact]
    public void HiddenOverlay_RequiresNoSubscriptionsEvenWhenAllTelemetryIsAvailable()
    {
        Assert.Equal(new OsdSubscriptionPolicy(false, false),
            OsdSubscriptionPolicy.Compatible.ForView(false, Json("""{"Items":["Fps"]}""")));
    }

    [Fact]
    public void NewCapabilitySnapshot_ReplacesPreviousHostDenials()
    {
        var disabled = OsdSubscriptionPolicy.FromCapabilities(Json("""{"capabilities":{"sensors":false,"fps":false}}"""));
        var restarted = OsdSubscriptionPolicy.FromCapabilities(Json("""{"capabilities":{"sensors":true,"fps":true}}"""));
        var settings = Json("""{"Items":["Fps"]}""");

        Assert.Equal(new OsdSubscriptionPolicy(false, false), disabled.ForView(true, settings));
        Assert.Equal(new OsdSubscriptionPolicy(true, true), restarted.ForView(true, settings));
    }
}
