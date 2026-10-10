using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class CpuGovernorTests
{
    [Fact]
    public void LinuxProvider_ShouldReadPolicyGovernors()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/devices/system/cpu/cpufreq/policy0/scaling_governor"] = "schedutil\n",
            ["/sys/devices/system/cpu/cpufreq/policy0/scaling_available_governors"] = "performance powersave schedutil\n",
            ["/sys/devices/system/cpu/cpufreq/policy1/scaling_governor"] = "schedutil\n",
            ["/sys/devices/system/cpu/cpufreq/policy1/scaling_available_governors"] = "performance powersave schedutil\n"
        });

        var status = new LinuxCpuGovernorProvider(fileSystem).Read();

        status.Source.Should().Be("linux-cpufreq");
        status.ActiveGovernor.Should().Be("schedutil");
        status.CanSetGovernor.Should().BeTrue();
        status.Policies.Should().HaveCount(2);
        status.Policies.Should().ContainEquivalentOf(new CpuGovernorPolicy(
            "policy0",
            "schedutil",
            ["performance", "powersave", "schedutil"],
            "/sys/devices/system/cpu/cpufreq/policy0/scaling_governor",
            "linux-cpufreq"));
        status.AvailableGovernors.Should().ContainEquivalentOf(new CpuGovernorOption("schedutil", "Schedutil", true));
    }

    [Fact]
    public void LinuxProvider_WhenNoPoliciesExist_ShouldReturnNote()
    {
        var status = new LinuxCpuGovernorProvider(new FakeFileSystem(new Dictionary<string, string>())).Read();

        status.Policies.Should().BeEmpty();
        status.Notes.Should().ContainSingle(note => note.Contains("/sys/devices/system/cpu", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LinuxProvider_ShouldFallbackToCpuDirectories()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor"] = "powersave\n",
            ["/sys/devices/system/cpu/cpu0/cpufreq/scaling_available_governors"] = "performance powersave\n"
        });

        var status = new LinuxCpuGovernorProvider(fileSystem).Read();

        status.Policies.Should().ContainSingle().Which.Id.Should().Be("cpu0");
        status.ActiveGovernor.Should().Be("powersave");
    }

    [Fact]
    public void Writer_ShouldSetAllPoliciesThatSupportGovernor()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/devices/system/cpu/cpufreq/policy0/scaling_governor"] = "powersave\n",
            ["/sys/devices/system/cpu/cpufreq/policy0/scaling_available_governors"] = "performance powersave schedutil\n",
            ["/sys/devices/system/cpu/cpufreq/policy1/scaling_governor"] = "powersave\n",
            ["/sys/devices/system/cpu/cpufreq/policy1/scaling_available_governors"] = "performance powersave schedutil\n"
        });
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["sh -c printf %s 'performance' > '/sys/devices/system/cpu/cpufreq/policy0/scaling_governor'"] = new(0, "", ""),
            ["sh -c printf %s 'performance' > '/sys/devices/system/cpu/cpufreq/policy1/scaling_governor'"] = new(0, "", "")
        });

        var result = new CpuGovernorWriter(fileSystem, runner, CrossPlatformControlPlatform.Linux).SetGovernor("turbo");

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be("performance");
        runner.Commands.Should().BeEquivalentTo(
        [
            "sh -c printf %s 'performance' > '/sys/devices/system/cpu/cpufreq/policy0/scaling_governor'",
            "sh -c printf %s 'performance' > '/sys/devices/system/cpu/cpufreq/policy1/scaling_governor'"
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void Writer_WhenGovernorIsUnsupported_ShouldFailBeforeRunningCommand()
    {
        var fileSystem = new FakeFileSystem(new Dictionary<string, string>
        {
            ["/sys/devices/system/cpu/cpufreq/policy0/scaling_governor"] = "powersave\n",
            ["/sys/devices/system/cpu/cpufreq/policy0/scaling_available_governors"] = "performance powersave\n"
        });
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>());

        var result = new CpuGovernorWriter(fileSystem, runner, CrossPlatformControlPlatform.Linux).SetGovernor("unsupported");

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("performance");
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
