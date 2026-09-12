using System.Globalization;
using System.Text.RegularExpressions;

internal sealed record DisplayBrightnessStatus(
    string Source,
    DisplayBrightnessDevice[] Devices,
    string[] Notes)
{
    public static DisplayBrightnessStatus Unknown(string source, params string[] notes) =>
        new(source, [], notes);
}

internal sealed record DisplayBrightnessDevice(
    string Id,
    string DisplayName,
    int Brightness,
    int MaxBrightness,
    int Percent,
    string BrightnessPath,
    string Source);

internal sealed class DisplayBrightnessReader(
    IFileSystem fileSystem)
{
    public DisplayBrightnessStatus Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxDisplayBrightnessProvider(fileSystem).Read();

        return DisplayBrightnessStatus.Unknown("runtime", "No cross-platform display brightness provider is available for this OS.");
    }
}

internal sealed class LinuxDisplayBrightnessProvider(IFileSystem fileSystem)
{
    private const string BacklightRoot = "/sys/class/backlight";

    public DisplayBrightnessStatus Read()
    {
        var devices = new List<DisplayBrightnessDevice>();

        foreach (var directory in fileSystem.EnumerateDirectories(BacklightRoot).OrderBy(path => path, StringComparer.Ordinal))
        {
            var brightness = ParsePositiveInt(fileSystem.ReadAllText(CombinePath(directory, "brightness")));
            var maxBrightness = ParsePositiveInt(fileSystem.ReadAllText(CombinePath(directory, "max_brightness")));
            if (brightness is null || maxBrightness is null || maxBrightness <= 0)
                continue;

            var boundedBrightness = Math.Clamp(brightness.Value, 0, maxBrightness.Value);
            var percent = (int)Math.Round(boundedBrightness * 100.0 / maxBrightness.Value, MidpointRounding.AwayFromZero);
            var id = fileSystem.GetFileName(directory);

            devices.Add(new DisplayBrightnessDevice(
                id,
                id,
                boundedBrightness,
                maxBrightness.Value,
                Math.Clamp(percent, 0, 100),
                CombinePath(directory, "brightness"),
                "linux-backlight"));
        }

        return devices.Count == 0
            ? DisplayBrightnessStatus.Unknown("linux-backlight", "No readable Linux backlight devices were found in /sys/class/backlight.")
            : new DisplayBrightnessStatus("linux-backlight", devices.ToArray(), []);
    }

    private static int? ParsePositiveInt(string value)
    {
        var match = Regex.Match(value, @"\d+");
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string CombinePath(string directory, string fileName) =>
        $"{directory.TrimEnd('/')}/{fileName}";
}

internal sealed class DisplayBrightnessWriter(
    IFileSystem fileSystem,
    ICommandResultRunner commandRunner,
    CrossPlatformControlPlatform platform = CrossPlatformControlPlatform.Auto)
{
    public HardwareControlSetResult SetBrightnessPercent(string value)
    {
        if (!TryParsePercent(value, out var percent))
        {
            return new HardwareControlSetResult(
                false,
                "display-brightness",
                value,
                "Brightness must be an integer percentage from 0 to 100.");
        }

        if (ResolvePlatform() != CrossPlatformControlPlatform.Linux)
        {
            return new HardwareControlSetResult(
                false,
                "display-brightness",
                percent.ToString(CultureInfo.InvariantCulture),
                "Display brightness control is currently implemented for Linux backlight devices only.");
        }

        var status = new LinuxDisplayBrightnessProvider(fileSystem).Read();
        var device = status.Devices.FirstOrDefault();
        if (device is null)
        {
            var note = status.Notes.FirstOrDefault(note => !string.IsNullOrWhiteSpace(note));
            return new HardwareControlSetResult(
                false,
                "display-brightness",
                percent.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(note) ? "No writable display brightness device was found." : note);
        }

        var rawBrightness = Math.Clamp(
            (int)Math.Round(device.MaxBrightness * percent / 100.0, MidpointRounding.AwayFromZero),
            0,
            device.MaxBrightness);

        var result = commandRunner.RunResult("sh", "-c", $"printf %s {rawBrightness} > {ShellQuote(device.BrightnessPath)}");
        return result.Succeeded
            ? new HardwareControlSetResult(true, "display-brightness", percent.ToString(CultureInfo.InvariantCulture), $"Set {device.Id} to {percent}% ({rawBrightness}/{device.MaxBrightness}).")
            : new HardwareControlSetResult(false, "display-brightness", percent.ToString(CultureInfo.InvariantCulture), result.GetSummary());
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

        return percent is >= 0 and <= 100;
    }

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
}
