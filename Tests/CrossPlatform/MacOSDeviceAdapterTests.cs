using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Platform.MacOS;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class MacOSDeviceAdapterTests
{
    [Theory]
    [InlineData("MacBookPro15,1", "x86_64", "MacBook Pro", "Processor Name", "6-Core Intel Core i7")]
    [InlineData("iMac19,1", "x86_64", "iMac", "Processor Name", "6-Core Intel Core i5")]
    [InlineData("Macmini9,1", "arm64", "Mac mini", "Chip", "Apple M1")]
    public async Task ReadSnapshot_ShouldRecognizeIntelAndAppleSiliconMacFamilies(
        string modelIdentifier,
        string architecture,
        string modelName,
        string processorLabel,
        string processorValue)
    {
        var runner = new FakeCommandRunner(new Dictionary<string, PlatformCommandResult>
        {
            ["sysctl -n hw.model"] = Success(modelIdentifier),
            ["uname -m"] = Success(architecture),
            ["system_profiler SPHardwareDataType"] = Success($"""
                Hardware:

                    Hardware Overview:

                      Model Name: {modelName}
                      {processorLabel}: {processorValue}
                      Serial Number (system): {modelIdentifier}-SERIAL
                """),
        });
        var packs = new[]
        {
            new DevicePackDefinition
            {
                Id = "apple-basic",
                DisplayName = "Apple Basic",
                Vendor = "Apple Inc.",
                ModelKeywords = [modelName],
            },
        };

        var snapshot = await new MacOSDeviceAdapter(runner, packs).ReadSnapshotAsync();

        Assert.Equal("macos", snapshot.Identity.Platform);
        Assert.Equal(architecture, snapshot.Identity.Architecture);
        Assert.Equal(modelName, snapshot.Identity.ProductName);
        Assert.Contains(modelIdentifier, snapshot.Identity.Model, StringComparison.Ordinal);
        Assert.Equal("apple-basic", snapshot.Support.DevicePackId);
        Assert.Equal($"{modelIdentifier}-SERIAL", snapshot.Identity.SerialNumber);
        Assert.DoesNotContain(snapshot.Capabilities, capability => capability.CanWrite);
    }

    [Fact]
    public async Task ReadSnapshot_ShouldExposeAppleIdentityTelemetryAndReadOnlyCapabilities()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, PlatformCommandResult>
        {
            ["sysctl -n hw.model"] = Success("MacBookPro18,3"),
            ["uname -m"] = Success("arm64"),
            ["system_profiler SPHardwareDataType"] = Success("""
                Hardware:

                    Hardware Overview:

                      Model Name: MacBook Pro
                      Chip: Apple M1 Pro
                      Serial Number (system): MAC-SERIAL
                """),
            ["sysctl -n hw.ncpu"] = Success("10"),
            ["sysctl -n hw.memsize"] = Success("17179869184"),
            ["pmset -g batt"] = Success("Now drawing from 'Battery Power'\\n -InternalBattery-0 (id=1)\\t86%; discharging; 3:42 remaining present: true"),
        });
        var packs = new[]
        {
            new DevicePackDefinition
            {
                Id = "apple-basic",
                DisplayName = "Apple Basic",
                Vendor = "Apple Inc.",
                ModelKeywords = ["MacBook Pro"],
            },
        };

        var snapshot = await new MacOSDeviceAdapter(runner, packs).ReadSnapshotAsync();

        Assert.Equal("macos", snapshot.Identity.Platform);
        Assert.Equal("arm64", snapshot.Identity.Architecture);
        Assert.Equal("Apple Inc.", snapshot.Identity.Vendor);
        Assert.Equal("MacBook Pro MacBookPro18,3", snapshot.Identity.Model);
        Assert.Equal("MAC-SERIAL", snapshot.Identity.SerialNumber);
        Assert.Equal("apple-basic", snapshot.Support.DevicePackId);
        Assert.Contains(snapshot.SensorReadings, reading => reading.Name == "Logical CPUs" && reading.Value == 10);
        Assert.Contains(snapshot.SensorReadings, reading => reading.Name == "Battery Charge" && reading.Value == 86);
        Assert.Contains(snapshot.Capabilities, capability => capability.Id == "read-only-telemetry" && capability.IsAvailable && !capability.CanWrite);
        Assert.Contains(snapshot.Capabilities, capability => capability.Id == "fan-control" && !capability.IsAvailable);
        Assert.Equal("Battery Power", snapshot.PowerStatus);
    }

    [Fact]
    public async Task ReadSnapshot_WhenCommandsFail_ShouldDegradeWithoutWrites()
    {
        var snapshot = await new MacOSDeviceAdapter(
            new FakeCommandRunner(new Dictionary<string, PlatformCommandResult>())).ReadSnapshotAsync();

        Assert.Empty(snapshot.Identity.Model);
        Assert.False(snapshot.Capabilities.Single(capability => capability.Id == "hardware-identity").IsAvailable);
        Assert.False(snapshot.Capabilities.Single(capability => capability.Id == "read-only-telemetry").IsAvailable);
        Assert.DoesNotContain(snapshot.Capabilities, capability => capability.CanWrite);
    }

    private static PlatformCommandResult Success(string output) => new(0, output, string.Empty);

    private sealed class FakeCommandRunner(IReadOnlyDictionary<string, PlatformCommandResult> results) : IPlatformCommandRunner
    {
        public PlatformCommandResult Run(string fileName, params string[] arguments)
        {
            var key = string.Join(' ', new[] { fileName }.Concat(arguments));
            return results.TryGetValue(key, out var result)
                ? result
                : new PlatformCommandResult(-1, string.Empty, "missing fake command");
        }
    }
}
