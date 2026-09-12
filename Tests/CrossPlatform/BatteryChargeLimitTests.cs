using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class BatteryChargeLimitTests
{
    [Fact]
    public void LinuxProvider_ShouldReadChargeThresholds()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/power_supply/BAT0/type"] = "Battery\n",
            ["/sys/class/power_supply/BAT0/charge_control_start_threshold"] = "40\n",
            ["/sys/class/power_supply/BAT0/charge_control_end_threshold"] = "80\n"
        });

        var status = new LinuxBatteryChargeLimitProvider(fileSystem).Read();

        status.Source.Should().Be("linux-power-supply-threshold");
        status.Devices.Should().ContainEquivalentOf(new BatteryChargeLimitDevice(
            "BAT0",
            "BAT0",
            40,
            80,
            "/sys/class/power_supply/BAT0/charge_control_start_threshold",
            "/sys/class/power_supply/BAT0/charge_control_end_threshold",
            "linux-power-supply-threshold"));
        status.Notes.Should().BeEmpty();
    }

    [Fact]
    public void LinuxProvider_WhenNoThresholdsExist_ShouldReturnNote()
    {
        var status = new LinuxBatteryChargeLimitProvider(new FakeFileSystem(new Dictionary<string, string>())).Read();

        status.Devices.Should().BeEmpty();
        status.Notes.Should().ContainSingle(note => note.Contains("/sys/class/power_supply", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Writer_ShouldSetEndThreshold()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/power_supply/BAT0/type"] = "Battery\n",
            ["/sys/class/power_supply/BAT0/charge_control_start_threshold"] = "40\n",
            ["/sys/class/power_supply/BAT0/charge_control_end_threshold"] = "80\n"
        });
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["sh -c printf %s 75 > '/sys/class/power_supply/BAT0/charge_control_end_threshold'"] = new(0, "", "")
        });

        var result = new BatteryChargeLimitWriter(fileSystem, runner, CrossPlatformControlPlatform.Linux).SetEndThreshold("75");

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be("75");
        runner.Commands.Should().ContainSingle().Which.Should().Be("sh -c printf %s 75 > '/sys/class/power_supply/BAT0/charge_control_end_threshold'");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("full")]
    public void Writer_WhenPercentIsInvalid_ShouldFailBeforeRunningCommand(string value)
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>());

        var result = new BatteryChargeLimitWriter(new FakeFileSystem(new Dictionary<string, string>()), runner, CrossPlatformControlPlatform.Linux).SetEndThreshold(value);

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("1 to 100");
        runner.Commands.Should().BeEmpty();
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

        public string GetFileName(string path) => path.TrimEnd('/').Split('/').Last();
    }

    private sealed class FakeCommandRunner(IReadOnlyDictionary<string, CommandResult> results) : ICommandResultRunner
    {
        public List<string> Commands { get; } = [];

        public string Run(string fileName, params string[] arguments)
        {
            var result = RunResult(fileName, arguments);
            return result.Succeeded ? result.StandardOutput : string.Empty;
        }

        public CommandResult RunResult(string fileName, params string[] arguments)
        {
            var key = string.Join(' ', new[] { fileName }.Concat(arguments));
            Commands.Add(key);
            return results.TryGetValue(key, out var result) ? result : new CommandResult(1, "", "not found");
        }
    }
}
