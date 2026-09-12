using System.Globalization;
using System.Text.Json.Serialization;

internal sealed record DoctorReport(
    string OverallStatus,
    DoctorCheck[] Checks)
{
    public static DoctorReport CreatePlaceholder() => new("pending", []);

    public static DoctorReport Create(CrossPlatformStatus status)
    {
        var checks = new List<DoctorCheck>
        {
            CheckRuntime(status),
            CheckHardwareIdentity(status.Hardware),
            CheckTelemetry(status.Telemetry),
            CheckPower(status.Power),
            CheckPowerProfile(status.PowerProfile),
            CheckCpuGovernor(status.CpuGovernor),
            CheckBatteryChargeLimit(status.BatteryChargeLimit),
            CheckDisplayBrightness(status.DisplayBrightness),
            CheckControls(status.Controls),
            CheckDeviceSupport(status.DeviceSupport),
            CheckHardwareControls(status)
        };

        var overallStatus = checks.Any(check => check.Status == DoctorCheckStatus.Fail)
            ? "fail"
            : checks.Any(check => check.Status == DoctorCheckStatus.Warn)
                ? "warn"
                : "pass";

        return new DoctorReport(overallStatus, checks.ToArray());
    }

    private static DoctorCheck CheckRuntime(CrossPlatformStatus status) =>
        new(
            "Runtime",
            DoctorCheckStatus.Pass,
            $"{status.OsDescription}; {status.Architecture}; {status.DotNetRuntime}",
            () => CrossPlatformStrings.Format(
                "Doctor_Detail_Runtime",
                "{0}; {1}; {2}",
                status.OsDescription,
                status.Architecture,
                status.DotNetRuntime));

    private static DoctorCheck CheckHardwareIdentity(HardwareIdentity hardware)
    {
        var hasIdentity = !string.IsNullOrWhiteSpace(hardware.Vendor) ||
                          !string.IsNullOrWhiteSpace(hardware.Model) ||
                          !string.IsNullOrWhiteSpace(hardware.ProductName);

        if (hasIdentity)
        {
            var vendor = FirstPresent(hardware.Vendor, "unknown vendor");
            var model = FirstPresent(hardware.Model, hardware.ProductName, "unknown model");
            return new DoctorCheck(
                "Hardware identity",
                DoctorCheckStatus.Pass,
                $"{vendor} {model} from {hardware.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_HardwareIdentity",
                    "{0} {1} from {2}.",
                    vendor,
                    model,
                    hardware.Source));
        }

        return new DoctorCheck(
            "Hardware identity",
            DoctorCheckStatus.Warn,
            $"No hardware identity was readable from {hardware.Source}.",
            () => CrossPlatformStrings.Format(
                "Doctor_Detail_NoHardwareIdentity",
                "No hardware identity was readable from {0}.",
                hardware.Source));
    }

    private static DoctorCheck CheckTelemetry(SystemTelemetry telemetry)
    {
        var hasTelemetry = !string.IsNullOrWhiteSpace(telemetry.CpuModel) ||
                           telemetry.MemoryTotalGiB is not null ||
                           telemetry.CpuFrequencies.Length > 0 ||
                           telemetry.Temperatures.Length > 0 ||
                           telemetry.FanSpeeds.Length > 0;

        if (hasTelemetry)
        {
            var details = new List<string>();
            var localizedDetails = new List<Func<string>>();
            if (!string.IsNullOrWhiteSpace(telemetry.CpuModel))
            {
                details.Add(telemetry.CpuModel);
                localizedDetails.Add(() => telemetry.CpuModel!);
            }
            if (telemetry.MemoryTotalGiB is not null)
            {
                details.Add($"{telemetry.MemoryTotalGiB:0.##} GiB RAM");
                localizedDetails.Add(() => CrossPlatformStrings.Format(
                    "Doctor_Detail_Memory",
                    "{0} GiB RAM",
                    telemetry.MemoryTotalGiB.Value.ToString("0.##", CultureInfo.InvariantCulture)));
            }
            if (telemetry.CpuFrequencies.Length > 0)
            {
                details.Add($"{telemetry.CpuFrequencies.Length} CPU frequency readings");
                localizedDetails.Add(() => CrossPlatformStrings.Format(
                    "Doctor_Detail_CpuFrequencies",
                    "{0} CPU frequency readings",
                    telemetry.CpuFrequencies.Length));
            }
            if (telemetry.Temperatures.Length > 0)
            {
                details.Add($"{telemetry.Temperatures.Length} temperature readings");
                localizedDetails.Add(() => CrossPlatformStrings.Format(
                    "Doctor_Detail_Temperatures",
                    "{0} temperature readings",
                    telemetry.Temperatures.Length));
            }
            if (telemetry.FanSpeeds.Length > 0)
            {
                details.Add($"{telemetry.FanSpeeds.Length} fan readings");
                localizedDetails.Add(() => CrossPlatformStrings.Format(
                    "Doctor_Detail_Fans",
                    "{0} fan readings",
                    telemetry.FanSpeeds.Length));
            }

            return new DoctorCheck(
                "Read-only telemetry",
                DoctorCheckStatus.Pass,
                $"{string.Join(", ", details)} from {telemetry.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_TelemetryAvailable",
                    "{0} from {1}.",
                    string.Join(CrossPlatformStrings.Get("Doctor_Detail_Separator", ", "), localizedDetails.Select(detail => detail())),
                    telemetry.Source));
        }

        var note = telemetry.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
        return string.IsNullOrWhiteSpace(note)
            ? new DoctorCheck(
                "Read-only telemetry",
                DoctorCheckStatus.Warn,
                $"No read-only telemetry was available from {telemetry.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_NoTelemetry",
                    "No read-only telemetry was available from {0}.",
                    telemetry.Source))
            : Note("Read-only telemetry", DoctorCheckStatus.Warn, note);
    }

    private static DoctorCheck CheckPower(PowerStatus power)
    {
        if (power.Supplies.Length == 0)
        {
            var note = power.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
            return string.IsNullOrWhiteSpace(note)
                ? new DoctorCheck(
                    "Power diagnostics",
                    DoctorCheckStatus.Warn,
                    $"No power status was available from {power.Source}.",
                    () => CrossPlatformStrings.Format(
                        "Doctor_Detail_NoPowerStatus",
                        "No power status was available from {0}.",
                        power.Source))
                : Note("Power diagnostics", DoctorCheckStatus.Warn, note);
        }

        var details = new List<string>();
        var localizedDetails = new List<Func<string>>();
        if (power.HasBattery)
        {
            var battery = power.Supplies.First(supply => supply.Type.Equals("Battery", StringComparison.OrdinalIgnoreCase));
            var batteryState = FirstPresent(battery.Status, "battery");
            if (battery.ChargePercent is null)
            {
                details.Add(batteryState);
                localizedDetails.Add(() => batteryState);
            }
            else
            {
                details.Add($"{batteryState} {battery.ChargePercent:0.#}%");
                localizedDetails.Add(() => CrossPlatformStrings.Format(
                    "Doctor_Detail_BatteryWithCharge",
                    "{0} {1}%",
                    batteryState,
                    battery.ChargePercent.Value.ToString("0.#", CultureInfo.InvariantCulture)));
            }
        }

        if (power.IsExternalPowerConnected is not null)
        {
            var connected = power.IsExternalPowerConnected.Value;
            details.Add(connected ? "external power connected" : "external power offline");
            localizedDetails.Add(() => CrossPlatformStrings.Get(
                connected ? "Doctor_Detail_ExternalPowerConnected" : "Doctor_Detail_ExternalPowerOffline",
                connected ? "external power connected" : "external power offline"));
        }

        if (details.Count == 0)
        {
            details.Add($"{power.Supplies.Length} power supplies");
            localizedDetails.Add(() => CrossPlatformStrings.Format(
                "Doctor_Detail_PowerSupplies",
                "{0} power supplies",
                power.Supplies.Length));
        }

        return new DoctorCheck(
            "Power diagnostics",
            DoctorCheckStatus.Pass,
            $"{string.Join(", ", details)} from {power.Source}.",
            () => CrossPlatformStrings.Format(
                "Doctor_Detail_PowerAvailable",
                "{0} from {1}.",
                string.Join(CrossPlatformStrings.Get("Doctor_Detail_Separator", ", "), localizedDetails.Select(detail => detail())),
                power.Source));
    }

    private static DoctorCheck CheckPowerProfile(PowerProfileStatus profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.ActiveProfile))
        {
            var writable = profile.CanSetProfile ? "settable" : "read-only";
            return new DoctorCheck(
                "Power profile",
                DoctorCheckStatus.Pass,
                $"{profile.ActiveProfile} from {profile.Source}; {writable}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_PowerProfileAvailable",
                    "{0} from {1}; {2}.",
                    profile.ActiveProfile,
                    profile.Source,
                    CrossPlatformStrings.Get(
                        profile.CanSetProfile ? "Doctor_Value_Settable" : "Doctor_Value_ReadOnly",
                        writable)));
        }

        var note = profile.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
        return string.IsNullOrWhiteSpace(note)
            ? new DoctorCheck(
                "Power profile",
                DoctorCheckStatus.Warn,
                $"No platform power profile was available from {profile.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_NoPowerProfile",
                    "No platform power profile was available from {0}.",
                    profile.Source))
            : Note("Power profile", DoctorCheckStatus.Warn, note);
    }

    private static DoctorCheck CheckCpuGovernor(CpuGovernorStatus governor)
    {
        if (governor.Policies.Length > 0 && !string.IsNullOrWhiteSpace(governor.ActiveGovernor))
        {
            var writable = governor.CanSetGovernor ? "settable" : "read-only";
            return new DoctorCheck(
                "CPU governor",
                DoctorCheckStatus.Pass,
                $"{governor.ActiveGovernor} across {governor.Policies.Length} policies from {governor.Source}; {writable}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_GovernorAvailable",
                    "{0} across {1} policies from {2}; {3}.",
                    governor.ActiveGovernor,
                    governor.Policies.Length,
                    governor.Source,
                    CrossPlatformStrings.Get(
                        governor.CanSetGovernor ? "Doctor_Value_Settable" : "Doctor_Value_ReadOnly",
                        writable)));
        }

        var note = governor.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
        return string.IsNullOrWhiteSpace(note)
            ? new DoctorCheck(
                "CPU governor",
                DoctorCheckStatus.Warn,
                $"No CPU governor provider was available from {governor.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_NoGovernor",
                    "No CPU governor provider was available from {0}.",
                    governor.Source))
            : Note("CPU governor", DoctorCheckStatus.Warn, note);
    }

    private static DoctorCheck CheckBatteryChargeLimit(BatteryChargeLimitStatus chargeLimit)
    {
        var device = chargeLimit.Devices.FirstOrDefault();
        if (device?.EndThreshold is not null)
        {
            return new DoctorCheck(
                "Battery charge limit",
                DoctorCheckStatus.Pass,
                $"{device.DisplayName} ends charging at {device.EndThreshold}% from {chargeLimit.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_ChargeLimitAvailable",
                    "{0} ends charging at {1}% from {2}.",
                    device.DisplayName,
                    device.EndThreshold.Value,
                    chargeLimit.Source));
        }

        var note = chargeLimit.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
        return string.IsNullOrWhiteSpace(note)
            ? new DoctorCheck(
                "Battery charge limit",
                DoctorCheckStatus.Warn,
                $"No battery charge limit provider was available from {chargeLimit.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_NoChargeLimit",
                    "No battery charge limit provider was available from {0}.",
                    chargeLimit.Source))
            : Note("Battery charge limit", DoctorCheckStatus.Warn, note);
    }

    private static DoctorCheck CheckDisplayBrightness(DisplayBrightnessStatus brightness)
    {
        var device = brightness.Devices.FirstOrDefault();
        if (device is not null)
        {
            return new DoctorCheck(
                "Display brightness",
                DoctorCheckStatus.Pass,
                $"{device.DisplayName} is at {device.Percent}% from {brightness.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_BrightnessAvailable",
                    "{0} is at {1}% from {2}.",
                    device.DisplayName,
                    device.Percent,
                    brightness.Source));
        }

        var note = brightness.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
        return string.IsNullOrWhiteSpace(note)
            ? new DoctorCheck(
                "Display brightness",
                DoctorCheckStatus.Warn,
                $"No display brightness provider was available from {brightness.Source}.",
                () => CrossPlatformStrings.Format(
                    "Doctor_Detail_NoBrightness",
                    "No display brightness provider was available from {0}.",
                    brightness.Source))
            : Note("Display brightness", DoctorCheckStatus.Warn, note);
    }

    private static DoctorCheck CheckControls(HardwareControlSurface controls)
    {
        if (controls.Controls.Length == 0)
        {
            var note = controls.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
            return string.IsNullOrWhiteSpace(note)
                ? new DoctorCheck(
                    "Control surface",
                    DoctorCheckStatus.Warn,
                    "No cross-platform controls were reported.",
                    () => CrossPlatformStrings.Get("Doctor_Detail_NoControls", "No cross-platform controls were reported."))
                : Note("Control surface", DoctorCheckStatus.Warn, note);
        }

        var available = controls.Controls.Count(control => control.IsAvailable);
        var writable = controls.Controls.Count(control => control.IsWritable);
        var status = writable > 0 ? DoctorCheckStatus.Pass : DoctorCheckStatus.Warn;
        return new DoctorCheck(
            "Control surface",
            status,
            $"{available} available controls; {writable} writable controls from {controls.Source}.",
            () => CrossPlatformStrings.Format(
                "Doctor_Detail_ControlsAvailable",
                "{0} available controls; {1} writable controls from {2}.",
                available,
                writable,
                controls.Source));
    }

    private static DoctorCheck CheckDeviceSupport(DeviceSupportStatus support)
    {
        var status = support.DevicePackId.Equals("generic-pc-basic", StringComparison.OrdinalIgnoreCase)
            ? DoctorCheckStatus.Warn
            : DoctorCheckStatus.Pass;
        var detail = $"{support.DisplayName}: {support.Reason}";
        return new DoctorCheck(
            "Device support",
            status,
            detail,
            () => CrossPlatformStrings.Format(
                "Doctor_Detail_DeviceSupport",
                "{0}: {1}",
                support.DisplayName,
                LocalizeKnownNote(support.Reason)));
    }

    private static DoctorCheck CheckHardwareControls(CrossPlatformStatus status)
    {
        if (status.DeviceSupport.IsHardwareControlAvailable)
        {
            return new DoctorCheck(
                "Hardware controls",
                DoctorCheckStatus.Pass,
                "Hardware controls are available.",
                () => CrossPlatformStrings.Get(
                    "Doctor_Detail_HardwareControlsAvailable",
                    "Hardware controls are available."));
        }

        var detail = status.SupportLevel.StartsWith("Windows desktop", StringComparison.OrdinalIgnoreCase)
            ? "Use the Windows desktop app or udt-cli.exe for supported hardware controls."
            : "Hardware-write controls are intentionally hidden on macOS/Linux.";

        return new DoctorCheck(
            "Hardware controls",
            DoctorCheckStatus.Warn,
            detail,
            () => CrossPlatformStrings.Get(
                status.SupportLevel.StartsWith("Windows desktop", StringComparison.OrdinalIgnoreCase)
                    ? "Doctor_Detail_WindowsHardwareControls"
                    : "Doctor_Detail_HardwareControlsHidden",
                detail));
    }

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static DoctorCheck Note(string name, DoctorCheckStatus status, string note) =>
        new(name, status, note, () => LocalizeKnownNote(note));

    private static string LocalizeKnownNote(string note) => note switch
    {
        "No cross-platform telemetry provider is available for this OS." =>
            CrossPlatformStrings.Get("Doctor_Note_NoTelemetryProvider", note),
        "No cross-platform power provider is available for this OS." =>
            CrossPlatformStrings.Get("Doctor_Note_NoPowerProvider", note),
        "No cross-platform power profile provider is available for this OS." =>
            CrossPlatformStrings.Get("Doctor_Note_NoPowerProfileProvider", note),
        "No cross-platform CPU governor provider is available for this OS." =>
            CrossPlatformStrings.Get("Doctor_Note_NoGovernorProvider", note),
        "No cross-platform battery charge limit provider is available for this OS." =>
            CrossPlatformStrings.Get("Doctor_Note_NoChargeLimitProvider", note),
        "No cross-platform display brightness provider is available for this OS." =>
            CrossPlatformStrings.Get("Doctor_Note_NoBrightnessProvider", note),
        "No cross-platform controls were reported." =>
            CrossPlatformStrings.Get("Doctor_Detail_NoControls", note),
        "No cross-platform device pack matched the hardware identity." =>
            CrossPlatformStrings.Get("Doctor_Note_NoDevicePack", note),
        _ => note,
    };
}

internal sealed record DoctorCheck(
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    DoctorCheckStatus Status,
    string Detail,
    [property: JsonIgnore] Func<string>? LocalizedDetail = null);

internal enum DoctorCheckStatus
{
    Pass,
    Warn,
    Fail
}
