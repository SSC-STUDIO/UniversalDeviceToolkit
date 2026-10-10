using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class SystemTelemetryEdgeCaseTests
{
    [Fact]
    public void Unknown_ShouldReturnDefaults()
    {
        var telemetry = SystemTelemetry.Unknown("test", "unavailable");

        telemetry.Source.Should().Be("test");
        telemetry.CpuModel.Should().BeEmpty();
        telemetry.LogicalProcessorCount.Should().BeGreaterThan(0);
        telemetry.MemoryTotalGiB.Should().BeNull();
        telemetry.MemoryAvailableGiB.Should().BeNull();
        telemetry.CpuFrequencies.Should().BeEmpty();
        telemetry.Temperatures.Should().BeEmpty();
        telemetry.FanSpeeds.Should().BeEmpty();
        telemetry.Notes.Should().ContainSingle("unavailable");
    }

    [Fact]
    public void Unknown_WithMultipleNotes_ShouldContainAll()
    {
        var telemetry = SystemTelemetry.Unknown("src", "note1", "note2", "note3");

        telemetry.Notes.Should().HaveCount(3);
        telemetry.Notes.Should().Contain("note1");
        telemetry.Notes.Should().Contain("note2");
        telemetry.Notes.Should().Contain("note3");
    }

    [Fact]
    public void Unknown_WithNoNotes_ShouldHaveEmptyNotes()
    {
        var telemetry = SystemTelemetry.Unknown("src");

        telemetry.Notes.Should().BeEmpty();
    }

    [Fact]
    public void CpuFrequencyReading_Equality()
    {
        var a = new CpuFrequencyReading("cpu0", 2400, "test");
        var b = new CpuFrequencyReading("cpu0", 2400, "test");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void TemperatureReading_Properties()
    {
        var temp = new TemperatureReading("Tctl", 65.5, "hwmon");

        temp.Name.Should().Be("Tctl");
        temp.Celsius.Should().Be(65.5);
        temp.Source.Should().Be("hwmon");
    }

    [Fact]
    public void FanSpeedReading_Properties()
    {
        var fan = new FanSpeedReading("CPU Fan", 2120, "hwmon");

        fan.Name.Should().Be("CPU Fan");
        fan.Rpm.Should().Be(2120);
        fan.Source.Should().Be("hwmon");
    }
}

public sealed class PowerStatusEdgeCaseTests
{
    [Fact]
    public void Unknown_ShouldReturnEmptyDevices()
    {
        var status = PowerStatus.Unknown("test", "unavailable");

        status.Source.Should().Be("test");
        status.Supplies.Should().BeEmpty();
        status.Notes.Should().ContainSingle("unavailable");
    }

    [Fact]
    public void SupplyReading_Equality()
    {
        var a = new PowerSupplyReading("BAT0", "Battery", "Discharging", 81, 51.2, 75, 80, 13.4, 15.5, 42, null, true, "Good", "test");
        var b = new PowerSupplyReading("BAT0", "Battery", "Discharging", 81, 51.2, 75, 80, 13.4, 15.5, 42, null, true, "Good", "test");

        a.Should().Be(b);
    }
}

public sealed class BatteryChargeLimitEdgeCaseTests
{
    [Fact]
    public void Unknown_ShouldReturnEmptyDevices()
    {
        var status = BatteryChargeLimitStatus.Unknown("test", "unavailable");

        status.Source.Should().Be("test");
        status.Devices.Should().BeEmpty();
        status.Notes.Should().ContainSingle("unavailable");
    }

    [Fact]
    public void Device_Equality()
    {
        var a = new BatteryChargeLimitDevice("BAT0", "BAT0", 40, 80, "/path/start", "/path/end", "test");
        var b = new BatteryChargeLimitDevice("BAT0", "BAT0", 40, 80, "/path/start", "/path/end", "test");

        a.Should().Be(b);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 50)]
    [InlineData(100, 0)]
    public void Device_BoundaryThresholds(int start, int end)
    {
        var device = new BatteryChargeLimitDevice("BAT0", "BAT0", start, end, "/s", "/e", "test");

        device.StartThreshold.Should().Be(start);
        device.EndThreshold.Should().Be(end);
    }
}

public sealed class DeviceSupportStatusEdgeCaseTests
{
    [Fact]
    public void Evaluate_LegionWindows_SupportLevelIsBasic()
    {
        var evaluator = new CrossPlatformDeviceSupportEvaluator();
        var identity = new HardwareIdentity("Lenovo", "Legion 5", "Legion 5", "SN", "test");

        var result = evaluator.Evaluate(identity, isWindows: true);

        result.SupportLevel.Should().NotBeEmpty();
        result.EnabledFeatures.Should().Contain("diagnostics");
    }

    [Fact]
    public void Evaluate_LegionLinux_SupportLevelIsBasic()
    {
        var evaluator = new CrossPlatformDeviceSupportEvaluator();
        var identity = new HardwareIdentity("Lenovo", "Legion 5", "Legion 5", "SN", "test");

        var result = evaluator.Evaluate(identity, isWindows: false);

        result.SupportLevel.Should().NotBeEmpty();
        result.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Fact]
    public void Evaluate_DellWindows_ReturnsBasicMode()
    {
        var evaluator = new CrossPlatformDeviceSupportEvaluator();
        var identity = new HardwareIdentity("Dell", "XPS 15", "XPS", "SN", "test");

        var result = evaluator.Evaluate(identity, isWindows: true);

        result.SupportLevel.Should().NotBeEmpty();
        result.DevicePackId.Should().NotBeEmpty();
    }
}

public sealed class DoctorReportSerializationTests
{
    [Fact]
    public void Serialize_RoundTrip_ShouldPreserveAllFields()
    {
        var report = new DoctorReport(
            "pass",
            [
                new DoctorCheck("Hardware identity", DoctorCheckStatus.Pass, "ok"),
                new DoctorCheck("Telemetry", DoctorCheckStatus.Warn, "partial data")
            ]);

        var json = JsonSerializer.Serialize(report);
        var deserialized = JsonSerializer.Deserialize<DoctorReport>(json);

        deserialized.Should().NotBeNull();
        deserialized!.OverallStatus.Should().Be("pass");
        deserialized.Checks.Should().HaveCount(2);
        deserialized.Checks[0].Name.Should().Be("Hardware identity");
        deserialized.Checks[1].Status.Should().Be(DoctorCheckStatus.Warn);
    }

    [Fact]
    public void DoctorCheckStatus_AllValues_AreDefined()
    {
        var values = Enum.GetValues<DoctorCheckStatus>();
        values.Should().Contain(DoctorCheckStatus.Pass);
        values.Should().Contain(DoctorCheckStatus.Warn);
        values.Should().Contain(DoctorCheckStatus.Fail);
    }
}
