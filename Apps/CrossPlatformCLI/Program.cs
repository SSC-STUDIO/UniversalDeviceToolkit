using System.Runtime.InteropServices;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Resources;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.Platform.MacOS;
using WindowsDeviceAdapter = UniversalDeviceToolkit.Platform.Windows.Core.WindowsDeviceAdapterCore;

var languageArguments = LanguageArguments.Parse(args);
LocalizationRuntime.Initialize(languageArguments.OverrideCulture, persist: false);
var command = languageArguments.Remaining.FirstOrDefault() ?? "status";
var commandArguments = languageArguments.Remaining.Skip(1).ToArray();

return command.ToLowerInvariant() switch
{
    "status" => PrintStatus(),
    "json" => PrintJson(),
    "hardware" => PrintHardware(),
    "telemetry" => PrintTelemetry(),
    "power" => PrintPower(),
    "profile" or "power-profile" => PrintOrSetPowerProfile(commandArguments),
    "controls" => PrintControls(),
    "set" => SetControl(commandArguments),
    "elevate" => Elevate(commandArguments),
    "support" => PrintSupport(),
    "doctor" => PrintDoctor(),
    "help" or "--help" or "-h" => PrintHelp(),
    _ => PrintUnknownCommand(command)
};

static string T(string key, string fallback, params object[] args)
{
    var value = CrossPlatformStrings.Get(key, fallback);
    return args.Length == 0 ? value : string.Format(LocalizationRuntime.CurrentCulture, value, args);
}

static int PrintStatus()
{
    var status = CrossPlatformStatus.Create();

    Console.WriteLine($"{status.ProductName}");
    Console.WriteLine(T("Help_Title", "Cross-platform diagnostics"));
    Console.WriteLine($"{T("Label_Version", "Version")}: {status.Version}");
    Console.WriteLine($"{T("Label_OS", "OS")}: {status.OsDescription}");
    Console.WriteLine($"{T("Label_Architecture", "Architecture")}: {status.Architecture}");
    Console.WriteLine($"{T("Label_Machine", "Machine")}: {status.MachineName}");
    Console.WriteLine($"{T("Label_Runtime", "Runtime")}: {status.DotNetRuntime}");
    Console.WriteLine($"{T("Label_Hardware", "Hardware")}: {FormatHardwareSummary(status.Hardware)}");
    Console.WriteLine($"{T("Label_Telemetry", "Telemetry")}: {FormatTelemetrySummary(status.Telemetry)}");
    Console.WriteLine($"{T("Label_Power", "Power")}: {FormatPowerSummary(status.Power)}");
    Console.WriteLine($"{T("Label_PowerProfile", "Power profile")}: {FormatPowerProfileSummary(status.PowerProfile)}");
    Console.WriteLine($"{T("Label_CpuGovernor", "CPU governor")}: {FormatCpuGovernorSummary(status.CpuGovernor)}");
    Console.WriteLine($"{T("Label_BatteryChargeLimit", "Battery charge limit")}: {FormatBatteryChargeLimitSummary(status.BatteryChargeLimit)}");
    Console.WriteLine($"{T("Label_DisplayBrightness", "Display brightness")}: {FormatDisplayBrightnessSummary(status.DisplayBrightness)}");
    Console.WriteLine($"{T("Label_Controls", "Controls")}: {FormatControlSummary(status.Controls)}");
    Console.WriteLine($"{T("Label_DevicePack", "Device pack")}: {status.DeviceSupport.DisplayName} ({status.DeviceSupport.DevicePackId})");
    Console.WriteLine($"{T("Label_SupportLevel", "Support level")}: {LocalizeSupportLevel(status.SupportLevel)}");
    Console.WriteLine();

    foreach (var capability in status.Capabilities)
        Console.WriteLine($"[{FormatBoolean(capability.Available)}] {T(capability.NameKey, capability.Name)} - {T(capability.DetailKey, capability.Detail)}");

    return 0;
}

static int PrintJson()
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    Console.WriteLine(JsonSerializer.Serialize(CrossPlatformStatus.Create(), options));
    return 0;
}

