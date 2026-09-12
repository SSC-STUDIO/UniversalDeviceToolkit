using UniversalDeviceToolkit.Abstractions.Hardware;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class DeviceAdapterCliIntegrationTests
{
    [Fact]
    public void Create_WithInjectedSnapshot_ShouldUseItForHardwareSupportAndDoctor()
    {
        var snapshot = new DeviceSnapshot(
            new DeviceIdentity("windows", "X64", "Acme", "Acme Model", "Acme Product", "BIOS-1", "SERIAL", "fake-adapter"),
            new DeviceSupportInfo(
                "Safe basic mode",
                "acme-basic",
                "Acme Basic",
                ["diagnostics", "hardware-identity"],
                ["fan-curve", "gpu-overclock"],
                "Matched by fake adapter."),
            [new DeviceCapability("hardware-identity", true, false, "fake-adapter", "Read-only identity.")],
            [new SensorReading("Logical CPUs", "System", 8, "count")],
            "AC Power",
            "fake-adapter");

        var status = CrossPlatformStatus.Create(new FakeDeviceAdapter(snapshot));

        Assert.Equal("Acme", status.Hardware.Vendor);
        Assert.Equal("Acme Product", status.Hardware.ProductName);
        Assert.Equal("acme-basic", status.DeviceSupport.DevicePackId);
        Assert.Equal("Acme Basic", status.DeviceSupport.DisplayName);
        Assert.Contains(status.Doctor.Checks, check =>
            check.Name == "Hardware identity" && check.Status == DoctorCheckStatus.Pass);
        Assert.Contains(status.Doctor.Checks, check =>
            check.Name == "Device support" && check.Detail.Contains("Acme Basic", StringComparison.Ordinal));
    }

    private sealed class FakeDeviceAdapter(DeviceSnapshot snapshot) : IDeviceAdapter
    {
        public string PlatformId => snapshot.Identity.Platform;

        public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }
}
