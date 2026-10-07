using UniversalDeviceToolkit.Tests;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

[CollectionDefinition(TestCollections.ProcessState, DisableParallelization = true)]
public sealed class ProcessStateTestCollectionDefinition;

[Collection(TestCollections.ProcessState)]
public sealed class ShellRestartArgumentsTests
{
    [Theory]
    [InlineData("--diagnose")]
    [InlineData("--diagnose-ui")]
    public void DiagnosticModes_EnableHostDataAndHardwareIsolation(string argument)
    {
        var directory = Directory.CreateTempSubdirectory("udt-shell-diagnostic-");
        try
        {
            using var scope = new EnvironmentVariableScope("UDT_APPDATA_OVERRIDE", directory.FullName);
            var configuration = ShellConfiguration.Load([argument]);

            Assert.True(configuration.Diagnostic);
            Assert.DoesNotContain(argument, configuration.HostArguments);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void LoadRecoveryArguments_OnlyForwardsHostOptionsFromBothArgumentSources()
    {
        var directory = Directory.CreateTempSubdirectory("udt-shell-args-");
        try
        {
            using var scope = new EnvironmentVariableScope("UDT_APPDATA_OVERRIDE", directory.FullName);
            File.WriteAllLines(Path.Combine(directory.FullName, "args.txt"), ["--safe-start", "--restart-after", "99"]);
            var defaults = ShellConfiguration.Load([]).HostArguments;

            var configuration = ShellConfiguration.Load(["--restart-after", "100", "--diagnose-ui", "--minimized", "--no-hardware"]);

            Assert.Equal(new[] { "--no-hardware" }.Concat(defaults), configuration.HostArguments);
            Assert.Contains("--safe-start", configuration.HostArguments);
            Assert.DoesNotContain("--restart-after", configuration.HostArguments);
            Assert.DoesNotContain("99", configuration.HostArguments);
            Assert.DoesNotContain("100", configuration.HostArguments);
            Assert.True(configuration.Diagnostic);
            Assert.True(configuration.StartMinimized);
            Assert.Equal(directory.FullName, configuration.DataDirectory);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void LoadIncompleteWaitMarker_PreservesTheFollowingHostFlag()
    {
        var directory = Directory.CreateTempSubdirectory("udt-shell-args-");
        try
        {
            using var scope = new EnvironmentVariableScope("UDT_APPDATA_OVERRIDE", directory.FullName);
            var defaults = ShellConfiguration.Load([]).HostArguments;

            var configuration = ShellConfiguration.Load(["--restart-after", "--no-hardware", "--restart-after"]);

            Assert.Equal(new[] { "--no-hardware" }.Concat(defaults), configuration.HostArguments);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public void Recovery_RetainsCustomPathsAndHostAndShellFlags()
    {
        string[] arguments = ["--host", @"C:\Custom Host\Host.exe", "--ui", @"C:\Custom UI", "--no-hardware", "--safe-start", "--minimized", "--elevation-checked"];
        var expected = arguments.Concat(["--restart-after", "1234"]).ToArray();
        Assert.Equal(expected, ShellConfiguration.BuildRestartArguments(arguments, 1234));
        Assert.Equal(8, arguments.Length);
    }

    [Fact]
    public void RepeatedRecovery_ReplacesEveryPreviousWaitMarker()
    {
        string[] arguments = ["--restart-after", "10", "--no-hardware", "--restart-after", "11", "--ui", @"C:\UI"];
        var first = ShellConfiguration.BuildRestartArguments(arguments, 12);
        var second = ShellConfiguration.BuildRestartArguments(first, 13);
        Assert.Equal(new[] { "--no-hardware", "--ui", @"C:\UI", "--restart-after", "13" }, second);
    }

    [Fact]
    public void IncompleteWaitMarker_DoesNotConsumeTheFollowingHostFlag()
    {
        Assert.Equal(new[] { "--no-hardware", "--restart-after", "15" },
            ShellConfiguration.BuildRestartArguments(["--restart-after", "--no-hardware", "--restart-after"], 15));
    }

    [Fact]
    public void DefaultStartup_OnlyAddsTheNewWaitMarker()
    {
        Assert.Equal(new[] { "--restart-after", "42" }, ShellConfiguration.BuildRestartArguments([], 42));
    }
}