static int PrintHardware()
{
    var hardware = CrossPlatformStatus.Create().Hardware;

    Console.WriteLine(T("Hardware_Title", "Hardware identity"));
    Console.WriteLine($"{T("Label_Vendor", "Vendor")}: {ValueOrUnknown(hardware.Vendor)}");
    Console.WriteLine($"{T("Label_Model", "Model")}: {ValueOrUnknown(hardware.Model)}");
    Console.WriteLine($"{T("Label_Product", "Product")}: {ValueOrUnknown(hardware.ProductName)}");
    Console.WriteLine($"{T("Label_Serial", "Serial")}: {ValueOrUnknown(hardware.SerialNumber)}");
    Console.WriteLine($"{T("Label_Source", "Source")}: {hardware.Source}");
    return 0;
}

static int PrintTelemetry()
{
    var telemetry = CrossPlatformStatus.Create().Telemetry;

    Console.WriteLine(T("Telemetry_Title", "System telemetry"));
    Console.WriteLine($"{T("Label_Cpu", "CPU")}: {ValueOrUnknown(telemetry.CpuModel)}");
    Console.WriteLine($"{T("Label_LogicalProcessors", "Logical processors")}: {telemetry.LogicalProcessorCount?.ToString() ?? UnknownValue()}");
    Console.WriteLine($"{T("Label_MemoryTotal", "Memory total")}: {FormatGibibytes(telemetry.MemoryTotalGiB)}");
    Console.WriteLine($"{T("Label_MemoryAvailable", "Memory available")}: {FormatGibibytes(telemetry.MemoryAvailableGiB)}");
    Console.WriteLine($"{T("Label_Source", "Source")}: {telemetry.Source}");

    if (telemetry.CpuFrequencies.Length > 0)
    {
        Console.WriteLine(T("Telemetry_CpuFrequencies", "CPU frequencies:"));
        foreach (var reading in telemetry.CpuFrequencies)
            Console.WriteLine($"  {reading.Name}: {reading.MHz:0.#} MHz ({reading.Source})");
    }

    if (telemetry.Temperatures.Length > 0)
    {
        Console.WriteLine(T("Telemetry_Temperatures", "Temperatures:"));
        foreach (var reading in telemetry.Temperatures)
            Console.WriteLine($"  {reading.Name}: {reading.Celsius:0.0} C ({reading.Source})");
    }

    if (telemetry.FanSpeeds.Length > 0)
    {
        Console.WriteLine(T("Telemetry_Fans", "Fans:"));
        foreach (var reading in telemetry.FanSpeeds)
            Console.WriteLine($"  {reading.Name}: {reading.Rpm} RPM ({reading.Source})");
    }

    foreach (var note in telemetry.Notes)
        Console.WriteLine($"{T("Label_Note", "Note")}: {note}");

    return 0;
}

static int PrintPower()
{
    var power = CrossPlatformStatus.Create().Power;

    Console.WriteLine(T("Power_Title", "Power status"));
    Console.WriteLine($"{T("Label_Source", "Source")}: {power.Source}");
    Console.WriteLine($"{T("Label_ExternalPower", "External power")}: {FormatBoolean(power.IsExternalPowerConnected)}");
    Console.WriteLine($"{T("Label_BatteryPresent", "Battery present")}: {FormatBoolean(power.HasBattery)}");

    if (power.Supplies.Length > 0)
    {
        Console.WriteLine(T("Power_Supplies", "Supplies:"));
        foreach (var supply in power.Supplies)
        {
            Console.WriteLine($"  {supply.Name} ({supply.Type})");
            Console.WriteLine($"    {T("Label_Status", "Status")}: {ValueOrUnknown(supply.Status)}");
            Console.WriteLine($"    {T("Label_Charge", "Charge")}: {FormatPercent(supply.ChargePercent)}");
            Console.WriteLine($"    {T("Label_Energy", "Energy")}: {FormatEnergy(supply.EnergyNowWh, supply.EnergyFullWh, supply.EnergyFullDesignWh)}");
            Console.WriteLine($"    {T("Label_PowerDraw", "Power draw")}: {FormatWatts(supply.PowerDrawW)}");
            Console.WriteLine($"    {T("Label_Voltage", "Voltage")}: {FormatVolts(supply.VoltageV)}");
            Console.WriteLine($"    {T("Label_CycleCount", "Cycle count")}: {supply.CycleCount?.ToString() ?? UnknownValue()}");
            Console.WriteLine($"    {T("Label_Online", "Online")}: {FormatBoolean(supply.IsOnline)}");
            Console.WriteLine($"    {T("Label_Present", "Present")}: {FormatBoolean(supply.IsPresent)}");
            Console.WriteLine($"    {T("Label_Health", "Health")}: {ValueOrUnknown(supply.Health)}");
        }
    }

    foreach (var note in power.Notes)
        Console.WriteLine($"{T("Label_Note", "Note")}: {note}");

    return 0;
}

