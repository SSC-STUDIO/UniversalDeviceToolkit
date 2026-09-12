internal sealed record HardwareControlSurface(
    string Source,
    HardwareControlDescriptor[] Controls,
    string[] Notes)
{
    public static HardwareControlSurface Unknown(string source, params string[] notes) =>
        new(source, [], notes);
}

internal sealed record HardwareControlDescriptor(
    string Id,
    string DisplayName,
    string Kind,
    bool IsAvailable,
    bool IsWritable,
    string CurrentValue,
    HardwareControlOption[] Options,
    string Detail);

internal sealed record HardwareControlOption(
    string Value,
    string DisplayName,
    bool IsActive);

internal sealed record HardwareControlSetResult(
    bool Succeeded,
    string ControlId,
    string Value,
    string Detail);

internal sealed class HardwareControlSurfaceReader(
    PowerProfileStatus powerProfile,
    CpuGovernorStatus cpuGovernor,
    BatteryChargeLimitStatus batteryChargeLimit,
    DisplayBrightnessStatus displayBrightness,
    DeviceSupportStatus deviceSupport)
{
    public HardwareControlSurface Read()
    {
        var isWindows = OperatingSystem.IsWindows();
        var controls = new List<HardwareControlDescriptor>
        {
            BuildPowerProfileControl(powerProfile),
            BuildCpuGovernorControl(cpuGovernor),
            BuildBatteryChargeLimitControl(batteryChargeLimit),
            BuildDisplayBrightnessControl(displayBrightness),
            BuildVendorHardwareControls(deviceSupport, isWindows)
        };

        string[] notes = isWindows
            ? ["Windows vendor controls remain available through the desktop app and udt-cli.exe; this CLI exposes cross-platform control metadata."]
            : ["Only standardized OS controls are writable on macOS/Linux. Vendor-specific hardware writes remain hidden unless a platform backend is added."];

        return new HardwareControlSurface(
            isWindows ? "windows-cross-platform-cli" : "cross-platform-control-surface",
            controls.ToArray(),
            notes);
    }

    private static HardwareControlDescriptor BuildPowerProfileControl(PowerProfileStatus profile) =>
        new(
            "power-profile",
            "Platform power profile",
            "standard-os",
            !string.IsNullOrWhiteSpace(profile.ActiveProfile) || profile.CanSetProfile || profile.AvailableProfiles.Length > 0,
            profile.CanSetProfile,
            profile.ActiveProfile,
            profile.AvailableProfiles
                .Select(option => new HardwareControlOption(option.Id, option.DisplayName, option.IsActive))
                .ToArray(),
            profile.CanSetProfile
                ? "Set through Linux powerprofilesctl or macOS pmset where available."
                : FirstPresent(profile.Notes));

    private static HardwareControlDescriptor BuildCpuGovernorControl(CpuGovernorStatus governor) =>
        new(
            "cpu-governor",
            "CPU governor",
            "standard-os",
            governor.Policies.Length > 0,
            governor.CanSetGovernor,
            governor.ActiveGovernor,
            governor.AvailableGovernors
                .Select(option => new HardwareControlOption(option.Id, option.DisplayName, option.IsActive))
                .ToArray(),
            governor.CanSetGovernor
                ? $"Set through Linux cpufreq across {governor.Policies.Length} policies."
                : FirstPresent(governor.Notes));

    private static HardwareControlDescriptor BuildBatteryChargeLimitControl(BatteryChargeLimitStatus chargeLimit)
    {
        var device = chargeLimit.Devices.FirstOrDefault();
        return new HardwareControlDescriptor(
            "battery-charge-limit",
            "Battery charge limit",
            "standard-os",
            device is not null,
            device?.EndThreshold is not null,
            device?.EndThreshold is null ? string.Empty : $"{device.EndThreshold}%",
            [],
            device?.EndThreshold is not null
                ? $"Set through Linux power_supply threshold for {device.Id}."
                : FirstPresent(chargeLimit.Notes));
    }

    private static HardwareControlDescriptor BuildDisplayBrightnessControl(DisplayBrightnessStatus brightness)
    {
        var device = brightness.Devices.FirstOrDefault();
        return new HardwareControlDescriptor(
            "display-brightness",
            "Display brightness",
            "standard-os",
            device is not null,
            device is not null,
            device is null ? string.Empty : $"{device.Percent}%",
            [],
            device is null
                ? FirstPresent(brightness.Notes)
                : $"Set through Linux backlight device {device.Id}.");
    }

    private static HardwareControlDescriptor BuildVendorHardwareControls(DeviceSupportStatus deviceSupport, bool isWindows) =>
        new(
            "vendor-hardware-controls",
            "Vendor hardware controls",
            "vendor-specific",
            deviceSupport.IsHardwareControlAvailable,
            false,
            deviceSupport.IsHardwareControlAvailable ? "available in desktop app" : "hidden",
            [],
            isWindows
                ? "Use the Windows desktop app or udt-cli.exe for vendor-specific controls."
                : "Hidden on macOS/Linux until a vendor-specific backend is implemented.");

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

internal sealed class HardwareControlSurfaceWriter(
    IFileSystem fileSystem,
    ICommandResultRunner commandRunner,
    CrossPlatformControlPlatform platform = CrossPlatformControlPlatform.Auto)
{
    public HardwareControlSetResult Set(string controlId, string value)
    {
        var normalizedControlId = NormalizeControlId(controlId);
        if (normalizedControlId.Equals("cpu-governor", StringComparison.OrdinalIgnoreCase))
            return new CpuGovernorWriter(fileSystem, commandRunner, ResolvePlatform()).SetGovernor(value);

        if (normalizedControlId.Equals("battery-charge-limit", StringComparison.OrdinalIgnoreCase))
            return new BatteryChargeLimitWriter(fileSystem, commandRunner, ResolvePlatform()).SetEndThreshold(value);

        if (normalizedControlId.Equals("display-brightness", StringComparison.OrdinalIgnoreCase))
            return new DisplayBrightnessWriter(fileSystem, commandRunner, ResolvePlatform()).SetBrightnessPercent(value);

        if (!normalizedControlId.Equals("power-profile", StringComparison.OrdinalIgnoreCase))
        {
            return new HardwareControlSetResult(
                false,
                controlId,
                value,
                "Only standard power-profile, cpu-governor, battery-charge-limit, and display-brightness controls are writable in the cross-platform CLI.");
        }

        var result = ResolvePlatform() switch
        {
            CrossPlatformControlPlatform.Linux => new LinuxPowerProfileProvider(commandRunner).SetProfile(value),
            CrossPlatformControlPlatform.MacOS => new MacPowerProfileProvider(commandRunner).SetProfile(value),
            _ => new PowerProfileWriter(commandRunner).SetProfile(value)
        };
        return new HardwareControlSetResult(result.Succeeded, "power-profile", result.ProfileId, result.Detail);
    }

    private CrossPlatformControlPlatform ResolvePlatform()
    {
        if (platform != CrossPlatformControlPlatform.Auto)
            return platform;

        if (OperatingSystem.IsLinux())
            return CrossPlatformControlPlatform.Linux;

        if (OperatingSystem.IsMacOS())
            return CrossPlatformControlPlatform.MacOS;

        return CrossPlatformControlPlatform.Other;
    }

    private static string NormalizeControlId(string value) =>
        value.Trim().ToLowerInvariant().Replace('_', '-');
}

internal enum CrossPlatformControlPlatform
{
    Auto,
    Linux,
    MacOS,
    Other
}
