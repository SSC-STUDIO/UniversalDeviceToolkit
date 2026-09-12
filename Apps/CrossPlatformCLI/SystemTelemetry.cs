using System.Globalization;
using System.Text.RegularExpressions;

internal sealed record SystemTelemetry(
    string Source,
    string CpuModel,
    int? LogicalProcessorCount,
    double? MemoryTotalGiB,
    double? MemoryAvailableGiB,
    CpuFrequencyReading[] CpuFrequencies,
    TemperatureReading[] Temperatures,
    FanSpeedReading[] FanSpeeds,
    string[] Notes)
{
    public static SystemTelemetry Unknown(string source, params string[] notes) =>
        new(source, string.Empty, Environment.ProcessorCount, null, null, [], [], [], notes);
}

internal sealed record CpuFrequencyReading(
    string Name,
    double MHz,
    string Source);

internal sealed record TemperatureReading(
    string Name,
    double Celsius,
    string Source);

internal sealed record FanSpeedReading(
    string Name,
    int Rpm,
    string Source);

internal sealed class SystemTelemetryReader(
    IFileSystem fileSystem,
    ICommandRunner commandRunner)
{
    public SystemTelemetry Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxSystemTelemetryProvider(fileSystem).Read();

        if (OperatingSystem.IsMacOS())
            return new MacSystemTelemetryProvider(commandRunner).Read();

        return SystemTelemetry.Unknown("runtime", "No cross-platform telemetry provider is available for this OS.");
    }
}

internal sealed class LinuxSystemTelemetryProvider(IFileSystem fileSystem)
{
    private const string HwmonRoot = "/sys/class/hwmon";

    public SystemTelemetry Read()
    {
        var cpuModel = ReadCpuModel();
        var (memoryTotalGiB, memoryAvailableGiB) = ReadMemory();
        var cpuFrequencies = ReadCpuFrequencies();
        var temperatures = ReadTemperatures();
        var fanSpeeds = ReadFanSpeeds();
        var notes = new List<string>();
        if (cpuFrequencies.Length == 0)
            notes.Add("No readable Linux CPU frequency inputs were found.");
        if (temperatures.Length == 0)
            notes.Add("No readable hwmon temperature inputs were found.");
        if (fanSpeeds.Length == 0)
            notes.Add("No readable hwmon fan speed inputs were found.");

        return new SystemTelemetry(
            "linux-procfs-sysfs",
            cpuModel,
            Environment.ProcessorCount,
            memoryTotalGiB,
            memoryAvailableGiB,
            cpuFrequencies,
            temperatures,
            fanSpeeds,
            notes.ToArray());
    }

    private string ReadCpuModel()
    {
        var cpuInfo = fileSystem.ReadAllText("/proc/cpuinfo");
        foreach (var line in cpuInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            if (key.Equals("model name", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Hardware", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeWhitespace(line[(separator + 1)..]);
            }
        }

        return string.Empty;
    }

    private (double? TotalGiB, double? AvailableGiB) ReadMemory()
    {
        var memoryInfo = fileSystem.ReadAllText("/proc/meminfo");
        double? total = null;
        double? available = null;

        foreach (var line in memoryInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            var value = TryParseFirstDouble(line[(separator + 1)..]);
            if (value is null)
                continue;

            if (key.Equals("MemTotal", StringComparison.OrdinalIgnoreCase))
                total = KilobytesToGibibytes(value.Value);
            else if (key.Equals("MemAvailable", StringComparison.OrdinalIgnoreCase))
                available = KilobytesToGibibytes(value.Value);
        }

        return (total, available);
    }

    private CpuFrequencyReading[] ReadCpuFrequencies()
    {
        var sysfsReadings = ReadSysfsCpuFrequencies();
        return sysfsReadings.Length > 0 ? sysfsReadings : ReadProcCpuFrequencies();
    }

    private CpuFrequencyReading[] ReadSysfsCpuFrequencies()
    {
        var readings = new List<CpuFrequencyReading>();

        foreach (var directory in fileSystem.EnumerateDirectories("/sys/devices/system/cpu").OrderBy(path => path, StringComparer.Ordinal))
        {
            var cpuId = fileSystem.GetFileName(directory);
            if (!Regex.IsMatch(cpuId, @"^cpu\d+$", RegexOptions.IgnoreCase))
                continue;

            var raw = fileSystem.ReadAllText(CombineUnixPath(CombineUnixPath(directory, "cpufreq"), "scaling_cur_freq"));
            var kilohertz = TryParseFirstDouble(raw);
            if (kilohertz is null || kilohertz <= 0)
                continue;

            readings.Add(new CpuFrequencyReading(cpuId, Math.Round(kilohertz.Value / 1000.0, 1), "linux-cpufreq"));
        }

        return readings.ToArray();
    }

    private CpuFrequencyReading[] ReadProcCpuFrequencies()
    {
        var readings = new List<CpuFrequencyReading>();
        var cpuInfo = fileSystem.ReadAllText("/proc/cpuinfo");
        var processorId = string.Empty;

        foreach (var line in cpuInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Equals("processor", StringComparison.OrdinalIgnoreCase))
            {
                processorId = value;
                continue;
            }

            if (!key.Equals("cpu MHz", StringComparison.OrdinalIgnoreCase))
                continue;

            var megahertz = TryParseFirstDouble(value);
            if (megahertz is null || megahertz <= 0)
                continue;

            var name = string.IsNullOrWhiteSpace(processorId) ? $"cpu{readings.Count}" : $"cpu{processorId}";
            readings.Add(new CpuFrequencyReading(name, Math.Round(megahertz.Value, 1), "linux-procfs"));
        }

        return readings.ToArray();
    }