static int PrintOrSetPowerProfile(IReadOnlyList<string> arguments)
{
    if (arguments.Count == 0)
        return PrintPowerProfile();

    var result = new PowerProfileWriter(new ProcessCommandRunner()).SetProfile(arguments[0]);
    Console.WriteLine(result.Succeeded
        ? T("PowerProfile_Changed", "Power profile changed")
        : T("PowerProfile_ChangeFailed", "Power profile change failed"));
    Console.WriteLine($"{T("Label_Profile", "Profile")}: {result.ProfileId}");
    Console.WriteLine($"{T("Label_Detail", "Detail")}: {result.Detail}");
    return result.Succeeded ? 0 : 1;
}

static int PrintPowerProfile()
{
    var profile = CrossPlatformStatus.Create().PowerProfile;

    Console.WriteLine(T("PowerProfile_Title", "Power profile"));
    Console.WriteLine($"{T("Label_Source", "Source")}: {profile.Source}");
    Console.WriteLine($"{T("Label_Active", "Active")}: {ValueOrUnknown(profile.ActiveProfile)}");
    Console.WriteLine($"{T("Label_CanSetProfile", "Can set profile")}: {FormatBoolean(profile.CanSetProfile)}");

    if (profile.AvailableProfiles.Length > 0)
    {
        Console.WriteLine(T("PowerProfile_Available", "Available profiles:"));
        foreach (var option in profile.AvailableProfiles)
            Console.WriteLine($"  [{FormatBoolean(option.IsActive)}] {option.Id} - {option.DisplayName}");
    }

    foreach (var note in profile.Notes)
        Console.WriteLine($"{T("Label_Note", "Note")}: {note}");

    return 0;
}

static int PrintControls()
{
    var controls = CrossPlatformStatus.Create().Controls;

    Console.WriteLine(T("Controls_Title", "Hardware controls"));
    Console.WriteLine($"{T("Label_Source", "Source")}: {controls.Source}");
    Console.WriteLine($"{T("Label_Controls", "Controls")}: {controls.Controls.Length}");

    foreach (var control in controls.Controls)
    {
        Console.WriteLine($"  {control.Id} - {control.DisplayName}");
        Console.WriteLine($"    {T("Label_Kind", "Kind")}: {control.Kind}");
        Console.WriteLine($"    {T("Label_Available", "Available")}: {FormatBoolean(control.IsAvailable)}");
        Console.WriteLine($"    {T("Label_Writable", "Writable")}: {FormatBoolean(control.IsWritable)}");
        Console.WriteLine($"    {T("Label_Current", "Current")}: {ValueOrUnknown(control.CurrentValue)}");
        Console.WriteLine($"    {T("Label_Detail", "Detail")}: {control.Detail}");
        if (control.Options.Length > 0)
        {
            Console.WriteLine($"    {T("Label_Options", "Options")}:");
            foreach (var option in control.Options)
                Console.WriteLine($"      [{FormatBoolean(option.IsActive)}] {option.Value} - {option.DisplayName}");
        }
    }

    foreach (var note in controls.Notes)
        Console.WriteLine($"{T("Label_Note", "Note")}: {note}");

    return 0;
}

static int SetControl(IReadOnlyList<string> arguments)
{
    if (arguments.Count < 2)
    {
        Console.Error.WriteLine(T("Set_Usage", "Usage: udt set <control-id> <value>"));
        return 2;
    }

    var result = new HardwareControlSurfaceWriter(new PhysicalFileSystem(), new ProcessCommandRunner()).Set(arguments[0], arguments[1]);
    Console.WriteLine(result.Succeeded
        ? T("Control_Changed", "Control changed")
        : T("Control_ChangeFailed", "Control change failed"));
    Console.WriteLine($"{T("Label_Control", "Control")}: {result.ControlId}");
    Console.WriteLine($"{T("Label_Value", "Value")}: {result.Value}");
    Console.WriteLine($"{T("Label_Detail", "Detail")}: {result.Detail}");
    return result.Succeeded ? 0 : 1;
}

