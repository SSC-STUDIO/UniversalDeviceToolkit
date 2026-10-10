using System.Globalization;
using System.Text.RegularExpressions;

internal sealed record BatteryChargeLimitStatus(
    string Source,
    BatteryChargeLimitDevice[] Devices,
    string[] Notes)
{
    public static BatteryChargeLimitStatus Unknown(string source, params string[] notes) =>
        new(source, [], notes);
}

internal sealed record BatteryChargeLimitDevice(
    string Id,
    string DisplayName,
    int? StartThreshold,
    int? EndThreshold,
    string StartThresholdPath,
    string EndThresholdPath,
    string Source);

internal sealed class BatteryChargeLimitReader(IFileSystem fileSystem)
{
    public BatteryChargeLimitStatus Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxBatteryChargeLimitProvider(fileSystem).Read();

        return BatteryChargeLimitStatus.Unknown("runtime", "No cross-platform battery charge limit provider is available for this OS.");
    }
}

internal sealed class LinuxBatteryChargeLimitProvider(IFileSystem fileSystem)
{
    private const string PowerSupplyRoot = "/sys/class/power_supply";

    public BatteryChargeLimitStatus Read()
    {
        var devices = fileSystem.EnumerateDirectories(PowerSupplyRoot)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(ReadDevice)
            .OfType<BatteryChargeLimitDevice>()
            .ToArray();

        return devices.Length == 0
            ? BatteryChargeLimitStatus.Unknown("linux-power-supply-threshold", "No readable Linux battery charge threshold controls were found in /sys/class/power_supply.")
            : new BatteryChargeLimitStatus("linux-power-supply-threshold", devices, []);
    }

    private BatteryChargeLimitDevice? ReadDevice(string directory)
    {
        var id = fileSystem.GetFileName(directory);
        var type = ReadValue(CombinePath(directory, "type"));
        if (!type.Equals("Battery", StringComparison.OrdinalIgnoreCase) && !id.StartsWith("BAT", StringComparison.OrdinalIgnoreCase))
            return null;

        var endPath = CombinePath(directory, "charge_control_end_threshold");
        var startPath = CombinePath(directory, "charge_control_start_threshold");
        var endThreshold = ParsePercent(fileSystem.ReadAllText(endPath));
        var startThreshold = ParsePercent(fileSystem.ReadAllText(startPath));
        if (endThreshold is null && startThreshold is null)
            return null;

        return new BatteryChargeLimitDevice(
            id,
            id,
            startThreshold,
            endThreshold,
            startPath,
            endPath,
            "linux-power-supply-threshold");
    }

    private static int? ParsePercent(string value)
    {
        var match = Regex.Match(value, @"\d+");
        if (!match.Success || !int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return null;

        return Math.Clamp(parsed, 0, 100);
    }

    private static string ReadValue(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;

    private static string CombinePath(string directory, string fileName) =>
        $"{directory.TrimEnd('/')}/{fileName}";
}

internal sealed class BatteryChargeLimitWriter(
    IFileSystem fileSystem,
    ICommandResultRunner commandRunner,
    CrossPlatformControlPlatform platform = CrossPlatformControlPlatform.Auto)
{
    public HardwareControlSetResult SetEndThreshold(string value)
    {
        if (!TryParsePercent(value, out var percent))
        {
            return new HardwareControlSetResult(
                false,
                "battery-charge-limit",
                value,
                "Battery charge limit must be an integer percentage from 1 to 100.");
        }

        if (ResolvePlatform() != CrossPlatformControlPlatform.Linux)
        {
            return new HardwareControlSetResult(
                false,
                "battery-charge-limit",
                percent.ToString(CultureInfo.InvariantCulture),
                "Battery charge limit control is currently implemented for Linux power_supply thresholds only.");
        }

        var status = new LinuxBatteryChargeLimitProvider(fileSystem).Read();
        var device = status.Devices.FirstOrDefault(candidate => candidate.EndThreshold is not null);
        if (device is null)
        {
            var note = status.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
            return new HardwareControlSetResult(
                false,
                "battery-charge-limit",
                percent.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(note) ? "No writable battery charge limit device was found." : note);
        }

        var result = commandRunner.RunResult("sh", "-c", $"printf %s {percent} > {ShellQuote(device.EndThresholdPath)}");
        return result.Succeeded
            ? new HardwareControlSetResult(true, "battery-charge-limit", percent.ToString(CultureInfo.InvariantCulture), $"Set {device.Id} charge limit to {percent}%.")
            : new HardwareControlSetResult(false, "battery-charge-limit", percent.ToString(CultureInfo.InvariantCulture), result.GetSummary());
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

    private static bool TryParsePercent(string value, out int percent)
    {
        if (!int.TryParse(value.Trim().TrimEnd('%'), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent))
            return false;

        return percent is >= 1 and <= 100;
    }

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}
