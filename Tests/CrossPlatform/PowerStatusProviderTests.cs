using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class PowerStatusProviderTests
{
    [Fact]
    public void LinuxProvider_ShouldReadBatteryAndAcStatus()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/power_supply/AC/type"] = "Mains\n",
            ["/sys/class/power_supply/AC/online"] = "1\n",
            ["/sys/class/power_supply/BAT0/type"] = "Battery\n",
            ["/sys/class/power_supply/BAT0/status"] = "Discharging\n",
            ["/sys/class/power_supply/BAT0/capacity"] = "81\n",
            ["/sys/class/power_supply/BAT0/energy_now"] = "51234000\n",
            ["/sys/class/power_supply/BAT0/energy_full"] = "75000000\n",
            ["/sys/class/power_supply/BAT0/energy_full_design"] = "80000000\n",
            ["/sys/class/power_supply/BAT0/power_now"] = "13450000\n",
            ["/sys/class/power_supply/BAT0/voltage_now"] = "15500000\n",
            ["/sys/class/power_supply/BAT0/cycle_count"] = "42\n",
            ["/sys/class/power_supply/BAT0/present"] = "1\n",
            ["/sys/class/power_supply/BAT0/health"] = "Good\n",
        });

        var power = new LinuxPowerStatusProvider(fileSystem).Read();

        power.Source.Should().Be("linux-power-supply");
        power.HasBattery.Should().BeTrue();
        power.IsExternalPowerConnected.Should().BeTrue();
        power.Notes.Should().BeEmpty();
        power.Supplies.Should().ContainEquivalentOf(new PowerSupplyReading(
            "AC",
            "Mains",
            "",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            null,
            "",
            "linux-power-supply"));
        power.Supplies.Should().ContainEquivalentOf(new PowerSupplyReading(
            "BAT0",
            "Battery",
            "Discharging",
            81,
            51.23,
            75,
            80,
            13.45,
            15.5,
            42,
            null,
            true,
            "Good",
            "linux-power-supply"));
    }

    [Fact]
    public void LinuxProvider_ShouldComputeEnergyAndPowerFromChargeAndVoltage()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/power_supply/BAT0/type"] = "Battery\n",
            ["/sys/class/power_supply/BAT0/status"] = "Charging\n",
            ["/sys/class/power_supply/BAT0/capacity"] = "50\n",
            ["/sys/class/power_supply/BAT0/charge_now"] = "2500000\n",
            ["/sys/class/power_supply/BAT0/charge_full"] = "5000000\n",
            ["/sys/class/power_supply/BAT0/charge_full_design"] = "6000000\n",
            ["/sys/class/power_supply/BAT0/current_now"] = "1200000\n",
            ["/sys/class/power_supply/BAT0/voltage_now"] = "12000000\n",
        });

        var battery = new LinuxPowerStatusProvider(fileSystem).Read().Supplies.Single();

        battery.EnergyNowWh.Should().Be(30);
        battery.EnergyFullWh.Should().Be(60);
        battery.EnergyFullDesignWh.Should().Be(72);
        battery.PowerDrawW.Should().Be(14.4);
        battery.VoltageV.Should().Be(12);
    }

    [Fact]
    public void LinuxProvider_WhenNoPowerSupplies_ShouldReturnNote()
    {
        var power = new LinuxPowerStatusProvider(new FakeFileSystem(new Dictionary<string, string>())).Read();

        power.Supplies.Should().BeEmpty();
        power.Notes.Should().ContainSingle(note => note.Contains("/sys/class/power_supply", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MacProvider_ShouldReadPmsetAndSystemProfilerBattery()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, string>
        {
            ["pmset -g batt"] = """
                Now drawing from 'Battery Power'
                 -InternalBattery-0 (id=1234567)	87%; discharging; 7:12 remaining present: true
                """,
            ["system_profiler SPPowerDataType"] = """
                Power:

                    Battery Information:

                      Health Information:
                          Cycle Count: 88
                          Condition: Normal
                """
        });

        var power = new MacPowerStatusProvider(runner).Read();

        power.Source.Should().Be("macos-pmset-system-profiler");
        power.HasBattery.Should().BeTrue();
        power.IsExternalPowerConnected.Should().BeFalse();
        power.Supplies.Should().ContainEquivalentOf(new PowerSupplyReading(
            "AC Power",
            "Mains",
            "Battery Power",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            true,
            "",
            "macos-pmset"));
        power.Supplies.Should().ContainEquivalentOf(new PowerSupplyReading(
            "InternalBattery-0",
            "Battery",
            "discharging",
            87,
            null,
            null,
            null,
            null,
            null,
            88,
            null,
            true,
            "Normal",
            "macos-pmset-system-profiler"));
    }

    private sealed class FakeFileSystem(IReadOnlyDictionary<string, string> files) : IFileSystem
    {
        public string ReadAllText(string path) => files.TryGetValue(path, out var value) ? value : string.Empty;

        public IEnumerable<string> EnumerateDirectories(string path) =>
            files.Keys
                .Where(file => file.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal))
                .Select(file =>
                {
                    var relativePath = file[(path.TrimEnd('/').Length + 1)..];
                    var separator = relativePath.IndexOf('/');
                    return separator < 0 ? string.Empty : $"{path.TrimEnd('/')}/{relativePath[..separator]}";
                })
                .Where(directory => !string.IsNullOrWhiteSpace(directory))
                .Distinct(StringComparer.Ordinal);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            var regex = new Regex(
                "^" + Regex.Escape(searchPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase);

            return files.Keys
                .Where(file => file.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal))
                .Where(file => !file[(path.TrimEnd('/').Length + 1)..].Contains('/'))
                .Where(file => regex.IsMatch(Path.GetFileName(file)));
        }

        public bool DirectoryExists(string path) =>
            files.Keys.Any(file => file.StartsWith(path.TrimEnd('/') + "/", StringComparison.Ordinal));

        public string GetFileName(string path) => path.TrimEnd('/', '\\').Split('/', '\\').Last();
    }

    private sealed class FakeCommandRunner(IReadOnlyDictionary<string, string> outputs) : ICommandRunner
    {
        public string Run(string fileName, params string[] arguments)
        {
            var key = string.Join(' ', new[] { fileName }.Concat(arguments));
            return outputs.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }
}