static int Elevate(IReadOnlyList<string> arguments)
{
    var result = new ElevationLauncher().Launch(arguments);
    Console.WriteLine(result.Succeeded
        ? T("Elevation_Requested", "Elevation requested")
        : T("Elevation_Failed", "Elevation request failed"));
    Console.WriteLine($"{T("Label_Detail", "Detail")}: {result.Detail}");
    return result.Succeeded ? 0 : 1;
}

static int PrintSupport()
{
    var support = CrossPlatformStatus.Create().DeviceSupport;

    Console.WriteLine(T("Support_Title", "Device support"));
    Console.WriteLine($"{T("Label_Level", "Level")}: {support.SupportLevel}");
    Console.WriteLine($"{T("Label_Pack", "Pack")}: {support.DisplayName} ({support.DevicePackId})");
    Console.WriteLine($"{T("Label_Reason", "Reason")}: {support.Reason}");
    Console.WriteLine($"{T("Label_HardwareControls", "Hardware controls")}: {(support.IsHardwareControlAvailable ? T("Value_Available", "available") : T("Value_Hidden", "hidden"))}");
    Console.WriteLine($"{T("Label_EnabledFeatures", "Enabled features")}: {string.Join(", ", support.EnabledFeatures)}");
    Console.WriteLine($"{T("Label_HiddenFeatures", "Hidden features")}: {string.Join(", ", support.HiddenFeatures)}");
    return 0;
}

static int PrintDoctor()
{
    var report = CrossPlatformStatus.Create().Doctor;

    Console.WriteLine(T("Doctor_Title", "Doctor report"));
    Console.WriteLine($"{T("Label_Overall", "Overall")}: {report.OverallStatus}");
    foreach (var check in report.Checks)
        Console.WriteLine($"[{FormatDoctorStatus(check.Status)}] {LocalizeDoctorCheckName(check.Name)} - {check.LocalizedDetail?.Invoke() ?? check.Detail}");

    return report.OverallStatus == "fail" ? 1 : 0;
}

static string FormatDoctorStatus(DoctorCheckStatus status) => status switch
{
    DoctorCheckStatus.Pass => T("Doctor_Status_Pass", "pass"),
    DoctorCheckStatus.Warn => T("Doctor_Status_Warn", "warn"),
    DoctorCheckStatus.Fail => T("Doctor_Status_Fail", "fail"),
    _ => status.ToString().ToLowerInvariant(),
};

static string LocalizeDoctorCheckName(string name) => name switch
{
    "Runtime" => T("Doctor_Check_Runtime", name),
    "Hardware identity" => T("Doctor_Check_HardwareIdentity", name),
    "Read-only telemetry" => T("Doctor_Check_ReadOnlyTelemetry", name),
    "Power diagnostics" => T("Doctor_Check_PowerDiagnostics", name),
    "Power profile" => T("Doctor_Check_PowerProfile", name),
    "CPU governor" => T("Doctor_Check_CpuGovernor", name),
    "Battery charge limit" => T("Doctor_Check_BatteryChargeLimit", name),
    "Display brightness" => T("Doctor_Check_DisplayBrightness", name),
    "Control surface" => T("Doctor_Check_ControlSurface", name),
    "Device support" => T("Doctor_Check_DeviceSupport", name),
    "Hardware controls" => T("Doctor_Check_HardwareControls", name),
    _ => name,
};

static int PrintHelp()
{
    Console.WriteLine(T("Help_Title", "Cross-platform diagnostics"));
    Console.WriteLine();
    Console.WriteLine(T("Help_Usage", "Usage:"));
    Console.WriteLine($"  udt status    {T("Help_Status", "Print human-readable platform support status.")}");
    Console.WriteLine($"  udt json      {T("Help_Json", "Print platform support status as JSON.")}");
    Console.WriteLine($"  udt hardware  {T("Help_Hardware", "Print basic hardware identity for device-pack matching.")}");
    Console.WriteLine($"  udt telemetry {T("Help_Telemetry", "Print safe read-only CPU, memory, and temperature telemetry.")}");
    Console.WriteLine($"  udt power     {T("Help_Power", "Print safe read-only battery and external power status.")}");
    Console.WriteLine($"  udt profile   {T("Help_Profile", "Print platform power profile, or set it with a profile argument.")}");
    Console.WriteLine($"  udt controls  {T("Help_Controls", "Print writable and hidden cross-platform hardware controls.")}");
    Console.WriteLine($"  udt set <id> <value>  {T("Help_Set", "Set a writable cross-platform control.")}");
    Console.WriteLine($"  udt elevate <command> [arguments]  {T("Help_Elevate", "Restart a write command through Windows UAC when needed.")}");
    Console.WriteLine($"  udt support   {T("Help_Support", "Print safe basic-mode device support matching.")}");
    Console.WriteLine($"  udt doctor    {T("Help_Doctor", "Print aggregated cross-platform readiness checks.")}");
    Console.WriteLine($"  udt help      {T("Help_Help", "Show this help.")}");
    Console.WriteLine($"  udt --language <culture>  {T("Help_Language", "Select a language for this invocation.")}");
    Console.WriteLine();
    Console.WriteLine(T("Help_WindowsNote", "Windows hardware controls remain in the Windows desktop app. macOS and Linux support starts with diagnostics and safe basic-mode discovery."));
    return 0;
}

