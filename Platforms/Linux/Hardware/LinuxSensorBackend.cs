using System.Globalization;
using System.Text.RegularExpressions;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;

namespace UniversalDeviceToolkit.Platform.Linux.Hardware;

/// <summary>
/// Linux <see cref="ISensorBackend"/> backed by procfs and sysfs:
/// <c>/proc/stat</c>, <c>/proc/meminfo</c>, <c>/proc/cpuinfo</c>,
/// <c>/sys/class/hwmon</c>, <c>/sys/class/thermal</c>,
/// <c>/sys/devices/system/cpu/*/cpufreq</c>, and <c>/sys/class/power_supply</c>.
/// Does not use LibreHardwareMonitor.
/// </summary>
public sealed class LinuxSensorBackend : ISensorBackend
{
    private const string HwmonRoot = "/sys/class/hwmon";
    private const string ThermalRoot = "/sys/class/thermal";
    private const string CpuSysfsRoot = "/sys/devices/system/cpu";
    private const string PowerSupplyRoot = "/sys/class/power_supply";
    private const string RaplRoot = "/sys/class/powercap";

    private static readonly string[] CpuHwmonChips =
    [
        "coretemp", "k10temp", "zenpower", "k8temp", "via_cputemp", "cpu_thermal",
        "soc_thermal", "soc-thermal", "cpu-thermal", "x86_pkg_temp"
    ];

    private static readonly string[] GpuHwmonChips =
    [
        "amdgpu", "nvidia", "nouveau", "i915", "xe", "radeon", "habanalabs"
    ];

    private static readonly string[] StorageHwmonChips =
    [
        "nvme", "drivetemp", "scsi"
    ];

    private readonly ILinuxFileSystem _fs;
    private readonly object _cpuStatLock = new();
    private (long Idle, long Total)? _previousCpuStat;
    private readonly object _raplLock = new();
    private (long EnergyUj, long TimestampMs)? _previousRapl;

    public LinuxSensorBackend()
        : this(PhysicalLinuxFileSystem.Instance)
    {
    }

    public LinuxSensorBackend(ILinuxFileSystem fileSystem)
    {
        _fs = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _previousCpuStat = ReadProcStatCpu();
        PrimeRapl();
    }

    /// <inheritdoc />
    public bool IsAvailable =>
        _fs.FileExists("/proc/stat") ||
        _fs.FileExists("/proc/meminfo") ||
        _fs.DirectoryExists(HwmonRoot) ||
        _fs.DirectoryExists(ThermalRoot) ||
        _fs.DirectoryExists(PowerSupplyRoot);

    /// <inheritdoc />
    public IReadOnlyList<SensorReading> GetReadings()
    {
        var readings = new List<SensorReading>();
        ReadCpuUsage(readings);
        ReadCpuModel(readings);
        ReadCpuFrequencies(readings);
        ReadMemory(readings);
        ReadHwmon(readings);
        ReadThermalZones(readings);
        ReadRaplPower(readings);
        LinuxPowerSupplyReader.AddReadings(_fs, readings);
        return readings;
    }

    private void ReadCpuUsage(List<SensorReading> readings)
    {
        var current = ReadProcStatCpu();
        if (current is null)
            return;

        double? usage = null;
        lock (_cpuStatLock)
        {
            if (_previousCpuStat is { } previous)
            {
                var idleDelta = current.Value.Idle - previous.Idle;
                var totalDelta = current.Value.Total - previous.Total;
                if (totalDelta > 0 && idleDelta >= 0 && idleDelta <= totalDelta)
                    usage = (1.0 - (double)idleDelta / totalDelta) * 100.0;
            }

            _previousCpuStat = current;
        }

        if (usage is not null)
            readings.Add(new SensorReading("CPU", "Usage", Math.Round(Math.Clamp(usage.Value, 0, 100), 1), "%"));
    }

