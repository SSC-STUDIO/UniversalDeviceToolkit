using System.Globalization;
using System.Text.RegularExpressions;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Platform.Linux.IO;

namespace UniversalDeviceToolkit.Platform.Linux.Hardware;

/// <summary>Reads <c>/sys/class/power_supply</c> for dashboard battery and AC adapter status.</summary>
public static class LinuxPowerSupplyReader
{
    public const string PowerSupplyRoot = "/sys/class/power_supply";

    public static void AddReadings(ILinuxFileSystem fileSystem, List<SensorReading> readings)
    {
        var snapshot = Read(fileSystem);
        var battery = snapshot.Batteries.FirstOrDefault();
        if (battery is null)
            return;

        if (battery.ChargePercent is not null)
            readings.Add(new SensorReading("Battery", "Charge", battery.ChargePercent.Value, "%"));
        if (battery.HealthPercent is not null)
            readings.Add(new SensorReading("Battery", "Health", battery.HealthPercent.Value, "%"));
        if (battery.TemperatureC is not null)
            readings.Add(new SensorReading("Battery", "Temperature", battery.TemperatureC.Value, "°C"));
        if (battery.PowerMilliwatts is not null)
            readings.Add(new SensorReading("Battery", "Power", battery.PowerMilliwatts.Value, "mW"));
        if (battery.VoltageV is not null)
            readings.Add(new SensorReading("Battery", "Voltage", battery.VoltageV.Value, "V"));
        if (battery.DesignCapacityMwh is not null)
            readings.Add(new SensorReading("Battery", "DesignCapacity", battery.DesignCapacityMwh.Value, "mWh"));
        if (battery.FullChargeCapacityMwh is not null)
            readings.Add(new SensorReading("Battery", "FullChargeCapacity", battery.FullChargeCapacityMwh.Value, "mWh"));
        if (battery.CycleCount is not null)
            readings.Add(new SensorReading("Battery", "CycleCount", battery.CycleCount.Value, ""));
        readings.Add(new SensorReading("Battery", "Charging", battery.IsCharging ? 1 : 0, ""));
        if (!string.IsNullOrWhiteSpace(battery.ModelName))
            readings.Add(new SensorReading(battery.ModelName, "BatteryIdentity", 0, ""));
    }

    public static LinuxPowerSnapshot Read(ILinuxFileSystem fileSystem)
    {
        var batteries = new List<LinuxBatteryInfo>();
        bool? acOnline = null;

        foreach (var directory in fileSystem.EnumerateDirectories(PowerSupplyRoot))
        {
            var name = Path.GetFileName(directory.TrimEnd('/')) ?? "power-supply";
            var type = Normalize(fileSystem.ReadText(Combine(directory, "type"))) ?? InferType(name);

            if (IsExternalPower(type, name))
            {
                var online = ParseBoolean(fileSystem.ReadText(Combine(directory, "online")));
                if (online == true)
                    acOnline = true;
                else if (online == false && acOnline is not true)
                    acOnline = false;
                continue;
            }

            if (!IsBattery(type, name, fileSystem, directory))
                continue;

            batteries.Add(ReadBattery(fileSystem, directory, name));
        }

        var adapterStatus = acOnline switch
        {
            true => "Connected",
            false => "Disconnected",
            _ => batteries.Count > 0 && batteries.Any(battery => battery.IsCharging)
                ? "Connected"
                : batteries.Count > 0 ? "Disconnected" : null
        };

        return new LinuxPowerSnapshot(batteries, adapterStatus);
    }

    public static LinuxChargeThreshold? ReadChargeThreshold(ILinuxFileSystem fileSystem)
    {
        foreach (var directory in fileSystem.EnumerateDirectories(PowerSupplyRoot))
        {
            var endPath = Combine(directory, "charge_control_end_threshold");
            if (!fileSystem.FileExists(endPath))
                continue;

            var startPath = Combine(directory, "charge_control_start_threshold");
            return new LinuxChargeThreshold(
                Path.GetFileName(directory.TrimEnd('/')) ?? "BAT",
                ReadInt(fileSystem.ReadText(startPath)),
                ReadInt(fileSystem.ReadText(endPath)),
                startPath,
                endPath);
        }

        return null;
    }