static string LocalizeSupportLevel(string value) => value switch
{
    "Windows desktop app and full hardware-control stack are available." =>
        T("SupportLevel_Windows", value),
    "Basic cross-platform diagnostics are available; vendor-specific hardware control is not enabled on this platform." =>
        T("SupportLevel_CrossPlatform", value),
    "Unsupported OS; diagnostics may be incomplete." =>
        T("SupportLevel_Unsupported", value),
    _ => value,
};

static string FormatHardwareSummary(HardwareIdentity hardware)
{
    var values = new[] { hardware.Vendor, hardware.Model }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();

    return values.Length == 0
        ? T("Summary_UnknownSource", "unknown ({0})", hardware.Source)
        : T("Summary_WithSource", "{0} ({1})", string.Join(' ', values), hardware.Source);
}

static string FormatTelemetrySummary(SystemTelemetry telemetry)
{
    var parts = new List<string>();
    if (!string.IsNullOrWhiteSpace(telemetry.CpuModel))
        parts.Add(telemetry.CpuModel);
    if (telemetry.MemoryTotalGiB is not null)
        parts.Add(T("Telemetry_MemorySummary", "{0:0.##} GiB RAM", telemetry.MemoryTotalGiB));
    if (telemetry.CpuFrequencies.Length > 0)
        parts.Add(T("Telemetry_FrequencySummary", "{0} CPU frequency readings", telemetry.CpuFrequencies.Length));
    if (telemetry.Temperatures.Length > 0)
        parts.Add(T("Telemetry_TemperatureSummary", "{0} temperature readings", telemetry.Temperatures.Length));
    if (telemetry.FanSpeeds.Length > 0)
        parts.Add(T("Telemetry_FanSummary", "{0} fan readings", telemetry.FanSpeeds.Length));

    return parts.Count == 0
        ? T("Summary_UnknownSource", "unknown ({0})", telemetry.Source)
        : T("Summary_WithSource", "{0} ({1})", string.Join(", ", parts), telemetry.Source);
}

static string FormatPowerSummary(PowerStatus power)
{
    var parts = new List<string>();
    if (power.HasBattery)
    {
        var battery = power.Supplies.First(supply => supply.Type.Equals("Battery", StringComparison.OrdinalIgnoreCase));
        var batteryParts = new List<string> { ValueOrUnknown(battery.Status) };
        if (battery.ChargePercent is not null)
            batteryParts.Add($"{battery.ChargePercent:0.#}%");
        parts.Add(T("Power_BatterySummary", "battery {0}", string.Join(' ', batteryParts.Where(part => !string.IsNullOrWhiteSpace(part)))));
    }

    if (power.IsExternalPowerConnected is not null)
        parts.Add(power.IsExternalPowerConnected.Value
            ? T("Power_ExternalConnected", "external power connected")
            : T("Power_ExternalOffline", "external power offline"));

    return parts.Count == 0
        ? T("Summary_UnknownSource", "unknown ({0})", power.Source)
        : T("Summary_WithSource", "{0} ({1})", string.Join(", ", parts), power.Source);
}

static string FormatPowerProfileSummary(PowerProfileStatus profile)
{
    if (!string.IsNullOrWhiteSpace(profile.ActiveProfile))
        return T("Summary_WithSource", "{0} ({1})", profile.ActiveProfile, profile.Source);

    return T("Summary_UnknownSource", "unknown ({0})", profile.Source);
}