    private TemperatureReading[] ReadTemperatures()
    {
        var readings = new List<TemperatureReading>();

        foreach (var deviceDirectory in fileSystem.EnumerateDirectories(HwmonRoot).OrderBy(path => path, StringComparer.Ordinal))
        {
            var chipName = NormalizeHwmonName(fileSystem.ReadAllText(CombineUnixPath(deviceDirectory, "name")));
            foreach (var inputPath in fileSystem.EnumerateFiles(deviceDirectory, "temp*_input").OrderBy(path => path, StringComparer.Ordinal))
            {
                var raw = fileSystem.ReadAllText(inputPath).Trim();
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var millidegrees))
                    continue;

                var celsius = millidegrees / 1000.0;
                if (celsius < -100 || celsius > 200)
                    continue;

                var labelPath = Regex.Replace(inputPath, "_input$", "_label", RegexOptions.IgnoreCase);
                var label = NormalizeHwmonName(fileSystem.ReadAllText(labelPath));
                var name = FirstPresent(label, chipName, Path.GetFileName(deviceDirectory));
                readings.Add(new TemperatureReading(name, Math.Round(celsius, 1), "linux-hwmon"));
            }
        }

        return readings.ToArray();
    }

    private FanSpeedReading[] ReadFanSpeeds()
    {
        var readings = new List<FanSpeedReading>();

        foreach (var deviceDirectory in fileSystem.EnumerateDirectories(HwmonRoot).OrderBy(path => path, StringComparer.Ordinal))
        {
            var chipName = NormalizeHwmonName(fileSystem.ReadAllText(CombineUnixPath(deviceDirectory, "name")));
            foreach (var inputPath in fileSystem.EnumerateFiles(deviceDirectory, "fan*_input").OrderBy(path => path, StringComparer.Ordinal))
            {
                var raw = fileSystem.ReadAllText(inputPath).Trim();
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rpm))
                    continue;

                if (rpm < 0 || rpm > 100_000)
                    continue;

                var labelPath = Regex.Replace(inputPath, "_input$", "_label", RegexOptions.IgnoreCase);
                var label = NormalizeHwmonName(fileSystem.ReadAllText(labelPath));
                var name = FirstPresent(label, chipName, Path.GetFileName(deviceDirectory));
                readings.Add(new FanSpeedReading(name, rpm, "linux-hwmon"));
            }
        }

        return readings.ToArray();
    }

    private static string NormalizeHwmonName(string value) =>
        NormalizeWhitespace(value.Replace('_', ' '));

    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static double? TryParseFirstDouble(string value)
    {
        var match = Regex.Match(value, @"[-+]?\d+(?:\.\d+)?");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double KilobytesToGibibytes(double value) =>
        Math.Round(value / 1024.0 / 1024.0, 2);

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string CombineUnixPath(string directory, string fileName) =>
        $"{directory.TrimEnd('/')}/{fileName}";
}

internal sealed class MacSystemTelemetryProvider(ICommandRunner commandRunner)
{
    public SystemTelemetry Read()
    {
        var cpuModel = commandRunner.Run("sysctl", "-n", "machdep.cpu.brand_string").Trim();
        if (string.IsNullOrWhiteSpace(cpuModel))
            cpuModel = commandRunner.Run("sysctl", "-n", "hw.model").Trim();

        var logicalCpuCount = TryParseInt(commandRunner.Run("sysctl", "-n", "hw.logicalcpu").Trim());
        var memoryBytes = TryParseDouble(commandRunner.Run("sysctl", "-n", "hw.memsize").Trim());
        var cpuFrequencyHertz = TryParseDouble(commandRunner.Run("sysctl", "-n", "hw.cpufrequency").Trim());
        var cpuFrequencies = cpuFrequencyHertz is null
            ? Array.Empty<CpuFrequencyReading>()
            : [new CpuFrequencyReading("package", Math.Round(cpuFrequencyHertz.Value / 1_000_000.0, 1), "macos-sysctl")];

        return new SystemTelemetry(
            "macos-sysctl",
            cpuModel,
            logicalCpuCount ?? Environment.ProcessorCount,
            memoryBytes is null ? null : Math.Round(memoryBytes.Value / 1024.0 / 1024.0 / 1024.0, 2),
            null,
            cpuFrequencies,
            [],
            [],
            ["macOS temperature and fan sensors require platform-specific SMC access and are not read by this safe diagnostics provider."]);
    }

    private static int? TryParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static double? TryParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
