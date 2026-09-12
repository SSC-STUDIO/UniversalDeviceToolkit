using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Tests;

public class WindowsDeviceInfoTests
{
    [Fact]
    public void PartialHardwarePreservesZeroAndLeavesUnavailableFieldsNull()
    {
        using var input = JsonDocument.Parse("""
            {"vendor":"Example","hardware":{"processor":{"name":"CPU","numberOfCores":0,"maxClockSpeedMHz":"unknown"},"memory":{"totalCapacityBytes":0}}}
            """);
        var result = DeviceInfoProjection.From(input.RootElement);
        Assert.Equal("Example", result.GetProperty("vendor").GetString());
        Assert.Equal("CPU", result.GetProperty("processor").GetProperty("name").GetString());
        Assert.Equal(0, result.GetProperty("processor").GetProperty("numberOfCores").GetInt32());
        Assert.False(result.GetProperty("processor").TryGetProperty("maxClockSpeedMHz", out _));
        Assert.Equal(0, result.GetProperty("memory").GetProperty("totalCapacityBytes").GetInt32());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("videoController").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("warranty").ValueKind);
    }

    [Fact]
    public void InvalidPayloadUsesTheStartupFallback()
    {
        var result = DeviceInfoProjection.From(default);
        Assert.Equal("Universal Device Toolkit", result.GetProperty("model").GetString());
        Assert.Equal(string.Empty, result.GetProperty("serialNumber").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("processor").ValueKind);
    }
}