    public static bool TryWriteChargeEndThreshold(ILinuxFileSystem fileSystem, int percent, out string? error)
    {
        error = null;
        var threshold = ReadChargeThreshold(fileSystem);
        if (threshold is null)
        {
            error = "No Linux charge_control_end_threshold sysfs control was found.";
            return false;
        }

        try
        {
            File.WriteAllText(threshold.EndPath, percent.ToString(CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static LinuxBatteryInfo ReadBattery(ILinuxFileSystem fileSystem, string directory, string name)
    {
        var voltageUv = ReadDouble(fileSystem.ReadText(Combine(directory, "voltage_now")));
        var status = Normalize(fileSystem.ReadText(Combine(directory, "status"))) ?? string.Empty;
        var energyNow = ReadEnergyMwh(fileSystem, directory, "energy_now", voltageUv, "charge_now");
        var energyFull = ReadEnergyMwh(fileSystem, directory, "energy_full", voltageUv, "charge_full");
        var energyDesign = ReadEnergyMwh(fileSystem, directory, "energy_full_design", voltageUv, "charge_full_design");
        var powerMw = ReadPowerMilliwatts(fileSystem, directory, voltageUv, status);
        var tempMilli = ReadDouble(fileSystem.ReadText(Combine(directory, "temp")));
        var health = energyFull is > 0 && energyDesign is > 0
            ? Math.Round(Math.Clamp(energyFull.Value / energyDesign.Value * 100.0, 0, 200), 1)
            : (double?)null;
        var model = FirstPresent(
            Normalize(fileSystem.ReadText(Combine(directory, "model_name"))),
            Normalize(fileSystem.ReadText(Combine(directory, "manufacturer"))),
            name);

        return new LinuxBatteryInfo(
            name,
            ReadDouble(fileSystem.ReadText(Combine(directory, "capacity"))),
            health,
            tempMilli is null ? null : Math.Round(tempMilli.Value / 10.0, 1),
            powerMw,
            voltageUv is null ? null : Math.Round(voltageUv.Value / 1_000_000.0, 2),
            energyDesign,
            energyFull,
            ReadInt(fileSystem.ReadText(Combine(directory, "cycle_count"))),
            status.Contains("Charging", StringComparison.OrdinalIgnoreCase),
            model);
    }

    private static double? ReadEnergyMwh(ILinuxFileSystem fileSystem, string directory, string energyFile, double? voltageUv, string chargeFile)
    {
        var energyUwh = ReadDouble(fileSystem.ReadText(Combine(directory, energyFile)));
        if (energyUwh is not null)
            return Math.Round(energyUwh.Value / 1000.0, 1);

        var chargeUah = ReadDouble(fileSystem.ReadText(Combine(directory, chargeFile)));
        if (chargeUah is null || voltageUv is null)
            return null;

        var wattHours = chargeUah.Value / 1_000_000.0 * voltageUv.Value / 1_000_000.0;
        return Math.Round(wattHours * 1000.0, 1);
    }

    private static double? ReadPowerMilliwatts(ILinuxFileSystem fileSystem, string directory, double? voltageUv, string status)
    {
        var powerUw = ReadDouble(fileSystem.ReadText(Combine(directory, "power_now")));
        double? milliwatts = null;
        if (powerUw is not null)
            milliwatts = powerUw.Value / 1000.0;
        else
        {
            var currentUa = ReadDouble(fileSystem.ReadText(Combine(directory, "current_now")));
            if (currentUa is not null && voltageUv is not null)
                milliwatts = currentUa.Value / 1_000_000.0 * voltageUv.Value / 1_000_000.0 * 1000.0;
        }

        if (milliwatts is null)
            return null;

        if (status.Contains("Discharging", StringComparison.OrdinalIgnoreCase) && milliwatts > 0)
            milliwatts = -milliwatts;

        return Math.Round(milliwatts.Value, 1);
    }

    private static bool IsBattery(string type, string name, ILinuxFileSystem fileSystem, string directory)
    {
        if (type.Equals("Battery", StringComparison.OrdinalIgnoreCase) || name.StartsWith("BAT", StringComparison.OrdinalIgnoreCase))
        {
            var present = ParseBoolean(fileSystem.ReadText(Combine(directory, "present")));
            return present != false;
        }

        return false;
    }

    private static bool IsExternalPower(string type, string name) =>
        type.Equals("Mains", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("USB", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("AC", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("AC", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("ADP", StringComparison.OrdinalIgnoreCase);

    private static string InferType(string name)
    {
        if (name.StartsWith("BAT", StringComparison.OrdinalIgnoreCase))
            return "Battery";
        if (name.StartsWith("AC", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ADP", StringComparison.OrdinalIgnoreCase))
            return "Mains";
        return string.Empty;
    }

    private static string Combine(string directory, string fileName) =>
        $"{directory.TrimEnd('/')}/{fileName}";

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string FirstPresent(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static double? ReadDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var match = Regex.Match(value, @"[-+]?\d+(?:\.\d+)?");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? ReadInt(string? value)
    {
        var number = ReadDouble(value);
        return number is null ? null : (int)Math.Round(number.Value, MidpointRounding.AwayFromZero);
    }

    private static bool? ParseBoolean(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
            return null;
        if (normalized is "1" or "true" or "yes")
            return true;
        if (normalized is "0" or "false" or "no")
            return false;
        return null;
    }
}

public sealed record LinuxPowerSnapshot(IReadOnlyList<LinuxBatteryInfo> Batteries, string? AdapterStatus);

public sealed record LinuxBatteryInfo(
    string Name,
    double? ChargePercent,
    double? HealthPercent,
    double? TemperatureC,
    double? PowerMilliwatts,
    double? VoltageV,
    double? DesignCapacityMwh,
    double? FullChargeCapacityMwh,
    int? CycleCount,
    bool IsCharging,
    string ModelName);

public sealed record LinuxChargeThreshold(
    string BatteryId,
    int? StartPercent,
    int? EndPercent,
    string StartPath,
    string EndPath);
