using System.Globalization;
using System.Text.RegularExpressions;

internal sealed record PowerStatus(
    string Source,
    PowerSupplyReading[] Supplies,
    string[] Notes)
{
    public bool HasBattery => Supplies.Any(IsBattery);

    public bool? IsExternalPowerConnected
    {
        get
        {
            var externalSupplies = Supplies.Where(IsExternalPowerSupply).ToArray();
            if (externalSupplies.Length == 0)
                return null;

            if (externalSupplies.Any(supply => supply.IsOnline == true))
                return true;

            return externalSupplies.All(supply => supply.IsOnline == false) ? false : null;
        }
    }

    public static PowerStatus Unknown(string source, params string[] notes) => new(source, [], notes);

    private static bool IsBattery(PowerSupplyReading supply) =>
        supply.Type.Equals("Battery", StringComparison.OrdinalIgnoreCase) &&
        supply.IsPresent != false;

    private static bool IsExternalPowerSupply(PowerSupplyReading supply) =>
        supply.Type.Equals("Mains", StringComparison.OrdinalIgnoreCase) ||
        supply.Type.Equals("AC", StringComparison.OrdinalIgnoreCase) ||
        supply.Type.Equals("USB", StringComparison.OrdinalIgnoreCase) ||
        supply.Name.Contains("AC", StringComparison.OrdinalIgnoreCase);
}

internal sealed record PowerSupplyReading(
    string Name,
    string Type,
    string Status,
    double? ChargePercent,
    double? EnergyNowWh,
    double? EnergyFullWh,
    double? EnergyFullDesignWh,
    double? PowerDrawW,
    double? VoltageV,
    int? CycleCount,
    bool? IsOnline,
    bool? IsPresent,
    string Health,
    string Source);

internal sealed class PowerStatusReader(
    IFileSystem fileSystem,
    ICommandRunner commandRunner)
{
    public PowerStatus Read()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxPowerStatusProvider(fileSystem).Read();

        if (OperatingSystem.IsMacOS())
            return new MacPowerStatusProvider(commandRunner).Read();

        return PowerStatus.Unknown("runtime", "No cross-platform power provider is available for this OS.");
    }
}

internal sealed class LinuxPowerStatusProvider(IFileSystem fileSystem)
{
    private const string PowerSupplyRoot = "/sys/class/power_supply";

    public PowerStatus Read()
    {
        var supplies = new List<PowerSupplyReading>();

        foreach (var directory in fileSystem.EnumerateDirectories(PowerSupplyRoot).OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = FirstPresent(Normalize(Path.GetFileName(directory.TrimEnd('/'))), "power-supply");
            var type = FirstPresent(Normalize(ReadField(directory, "type")), InferType(name));
            var voltageMicrovolts = TryParseDouble(ReadField(directory, "voltage_now"));

            supplies.Add(new PowerSupplyReading(
                name,
                type,
                Normalize(ReadField(directory, "status")),
                TryParseDouble(ReadField(directory, "capacity")),
                ReadEnergyWh(directory, "energy_now", voltageMicrovolts, "charge_now"),
                ReadEnergyWh(directory, "energy_full", voltageMicrovolts, "charge_full"),
                ReadEnergyWh(directory, "energy_full_design", voltageMicrovolts, "charge_full_design"),
                ReadPowerW(directory, voltageMicrovolts),
                voltageMicrovolts is null ? null : Math.Round(voltageMicrovolts.Value / 1_000_000.0, 2),
                TryParseInt(ReadField(directory, "cycle_count")),
                ParseBoolean(ReadField(directory, "online")),
                ParseBoolean(ReadField(directory, "present")) ?? (type.Equals("Battery", StringComparison.OrdinalIgnoreCase) ? true : null),
                Normalize(ReadField(directory, "health")),
                "linux-power-supply"));
        }

        var notes = supplies.Count == 0
            ? ["No readable Linux power supplies were found in /sys/class/power_supply."]
            : Array.Empty<string>();

        return new PowerStatus("linux-power-supply", supplies.ToArray(), notes);
    }

    private string ReadField(string directory, string fileName) =>
        fileSystem.ReadAllText($"{directory.TrimEnd('/')}/{fileName}");

