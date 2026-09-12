using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Platform.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

public sealed class WindowsDeviceAdapterTests
{
    [Theory]
    [InlineData("LENOVO", "Legion Pro 7 16IRX9", "lenovo-legion-pro-7")]
    [InlineData("ASUSTeK COMPUTER INC.", "ROG Zephyrus G14", "asus-basic")]
    [InlineData("Dell Inc.", "Alienware m18 R2", "dell-basic")]
    [InlineData("Hewlett-Packard Company", "OMEN Max 16", "hp-basic")]
    [InlineData("Micro-Star International Co., Ltd.", "MSI Raider 18", "msi-basic")]
    [InlineData("Default string", "System Product Name", "universal-desktop-basic")]
    [InlineData("Acme Corporation", "Unrecognized Office PC", "generic-pc-basic")]
    public async Task ReadSnapshot_ShouldUseSharedCatalogForCommonWindowsMachines(
        string vendor,
        string model,
        string expectedPackId)
    {
        var reader = new FakeWmiReader(new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>>
        {
            ["Win32_ComputerSystem"] = [Row(("Manufacturer", vendor), ("Model", model))],
            ["Win32_BIOS"] = [Row(("SMBIOSBIOSVersion", "TEST-BIOS"))],
            ["Win32_Processor"] = [Row(("NumberOfLogicalProcessors", "8"))],
        });

        var snapshot = await new WindowsDeviceAdapter(reader, DevicePackCatalogLoader.Load()).ReadSnapshotAsync();

        Assert.Equal(expectedPackId, snapshot.Support.DevicePackId);
        Assert.False(snapshot.Support.IsHardwareControlAvailable);
        Assert.DoesNotContain(snapshot.Capabilities, capability => capability.CanWrite);
    }

    [Fact]
    public async Task ReadSnapshot_ShouldMatchGenericWindowsMachineAndKeepWritesHidden()
    {
        var reader = new FakeWmiReader(new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>>
        {
            ["Win32_ComputerSystem"] =
            [
                Row(
                    ("Manufacturer", "ASUSTeK COMPUTER INC."),
                    ("Model", "ROG Zephyrus G14"),
                    ("SystemSKUNumber", "FAKE-SKU"),
                    ("TotalPhysicalMemory", "17179869184")),
            ],
            ["Win32_BIOS"] = [Row(("SMBIOSBIOSVersion", "FAKE-BIOS"), ("SerialNumber", "FAKE-SERIAL"))],
            ["Win32_Processor"] = [Row(("Name", "Test CPU"), ("LoadPercentage", "27"), ("NumberOfLogicalProcessors", "16"))],
            ["Win32_Battery"] = [Row(("EstimatedChargeRemaining", "82"), ("BatteryStatus", "2"))],
            ["Win32_VideoController"] = [Row(("Name", "Test GPU"))],
        });
        var packs = new[]
        {
            new DevicePackDefinition
            {
                Id = "asus-basic",
                DisplayName = "ASUS Basic",
                Vendor = "ASUSTeK COMPUTER INC.",
                ModelKeywords = ["ROG"],
            },
        };

        var snapshot = await new WindowsDeviceAdapter(reader, packs).ReadSnapshotAsync();

        Assert.Equal("windows", snapshot.Identity.Platform);
        Assert.Equal("ASUSTeK COMPUTER INC.", snapshot.Identity.Vendor);
        Assert.Equal("ROG Zephyrus G14", snapshot.Identity.Model);
        Assert.Equal("FAKE-BIOS", snapshot.Identity.BiosVersion);
        Assert.Equal("FAKE-SERIAL", snapshot.Identity.SerialNumber);
        Assert.Equal("asus-basic", snapshot.Support.DevicePackId);
        Assert.Contains(snapshot.SensorReadings, reading => reading.Name == "CPU Usage" && reading.Value == 27);
        Assert.Contains(snapshot.SensorReadings, reading => reading.Name == "Battery Charge" && reading.Value == 82);
        Assert.Contains(snapshot.Capabilities, capability => capability.Id == "power-diagnostics" && capability.IsAvailable);
        Assert.DoesNotContain(snapshot.Capabilities, capability => capability.CanWrite);
    }

    [Fact]
    public async Task ReadSnapshot_WhenWmiIsUnavailable_ShouldReturnSafeUnknownSnapshot()
    {
        var snapshot = await new WindowsDeviceAdapter(new FakeWmiReader(new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>>())).ReadSnapshotAsync();

        Assert.Empty(snapshot.Identity.Vendor);
        Assert.Empty(snapshot.Identity.Model);
        Assert.Equal(DeviceSupportMatcher.GenericBasicPackId, snapshot.Support.DevicePackId);
        Assert.False(snapshot.Capabilities.Single(capability => capability.Id == "hardware-identity").IsAvailable);
        Assert.DoesNotContain(snapshot.Capabilities, capability => capability.CanWrite);
    }

    private static IReadOnlyDictionary<string, string?> Row(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => (string?)value.Value, StringComparer.OrdinalIgnoreCase);

    private sealed class FakeWmiReader(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string?>>> rows) : IWindowsWmiReader
    {
        public IReadOnlyList<IReadOnlyDictionary<string, string?>> Query(string className) =>
            rows.TryGetValue(className, out var result) ? result : [];
    }
}