    private (long Idle, long Total)? ReadProcStatCpu()
    {
        var text = _fs.ReadText("/proc/stat");
        if (text is null)
            return null;

        var firstLine = text.Split('\n', 2)[0].Trim();
        if (!firstLine.StartsWith("cpu ", StringComparison.Ordinal))
            return null;

        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            return null;

        var values = parts.Skip(1).Select(part => long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0L).ToArray();
        var total = values.Sum();
        var idle = values.Length > 3 ? values[3] : 0L;
        if (values.Length > 4)
            idle += values[4]; // iowait
        return (idle, total);
    }

    private void ReadCpuModel(List<SensorReading> readings)
    {
        var model = LinuxCpuInfo.ReadModelName(_fs);
        if (!string.IsNullOrWhiteSpace(model))
            readings.Add(new SensorReading(model, "Identity", 0, ""));
    }

    private void ReadCpuFrequencies(List<SensorReading> readings)
    {
        var megahertz = new List<double>();
        foreach (var directory in _fs.EnumerateDirectories(CpuSysfsRoot))
        {
            var cpuId = Path.GetFileName(directory.TrimEnd('/'));
            if (cpuId is null || !Regex.IsMatch(cpuId, @"^cpu\d+$", RegexOptions.IgnoreCase))
                continue;

            var raw = ReadDouble(Combine(directory, "cpufreq/scaling_cur_freq"));
            if (raw is null or <= 0)
                continue;

            megahertz.Add(raw.Value / 1000.0);
        }

        if (megahertz.Count == 0)
            megahertz.AddRange(LinuxCpuInfo.ReadProcFrequenciesMhz(_fs));

        if (megahertz.Count == 0)
            return;

        readings.Add(new SensorReading("CPU", "Frequency", Math.Round(megahertz.Max(), 1), "MHz"));
        readings.Add(new SensorReading("CPU", "FrequencyAvg", Math.Round(megahertz.Average(), 1), "MHz"));
    }

    private void ReadMemory(List<SensorReading> readings)
    {
        var meminfo = _fs.ReadText("/proc/meminfo");
        if (meminfo is null)
            return;

        long? totalKb = null;
        long? availableKb = null;
        foreach (var line in meminfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            var numeric = Regex.Match(line[(separator + 1)..], @"(\d+)");
            if (!numeric.Success || !long.TryParse(numeric.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb))
                continue;

            if (key.Equals("MemTotal", StringComparison.OrdinalIgnoreCase))
                totalKb = kb;
            else if (key.Equals("MemAvailable", StringComparison.OrdinalIgnoreCase))
                availableKb = kb;
        }

        if (totalKb is not > 0)
            return;

        var usedKb = totalKb.Value - (availableKb ?? 0);
        readings.Add(new SensorReading("Memory", "Total", Math.Round(totalKb.Value / 1024.0, 1), "MB"));
        readings.Add(new SensorReading("Memory", "Used", Math.Round(usedKb / 1024.0, 1), "MB"));
        if (availableKb is not null)
        {
            var usedPct = (1.0 - (double)availableKb.Value / totalKb.Value) * 100.0;
            readings.Add(new SensorReading("Memory", "Usage", Math.Round(Math.Clamp(usedPct, 0, 100), 1), "%"));
        }
    }

