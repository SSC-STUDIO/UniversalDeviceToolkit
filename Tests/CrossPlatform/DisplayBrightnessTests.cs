using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class DisplayBrightnessTests
{
    [Fact]
    public void LinuxProvider_ShouldReadBacklightBrightness()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/backlight/intel_backlight/brightness"] = "480\n",
            ["/sys/class/backlight/intel_backlight/max_brightness"] = "960\n"
        });

        var status = new LinuxDisplayBrightnessProvider(fileSystem).Read();

        status.Source.Should().Be("linux-backlight");
        status.Devices.Should().ContainEquivalentOf(new DisplayBrightnessDevice(
            "intel_backlight",
            "intel_backlight",
            480,
            960,
            50,
            "/sys/class/backlight/intel_backlight/brightness",
            "linux-backlight"));
        status.Notes.Should().BeEmpty();
    }

    [Fact]
    public void LinuxProvider_WhenNoBacklightExists_ShouldReturnNote()
    {
        var status = new LinuxDisplayBrightnessProvider(new FakeFileSystem(new Dictionary<string, string>())).Read();

        status.Devices.Should().BeEmpty();
        status.Notes.Should().ContainSingle(note => note.Contains("/sys/class/backlight", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Writer_ShouldWriteRawBacklightValueForPercent()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/class/backlight/amdgpu_bl0/brightness"] = "25\n",
            ["/sys/class/backlight/amdgpu_bl0/max_brightness"] = "250\n"
        });
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["sh -c printf %s 150 > '/sys/class/backlight/amdgpu_bl0/brightness'"] = new(0, "", "")
        });

        var result = new DisplayBrightnessWriter(fileSystem, runner, CrossPlatformControlPlatform.Linux).SetBrightnessPercent("60");

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be("60");
        runner.Commands.Should().ContainSingle().Which.Should().Be("sh -c printf %s 150 > '/sys/class/backlight/amdgpu_bl0/brightness'");
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("101")]
    [InlineData("bright")]
    public void Writer_WhenPercentIsInvalid_ShouldFailBeforeRunningCommand(string value)
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>());

        var result = new DisplayBrightnessWriter(new FakeFileSystem(new Dictionary<string, string>()), runner, CrossPlatformControlPlatform.Linux).SetBrightnessPercent(value);

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("0 to 100");
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
