using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class PowerProfileProviderTests
{
    [Fact]
    public void LinuxProvider_ShouldReadActivePowerprofilesctlProfile()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["powerprofilesctl"] = Success("""
              power-saver:
                Driver:     placeholder

            * balanced:
                Driver:     placeholder

              performance:
                Driver:     placeholder
                Degraded:   no
            """)
        });

        var status = new LinuxPowerProfileProvider(runner).Read();

        status.Source.Should().Be("linux-powerprofilesctl");
        status.ActiveProfile.Should().Be("balanced");
        status.CanSetProfile.Should().BeTrue();
        status.AvailableProfiles.Should().BeEquivalentTo(
        [
            new PowerProfileOption("power-saver", "Power saver", false),
            new PowerProfileOption("balanced", "Balanced", true),
            new PowerProfileOption("performance", "Performance", false)
        ]);
        status.Notes.Should().BeEmpty();
    }

    [Fact]
    public void LinuxProvider_SetProfile_ShouldNormalizeAndRunPowerprofilesctl()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["powerprofilesctl set power-saver"] = Success("")
        });

        var result = new LinuxPowerProfileProvider(runner).SetProfile("quiet");

        result.Succeeded.Should().BeTrue();
        result.ProfileId.Should().Be("power-saver");
        runner.Commands.Should().ContainSingle().Which.Should().Be("powerprofilesctl set power-saver");
    }

    [Fact]
    public void LinuxProvider_SetProfile_WhenProfileIsUnknown_ShouldFailBeforeRunningCommand()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>());

        var result = new LinuxPowerProfileProvider(runner).SetProfile("turbo");

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("power-saver");
        runner.Commands.Should().BeEmpty();
    }

    [Fact]
    public void MacProvider_ShouldReadLowPowerMode()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["pmset -g custom"] = Success("""
            Battery Power:
             lowpowermode         1
             displaysleep         2
            AC Power:
             lowpowermode         1
             displaysleep         10
            """)
        });

        var status = new MacPowerProfileProvider(runner).Read();

        status.Source.Should().Be("macos-pmset");
        status.ActiveProfile.Should().Be("low-power");
        status.CanSetProfile.Should().BeTrue();
        status.AvailableProfiles.Should().BeEquivalentTo(
        [
            new PowerProfileOption("automatic", "Automatic", false),
            new PowerProfileOption("low-power", "Low power", true)
        ]);
        status.Notes.Should().BeEmpty();
    }

    [Fact]
    public void MacProvider_SetProfile_ShouldRunPmset()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["pmset -a lowpowermode 0"] = Success("")
        });

        var result = new MacPowerProfileProvider(runner).SetProfile("balanced");

        result.Succeeded.Should().BeTrue();
        result.ProfileId.Should().Be("automatic");
        runner.Commands.Should().ContainSingle().Which.Should().Be("pmset -a lowpowermode 0");
    }

    [Fact]
    public void Provider_WhenCommandFails_ShouldSurfaceCommandSummary()
    {
        var runner = new FakeCommandRunner(new Dictionary<string, CommandResult>
        {
            ["powerprofilesctl set performance"] = new(1, "", "performance profile unavailable")
        });

        var result = new LinuxPowerProfileProvider(runner).SetProfile("performance");

        result.Succeeded.Should().BeFalse();
        result.Detail.Should().Contain("performance profile unavailable");
    }

    private static CommandResult Success(string output) => new(0, output, "");

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