    private void ReadHwmon(List<SensorReading> readings)
    {
        if (!_fs.DirectoryExists(HwmonRoot))
            return;

        var cpuTemps = new List<double>();
        var gpuTemps = new List<double>();
        var memoryTemps = new List<double>();
        var motherboardTemps = new List<double>();
        var storageTemps = new List<double>();
        var cpuFans = new List<double>();
        var gpuFans = new List<double>();
        var otherFans = new List<double>();
        var cpuPowers = new List<double>();
        var gpuPowers = new List<double>();
        var cpuVoltages = new List<double>();

        foreach (var deviceDir in _fs.EnumerateDirectories(HwmonRoot))
        {
            var chipName = NormalizeName(_fs.ReadText(Combine(deviceDir, "name")));
            var domain = ClassifyChip(chipName);

            foreach (var inputPath in _fs.EnumerateFiles(deviceDir, "temp*_input"))
            {
                var celsius = ReadMilliAsUnit(inputPath, 1000.0);
                if (celsius is null || celsius < -100 || celsius > 200)
                    continue;

                var label = NormalizeName(_fs.ReadText(Regex.Replace(inputPath, "_input$", "_label", RegexOptions.IgnoreCase)));
                var classified = ClassifyTemperature(domain, chipName, label);
                var display = FirstPresent(label, chipName, Path.GetFileName(deviceDir));
                readings.Add(new SensorReading(display, "Temperature", Math.Round(celsius.Value, 1), "°C"));

                switch (classified)
                {
                    case SensorDomain.Cpu:
                        cpuTemps.Add(celsius.Value);
                        break;
                    case SensorDomain.Gpu:
                        gpuTemps.Add(celsius.Value);
                        break;
                    case SensorDomain.Memory:
                        memoryTemps.Add(celsius.Value);
                        break;
                    case SensorDomain.Storage:
                        storageTemps.Add(celsius.Value);
                        break;
                    default:
                        motherboardTemps.Add(celsius.Value);
                        break;
                }
            }

            foreach (var inputPath in _fs.EnumerateFiles(deviceDir, "fan*_input"))
            {
                var rpm = ReadDouble(inputPath);
                if (rpm is null || rpm < 0 || rpm > 100_000)
                    continue;

                var label = NormalizeName(_fs.ReadText(Regex.Replace(inputPath, "_input$", "_label", RegexOptions.IgnoreCase)));
                var display = FirstPresent(label, chipName, Path.GetFileName(deviceDir));
                readings.Add(new SensorReading(display, "Fan", rpm.Value, "RPM"));

                var text = $"{label} {chipName}";
                if (ContainsAny(text, "gpu", "dgpu", "amdgpu", "nvidia"))
                    gpuFans.Add(rpm.Value);
                else if (ContainsAny(text, "cpu", "processor", "package") || domain == SensorDomain.Cpu)
                    cpuFans.Add(rpm.Value);
                else
                    otherFans.Add(rpm.Value);
            }

            foreach (var inputPath in _fs.EnumerateFiles(deviceDir, "power*_input"))
            {
                var microwatts = ReadDouble(inputPath);
                if (microwatts is null or < 0)
                    continue;

                var watts = microwatts.Value / 1_000_000.0;
                if (watts is < 0 or > 2000)
                    continue;

                var label = NormalizeName(_fs.ReadText(Regex.Replace(inputPath, "_input$", "_label", RegexOptions.IgnoreCase)));
                readings.Add(new SensorReading(FirstPresent(label, chipName), "Power", Math.Round(watts, 2), "W"));
                if (domain == SensorDomain.Gpu)
                    gpuPowers.Add(watts);
                else if (domain == SensorDomain.Cpu)
                    cpuPowers.Add(watts);
            }

            foreach (var inputPath in _fs.EnumerateFiles(deviceDir, "in*_input"))
            {
                var millivolts = ReadDouble(inputPath);
                if (millivolts is null or <= 0)
                    continue;

                var volts = millivolts.Value / 1000.0;
                if (volts is < 0.2 or > 20)
                    continue;

                var label = NormalizeName(_fs.ReadText(Regex.Replace(inputPath, "_input$", "_label", RegexOptions.IgnoreCase)));
                if (domain == SensorDomain.Cpu && (string.IsNullOrWhiteSpace(label) || ContainsAny(label, "vcore", "vdd", "vin", "cpu")))
                    cpuVoltages.Add(volts);
            }
        }

        AddCanonical(readings, "CPU", "Temperature", cpuTemps.Count > 0 ? cpuTemps.Max() : null, "°C");
        AddCanonical(readings, "GPU", "Temperature", gpuTemps.Count > 0 ? gpuTemps.Max() : null, "°C");
        AddCanonical(readings, "Memory", "Temperature", memoryTemps.Count > 0 ? memoryTemps.Max() : null, "°C");
        AddCanonical(readings, "Motherboard", "Temperature", motherboardTemps.Count > 0 ? motherboardTemps.Max() : null, "°C");
        foreach (var storageTemp in storageTemps.Take(4))
            readings.Add(new SensorReading("Storage", "Temperature", Math.Round(storageTemp, 1), "°C"));

        AddCanonical(readings, "CPU", "Fan", FirstFan(cpuFans, otherFans), "RPM");
        AddCanonical(readings, "GPU", "Fan", gpuFans.Count > 0 ? gpuFans.Max() : null, "RPM");
        AddCanonical(readings, "CPU", "Power", cpuPowers.Count > 0 ? cpuPowers.Max() : null, "W");
        AddCanonical(readings, "GPU", "Power", gpuPowers.Count > 0 ? gpuPowers.Max() : null, "W");
        AddCanonical(readings, "CPU", "Voltage", cpuVoltages.Count > 0 ? cpuVoltages[0] : null, "V");
    }