static string FormatCpuGovernorSummary(CpuGovernorStatus governor)
{
    if (!string.IsNullOrWhiteSpace(governor.ActiveGovernor))
        return T("CpuGovernor_Summary", "{0} across {1} policies ({2})",
            governor.ActiveGovernor, governor.Policies.Length, governor.Source);

    return T("Summary_UnknownSource", "unknown ({0})", governor.Source);
}

static string FormatBatteryChargeLimitSummary(BatteryChargeLimitStatus chargeLimit)
{
    var device = chargeLimit.Devices.FirstOrDefault();
    return device?.EndThreshold is null
        ? T("Summary_UnknownSource", "unknown ({0})", chargeLimit.Source)
        : T("BatteryChargeLimit_Summary", "{0}% on {1} ({2})", device.EndThreshold, device.Id, chargeLimit.Source);
}

static string FormatDisplayBrightnessSummary(DisplayBrightnessStatus brightness)
{
    var device = brightness.Devices.FirstOrDefault();
    return device is null
        ? T("Summary_UnknownSource", "unknown ({0})", brightness.Source)
        : T("DisplayBrightness_Summary", "{0}% on {1} ({2})", device.Percent, device.Id, brightness.Source);
}

static string FormatControlSummary(HardwareControlSurface controls)
{
    var writable = controls.Controls.Count(control => control.IsWritable);
    var available = controls.Controls.Count(control => control.IsAvailable);
    return T("Controls_Summary", "{0} available, {1} writable ({2})", available, writable, controls.Source);
}

static string UnknownValue() => T("Value_Unknown", "unknown");

static string ValueOrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? UnknownValue() : value;

static string FormatGibibytes(double? value) => value is null
    ? UnknownValue()
    : T("Format_Gibibytes", "{0:0.##} GiB", value);

static string FormatPercent(double? value) => value is null
    ? UnknownValue()
    : T("Format_Percent", "{0:0.#}%", value);

static string FormatWatts(double? value) => value is null
    ? UnknownValue()
    : T("Format_Watts", "{0:0.##} W", value);

static string FormatVolts(double? value) => value is null
    ? UnknownValue()
    : T("Format_Volts", "{0:0.##} V", value);

static string FormatBoolean(bool? value) => value is null
    ? UnknownValue()
    : value.Value ? T("Value_Yes", "yes") : T("Value_No", "no");

static string FormatEnergy(double? nowWh, double? fullWh, double? designWh)
{
    var parts = new List<string>();
    if (nowWh is not null)
        parts.Add(T("Format_EnergyNow", "{0:0.##} Wh now", nowWh));
    if (fullWh is not null)
        parts.Add(T("Format_EnergyFull", "{0:0.##} Wh full", fullWh));
    if (designWh is not null)
        parts.Add(T("Format_EnergyDesign", "{0:0.##} Wh design", designWh));

    return parts.Count == 0 ? UnknownValue() : string.Join(", ", parts);
}

static int PrintUnknownCommand(string command)
{
    Console.Error.WriteLine(string.Format(
        T("Error_UnknownCommand", "Unknown command '{0}'. Run 'udt help'."), command));
    return 2;
}

