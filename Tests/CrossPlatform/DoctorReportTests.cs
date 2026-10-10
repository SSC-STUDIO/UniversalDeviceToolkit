using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class DoctorReportTests
{
    [Fact]
    public void Create_WhenIdentityTelemetryAndPackMatch_ShouldWarnOnlyForHiddenControls()
    {
        var status = CreateStatus(
            new HardwareIdentity("Framework Computer Inc.", "Framework Laptop 16 A8", "Framework Laptop 16", "SERIAL", "test"),
            new SystemTelemetry("linux-procfs-sysfs", "AMD Ryzen 7 7840U", 16, 31.25, 12.5, [new CpuFrequencyReading("cpu0", 2412.5, "linux-cpufreq")], [new TemperatureReading("Tctl", 54.1, "linux-hwmon")], [new FanSpeedReading("CPU Fan", 2120, "linux-hwmon")], []),
            new PowerStatus("linux-power-supply", [new PowerSupplyReading("BAT0", "Battery", "Discharging", 81, 51.2, 75, 80, 13.4, 15.5, 42, null, true, "Good", "test")], []),
            new PowerProfileStatus("linux-powerprofilesctl", "balanced", [new PowerProfileOption("balanced", "Balanced", true)], true, []),
            new CpuGovernorStatus("linux-cpufreq", "schedutil", [new CpuGovernorPolicy("policy0", "schedutil", ["performance", "powersave", "schedutil"], "/sys/devices/system/cpu/cpufreq/policy0/scaling_governor", "linux-cpufreq")], [new CpuGovernorOption("schedutil", "Schedutil", true)], true, []),
            new BatteryChargeLimitStatus("linux-power-supply-threshold", [new BatteryChargeLimitDevice("BAT0", "BAT0", 40, 80, "/sys/class/power_supply/BAT0/charge_control_start_threshold", "/sys/class/power_supply/BAT0/charge_control_end_threshold", "linux-power-supply-threshold")], []),
            new DisplayBrightnessStatus("linux-backlight", [new DisplayBrightnessDevice("intel_backlight", "intel_backlight", 480, 960, 50, "/sys/class/backlight/intel_backlight/brightness", "linux-backlight")], []),
            new HardwareControlSurface("test", [new HardwareControlDescriptor("power-profile", "Power profile", "standard-os", true, true, "balanced", [], "test"), new HardwareControlDescriptor("cpu-governor", "CPU governor", "standard-os", true, true, "schedutil", [], "test"), new HardwareControlDescriptor("battery-charge-limit", "Battery charge limit", "standard-os", true, true, "80%", [], "test"), new HardwareControlDescriptor("display-brightness", "Display brightness", "standard-os", true, true, "50%", [], "test")], []),
            new CrossPlatformDeviceSupportEvaluator().Evaluate(
                new HardwareIdentity("Framework Computer Inc.", "Framework Laptop 16 A8", "Framework Laptop 16", "SERIAL", "test"),
                isWindows: false),
            "Basic cross-platform diagnostics are available; vendor-specific hardware control is not enabled on this platform.");

        var report = DoctorReport.Create(status);

        report.OverallStatus.Should().Be("warn");
        report.Checks.Should().Contain(check => check.Name == "Hardware identity" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Read-only telemetry" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Power diagnostics" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Power profile" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "CPU governor" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Battery charge limit" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Display brightness" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Control surface" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Device support" && check.Status == DoctorCheckStatus.Pass);
        report.Checks.Should().Contain(check => check.Name == "Hardware controls" && check.Status == DoctorCheckStatus.Warn);
    }

    [Fact]
    public void Create_WhenIdentityAndTelemetryAreMissing_ShouldWarn()
    {
        var status = CreateStatus(
            HardwareIdentity.Unknown("test"),
            SystemTelemetry.Unknown("test", "telemetry unavailable"),
            PowerStatus.Unknown("test", "power unavailable"),
            PowerProfileStatus.Unknown("test", "profile unavailable"),
            CpuGovernorStatus.Unknown("test", "governor unavailable"),
            BatteryChargeLimitStatus.Unknown("test", "charge limit unavailable"),
            DisplayBrightnessStatus.Unknown("test", "brightness unavailable"),
            HardwareControlSurface.Unknown("test", "controls unavailable"),
            new CrossPlatformDeviceSupportEvaluator().Evaluate(HardwareIdentity.Unknown("test"), isWindows: false),
            "Basic cross-platform diagnostics are available; vendor-specific hardware control is not enabled on this platform.");

        var report = DoctorReport.Create(status);

        report.OverallStatus.Should().Be("warn");
        report.Checks.Should().Contain(check => check.Name == "Hardware identity" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Read-only telemetry" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Power diagnostics" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Power profile" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "CPU governor" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Battery charge limit" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Display brightness" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Control surface" && check.Status == DoctorCheckStatus.Warn);
        report.Checks.Should().Contain(check => check.Name == "Device support" && check.Status == DoctorCheckStatus.Warn);
    }

    [Fact]
    public void Serialize_ShouldWriteCheckStatusAsString()
    {
        var report = new DoctorReport("warn", [new DoctorCheck("Hardware identity", DoctorCheckStatus.Warn, "missing")]);

        var json = JsonSerializer.Serialize(report);

        json.Should().Contain("\"Status\":\"Warn\"");
    }

    private static CrossPlatformStatus CreateStatus(
        HardwareIdentity hardware,
        SystemTelemetry telemetry,
        PowerStatus power,
        PowerProfileStatus powerProfile,
        CpuGovernorStatus cpuGovernor,
        BatteryChargeLimitStatus batteryChargeLimit,
        DisplayBrightnessStatus displayBrightness,
        HardwareControlSurface controls,
        DeviceSupportStatus deviceSupport,
        string supportLevel)
    {
        var status = new CrossPlatformStatus(
            "Universal Device Toolkit",
            "4.1.0.0",
            "Test OS",
            "X64",
            "test-machine",
            ".NET 10.0",
            hardware,
            telemetry,
            power,
            powerProfile,
            cpuGovernor,
            batteryChargeLimit,
            displayBrightness,
            controls,
            deviceSupport,
            DoctorReport.CreatePlaceholder(),
            supportLevel,
            []);

        return status with { Doctor = DoctorReport.Create(status) };
    }
}