    private void ReadThermalZones(List<SensorReading> readings)
    {
        if (!_fs.DirectoryExists(ThermalRoot))
            return;

        var cpuTemps = new List<double>();
        foreach (var zoneDir in _fs.EnumerateDirectories(ThermalRoot))
        {
            var zoneName = Path.GetFileName(zoneDir.TrimEnd('/'));
            if (zoneName is null || !zoneName.StartsWith("thermal_zone", StringComparison.OrdinalIgnoreCase))
                continue;

            var celsius = ReadMilliAsUnit(Combine(zoneDir, "temp"), 1000.0);
            if (celsius is null || celsius < -100 || celsius > 200)
                continue;

            var rawType = _fs.ReadText(Combine(zoneDir, "type"));
            var type = NormalizeName(rawType);
            var display = FirstPresent(type, zoneName);
            readings.Add(new SensorReading(display, "Temperature", Math.Round(celsius.Value, 1), "°C"));

            if (ContainsAny($"{rawType} {type}", "cpu", "x86_pkg", "x86 pkg", "soc", "package", "processor", "acpitz"))
                cpuTemps.Add(celsius.Value);
        }

        if (!readings.Any(reading =>
                string.Equals(reading.Name, "CPU", StringComparison.Ordinal) &&
                string.Equals(reading.Category, "Temperature", StringComparison.Ordinal))
            && cpuTemps.Count > 0)
        {
            AddCanonical(readings, "CPU", "Temperature", cpuTemps.Max(), "°C");
        }
    }

    private void ReadRaplPower(List<SensorReading> readings)
    {
        if (readings.Any(reading =>
                string.Equals(reading.Name, "CPU", StringComparison.Ordinal) &&
                string.Equals(reading.Category, "Power", StringComparison.Ordinal)))
        {
            return;
        }

        var energy = ReadRaplEnergyUj();
        if (energy is null)
            return;

        var nowMs = Environment.TickCount64;
        double? watts = null;
        lock (_raplLock)
        {
            if (_previousRapl is { } previous && nowMs > previous.TimestampMs)
            {
                var deltaUj = energy.Value - previous.EnergyUj;
                var deltaSec = (nowMs - previous.TimestampMs) / 1000.0;
                if (deltaUj >= 0 && deltaSec > 0)
                    watts = (deltaUj / 1_000_000.0) / deltaSec;
            }

            _previousRapl = (checked((long)energy.Value), nowMs);
        }

        if (watts is > 0 and < 2000)
            AddCanonical(readings, "CPU", "Power", watts, "W");
    }

    private void PrimeRapl()
    {
        var energy = ReadRaplEnergyUj();
        if (energy is not null)
            _previousRapl = (checked((long)energy.Value), Environment.TickCount64);
    }

    private double? ReadRaplEnergyUj()
    {
        var energy = ReadDouble(Combine(RaplRoot, "intel-rapl:0/energy_uj"));
        if (energy is not null)
            return energy;

        foreach (var domain in _fs.EnumerateDirectories(RaplRoot))
        {
            energy = ReadDouble(Combine(domain, "energy_uj"));
            if (energy is not null)
                return energy;
        }

        return null;
    }