internal sealed record CrossPlatformStatus(
    string ProductName,
    string Version,
    string OsDescription,
    string Architecture,
    string MachineName,
    string DotNetRuntime,
    HardwareIdentity Hardware,
    SystemTelemetry Telemetry,
    PowerStatus Power,
    PowerProfileStatus PowerProfile,
    CpuGovernorStatus CpuGovernor,
    BatteryChargeLimitStatus BatteryChargeLimit,
    DisplayBrightnessStatus DisplayBrightness,
    HardwareControlSurface Controls,
    DeviceSupportStatus DeviceSupport,
    DoctorReport Doctor,
    string SupportLevel,
    CapabilityStatus[] Capabilities)
{
    public static CrossPlatformStatus Create(IDeviceAdapter? adapter = null)
    {
        var isWindows = System.OperatingSystem.IsWindows();
        var isMacOS = System.OperatingSystem.IsMacOS();
        var isLinux = System.OperatingSystem.IsLinux();
        var supportLevel = isWindows
            ? "Windows generic device diagnostics are available; verified vendor hardware controls remain conditional."
            : isMacOS || isLinux
                ? "Basic cross-platform diagnostics are available; vendor-specific hardware control is not enabled on this platform."
                : "Unsupported OS; diagnostics may be incomplete.";

        var deviceSnapshot = CrossPlatformDeviceAdapterFactory.ReadSnapshot(isWindows, isMacOS, adapter);
        var hardware = deviceSnapshot is null
            ? new HardwareIdentityReader(
                new PhysicalFileSystem(),
                new ProcessCommandRunner()).Read()
            : CrossPlatformDeviceAdapterFactory.ToHardwareIdentity(deviceSnapshot.Identity);
        var telemetry = new SystemTelemetryReader(
            new PhysicalFileSystem(),
            new ProcessCommandRunner()).Read();
        var power = new PowerStatusReader(
            new PhysicalFileSystem(),
            new ProcessCommandRunner()).Read();
        var powerProfile = new PowerProfileReader(
            new ProcessCommandRunner()).Read();
        var cpuGovernor = new CpuGovernorReader(
            new PhysicalFileSystem()).Read();
        var batteryChargeLimit = new BatteryChargeLimitReader(
            new PhysicalFileSystem()).Read();
        var displayBrightness = new DisplayBrightnessReader(
            new PhysicalFileSystem()).Read();
        var deviceSupport = deviceSnapshot is null
            ? new CrossPlatformDeviceSupportEvaluator().Evaluate(hardware, isWindows)
            : DeviceSupportStatus.From(deviceSnapshot.Support);
        var controls = new HardwareControlSurfaceReader(powerProfile, cpuGovernor, batteryChargeLimit, displayBrightness, deviceSupport).Read();
        var status = new CrossPlatformStatus(
            "Universal Device Toolkit",
            GetVersion(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            Environment.MachineName,
            RuntimeInformation.FrameworkDescription,
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
            BuildCapabilities(isWindows, isMacOS, isLinux));

        return status with { Doctor = DoctorReport.Create(status) };
    }

    private static CapabilityStatus[] BuildCapabilities(bool isWindows, bool isMacOS, bool isLinux) =>
    [
        Capability("CrossPlatformCli", "Cross-platform CLI", true, "This net10.0 entry point runs without WindowsDesktop, WPF, WMI, registry, or Win32 APIs."),
        Capability("MachineDiagnostics", "Machine diagnostics", true, "Reports OS, architecture, machine name, and .NET runtime."),
        Capability("HardwareIdentity", "Hardware identity", true, "Reads Windows WMI, Linux DMI, or macOS system profiler identity when available; avoids privileged hardware writes."),
        Capability("ReadOnlyTelemetry", "Read-only telemetry", true, "Reads Windows WMI, Linux procfs/sysfs, or macOS sysctl CPU and memory telemetry where available."),
        Capability("PowerDiagnostics", "Power diagnostics", true, "Reads Linux power_supply or macOS pmset battery and external power status without changing hardware state."),
        Capability("PlatformPowerProfiles", "Platform power profiles", isMacOS || isLinux,
            isLinux
                ? "Can inspect and set Linux power-profiles-daemon profiles through powerprofilesctl."
                : isMacOS
                    ? "Can inspect and set macOS low power mode through pmset."
                    : "Use the Windows desktop app for Windows power mode and Lenovo thermal mode integration.",
            isLinux ? "Capability_PlatformPowerProfiles_Linux_Detail" : isMacOS ? "Capability_PlatformPowerProfiles_Mac_Detail" : "Capability_PlatformPowerProfiles_Windows_Detail"),
        Capability("CpuGovernor", "CPU governor", isLinux,
            isLinux
                ? "Can inspect and set Linux CPU frequency governors through /sys/devices/system/cpu."
                : "Linux CPU governor control is not available on this platform.",
            isLinux ? "Capability_CpuGovernor_Linux_Detail" : "Capability_CpuGovernor_Other_Detail"),
        Capability("BatteryChargeLimit", "Battery charge limit", isLinux,
            isLinux
                ? "Can inspect and set Linux battery charge thresholds through /sys/class/power_supply."
                : "Linux battery charge threshold control is not available on this platform.",
            isLinux ? "Capability_BatteryChargeLimit_Linux_Detail" : "Capability_BatteryChargeLimit_Other_Detail"),
        Capability("DisplayBrightness", "Display brightness", isLinux,
            isLinux
                ? "Can inspect and set Linux backlight brightness through /sys/class/backlight."
                : "Linux backlight control is not available on this platform.",
            isLinux ? "Capability_DisplayBrightness_Linux_Detail" : "Capability_DisplayBrightness_Other_Detail"),
        Capability("CrossPlatformControlSurface", "Cross-platform control surface", true, "Lists writable standard OS controls and hidden vendor-specific controls through one metadata surface."),
        Capability("BasicModeCompatibility", "Basic-mode compatibility", true, "Matches common vendors to safe basic device packs and hides hardware-write features on non-Windows platforms."),
        Capability("WindowsHardwareControls", "Windows hardware controls", isWindows,
            isWindows
                ? "Use the Windows desktop app or existing udt CLI for Lenovo hardware controls."
                : "Windows-only controls are intentionally hidden on macOS/Linux.",
            isWindows ? "Capability_WindowsHardwareControls_Windows_Detail" : "Capability_WindowsHardwareControls_Other_Detail"),
        Capability("LinuxDiagnostics", "Linux diagnostics", isLinux,
            isLinux ? "Running on Linux; safe diagnostics are enabled." : "Not running on Linux.",
            isLinux ? "Capability_LinuxDiagnostics_Linux_Detail" : "Capability_LinuxDiagnostics_Other_Detail"),
        Capability("MacOsDiagnostics", "macOS diagnostics", isMacOS,
            isMacOS ? "Running on macOS; safe diagnostics are enabled." : "Not running on macOS.",
            isMacOS ? "Capability_MacOsDiagnostics_Mac_Detail" : "Capability_MacOsDiagnostics_Other_Detail")
    ];

    private static CapabilityStatus Capability(
        string key,
        string name,
        bool available,
        string detail,
        string? detailKey = null) =>
        new(name, available, detail, $"Capability_{key}_Name", detailKey ?? $"Capability_{key}_Detail");

    private static string GetVersion() =>
        typeof(CrossPlatformStatus).Assembly.GetName().Version?.ToString() ?? "unknown";
}

internal static class CrossPlatformDeviceAdapterFactory
{
    public static DeviceSnapshot? ReadSnapshot(bool isWindows, bool isMacOS, IDeviceAdapter? adapter = null)
    {
        if (adapter is null && isWindows)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            adapter = new WindowsDeviceAdapter();
        }
        else if (adapter is null && isMacOS)
        {
            adapter = new MacOSDeviceAdapter();
        }

        try
        {
            return adapter?.ReadSnapshotAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    public static HardwareIdentity ToHardwareIdentity(DeviceIdentity identity) =>
        new(
            identity.Vendor,
            identity.Model,
            identity.ProductName,
            identity.SerialNumber,
            identity.Source);
}

internal sealed record CapabilityStatus(
    string Name,
    bool Available,
    string Detail,
    [property: JsonIgnore] string NameKey,
    [property: JsonIgnore] string DetailKey);

internal sealed record LanguageArguments(CultureInfo? OverrideCulture, IReadOnlyList<string> Remaining)
{
    public static LanguageArguments Parse(IReadOnlyList<string> args)
    {
        var remaining = new List<string>();
        CultureInfo? culture = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--language=", StringComparison.OrdinalIgnoreCase))
            {
                culture = LocalizationCatalog.NormalizeCulture(argument["--language=".Length..]);
                continue;
            }

            if (argument.Equals("--language", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count)
            {
                culture = LocalizationCatalog.NormalizeCulture(args[++index]);
                continue;
            }

            remaining.Add(argument);
        }

        return new LanguageArguments(culture, remaining);
    }
}

internal static class CrossPlatformStrings
{
    private static readonly ResourceManagerStringLocalizer Localizer = new(
        new ResourceManager(
            "UniversalDeviceToolkit.CrossPlatform.Resources.Resource",
            typeof(CrossPlatformStrings).Assembly));

    static CrossPlatformStrings() => Localizer.CurrentCulture = LocalizationRuntime.CurrentCulture;

    public static string Get(string key, string fallback)
    {
        Localizer.CurrentCulture = LocalizationRuntime.CurrentCulture;
        return Localizer.GetString(key, fallback);
    }

    public static string Format(string key, string fallback, params object[] args)
    {
        var template = Get(key, fallback);
        return args.Length == 0
            ? template
            : string.Format(Localizer.CurrentCulture, template, args);
    }
}