    private double? ReadEnergyWh(string directory, string energyFileName, double? voltageMicrovolts, string chargeFileName)
    {
        var energyMicrowattHours = TryParseDouble(ReadField(directory, energyFileName));
        if (energyMicrowattHours is not null)
            return Math.Round(energyMicrowattHours.Value / 1_000_000.0, 2);

        var chargeMicroampHours = TryParseDouble(ReadField(directory, chargeFileName));
        if (chargeMicroampHours is null || voltageMicrovolts is null)
            return null;

        var wattHours = chargeMicroampHours.Value / 1_000_000.0 * voltageMicrovolts.Value / 1_000_000.0;
        return Math.Round(wattHours, 2);
    }

    private double? ReadPowerW(string directory, double? voltageMicrovolts)
    {
        var powerMicrowatts = TryParseDouble(ReadField(directory, "power_now"));
        if (powerMicrowatts is not null)
            return Math.Round(powerMicrowatts.Value / 1_000_000.0, 2);

        var currentMicroamps = TryParseDouble(ReadField(directory, "current_now"));
        if (currentMicroamps is null || voltageMicrovolts is null)
            return null;

        var watts = currentMicroamps.Value / 1_000_000.0 * voltageMicrovolts.Value / 1_000_000.0;
        return Math.Round(watts, 2);
    }

    private static string InferType(string name)
    {
        if (name.StartsWith("BAT", StringComparison.OrdinalIgnoreCase))
            return "Battery";

        if (name.StartsWith("AC", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("ADP", StringComparison.OrdinalIgnoreCase))
            return "Mains";

        return string.Empty;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static double? TryParseDouble(string value)
    {
        var match = Regex.Match(value, @"[-+]?\d+(?:\.\d+)?");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? TryParseInt(string value)
    {
        var number = TryParseDouble(value);
        return number is null ? null : (int)Math.Round(number.Value, MidpointRounding.AwayFromZero);
    }

    private static bool? ParseBoolean(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static string FirstPresent(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}

internal sealed class MacPowerStatusProvider(ICommandRunner commandRunner)
{
    public PowerStatus Read()
    {
        var pmset = commandRunner.Run("pmset", "-g", "batt");
        var systemProfiler = commandRunner.Run("system_profiler", "SPPowerDataType");
        var supplies = new List<PowerSupplyReading>();

        var powerSource = ExtractPowerSource(pmset);
        if (!string.IsNullOrWhiteSpace(powerSource))
        {
            supplies.Add(new PowerSupplyReading(
                "AC Power",
                "Mains",
                powerSource,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                powerSource.Contains("AC", StringComparison.OrdinalIgnoreCase),
                true,
                string.Empty,
                "macos-pmset"));
        }

        var battery = ReadBattery(pmset, systemProfiler);
        if (battery is not null)
            supplies.Add(battery);

        var notes = supplies.Count == 0
            ? ["No readable macOS power data was returned by pmset."]
            : Array.Empty<string>();

        return new PowerStatus("macos-pmset-system-profiler", supplies.ToArray(), notes);
    }

    private static PowerSupplyReading? ReadBattery(string pmset, string systemProfiler)
    {
        var match = Regex.Match(
            pmset,
            @"^\s*-(?<name>[^\t(]+).*?\)\s*(?<percent>\d+(?:\.\d+)?)%;\s*(?<status>[^;]+);.*?present:\s*(?<present>true|false)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        if (!match.Success)
            return null;

        return new PowerSupplyReading(
            Normalize(match.Groups["name"].Value),
            "Battery",
            Normalize(match.Groups["status"].Value),
            TryParseDouble(match.Groups["percent"].Value),
            null,
            null,
            null,
            null,
            null,
            ExtractSystemProfilerInt(systemProfiler, "Cycle Count"),
            null,
            ParseBoolean(match.Groups["present"].Value),
            ExtractSystemProfilerValue(systemProfiler, "Condition"),
            "macos-pmset-system-profiler");
    }

    private static string ExtractPowerSource(string text)
    {
        var match = Regex.Match(text, @"Now drawing from '(?<source>[^']+)'", RegexOptions.IgnoreCase);
        return match.Success ? Normalize(match.Groups["source"].Value) : string.Empty;
    }

    private static string ExtractSystemProfilerValue(string text, string key)
    {
        var match = Regex.Match(
            text,
            $@"^\s*{Regex.Escape(key)}:\s*(?<value>.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? Normalize(match.Groups["value"].Value) : string.Empty;
    }

    private static int? ExtractSystemProfilerInt(string text, string key)
    {
        var value = ExtractSystemProfilerValue(text, key);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static double? TryParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool? ParseBoolean(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        return null;
    }
}