    private double? ReadDouble(string path)
    {
        var raw = _fs.ReadText(path)?.Trim();
        return raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private double? ReadMilliAsUnit(string path, double divisor)
    {
        var value = ReadDouble(path);
        return value is null ? null : value.Value / divisor;
    }

    private static void AddCanonical(List<SensorReading> readings, string name, string category, double? value, string unit)
    {
        if (value is null)
            return;

        readings.RemoveAll(reading =>
            string.Equals(reading.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(reading.Category, category, StringComparison.OrdinalIgnoreCase));
        readings.Add(new SensorReading(name, category, Math.Round(value.Value, category == "Fan" ? 0 : 1), unit));
    }

    private static double? FirstFan(List<double> preferred, List<double> fallback)
    {
        if (preferred.Count > 0)
            return preferred.Max();
        if (fallback.Count > 0)
            return fallback.Max();
        return null;
    }

    private static SensorDomain ClassifyChip(string chipName)
    {
        if (CpuHwmonChips.Any(chip => chipName.Equals(chip, StringComparison.OrdinalIgnoreCase)))
            return SensorDomain.Cpu;
        if (GpuHwmonChips.Any(chip => chipName.Equals(chip, StringComparison.OrdinalIgnoreCase)))
            return SensorDomain.Gpu;
        if (StorageHwmonChips.Any(chip => chipName.Contains(chip, StringComparison.OrdinalIgnoreCase)))
            return SensorDomain.Storage;
        if (ContainsAny(chipName, "dimm", "spd5118", "jedec", "acpi_mem"))
            return SensorDomain.Memory;
        return SensorDomain.Motherboard;
    }

    private static SensorDomain ClassifyTemperature(SensorDomain chipDomain, string chipName, string label)
    {
        var text = $"{chipName} {label}";
        if (ContainsAny(text, "nvme", "ssd", "drive", "composite"))
            return SensorDomain.Storage;
        if (ContainsAny(text, "gpu", "dgpu", "vram", "hotspot", "edge"))
            return SensorDomain.Gpu;
        if (ContainsAny(text, "dimm", "mem", "dram"))
            return SensorDomain.Memory;
        if (ContainsAny(text, "tctl", "tdie", "package", "core", "cpu", "peci", "tccd"))
            return SensorDomain.Cpu;
        return chipDomain;
    }

    private static bool ContainsAny(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeName(string? value) =>
        Regex.Replace((value ?? string.Empty).Trim().Replace('_', ' '), @"\s+", " ").Trim();

    private static string FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string Combine(string directory, string relative) =>
        $"{directory.TrimEnd('/')}/{relative.TrimStart('/')}";

    private enum SensorDomain
    {
        Cpu,
        Gpu,
        Memory,
        Storage,
        Motherboard
    }
}

internal static class LinuxCpuInfo
{
    public static string? ReadModelName(ILinuxFileSystem fileSystem)
    {
        var cpuInfo = fileSystem.ReadText("/proc/cpuinfo");
        if (cpuInfo is null)
            return null;

        foreach (var line in cpuInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            if (key.Equals("model name", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Hardware", StringComparison.OrdinalIgnoreCase))
            {
                var value = line[(separator + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : Regex.Replace(value, @"\s+", " ");
            }
        }

        return null;
    }

    public static IReadOnlyList<double> ReadProcFrequenciesMhz(ILinuxFileSystem fileSystem)
    {
        var cpuInfo = fileSystem.ReadText("/proc/cpuinfo");
        if (cpuInfo is null)
            return [];

        var values = new List<double>();
        foreach (var line in cpuInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            if (!key.Equals("cpu MHz", StringComparison.OrdinalIgnoreCase))
                continue;

            if (double.TryParse(line[(separator + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz > 0)
                values.Add(mhz);
        }

        return values;
    }
}
