using System;
using System.IO;
using FluentAssertions;
using UniversalDeviceToolkit.Host;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class NetworkStateRecoveryCommandTests
{
    [Fact]
    public void UnrelatedArguments_DoNotRunRecovery()
    {
        var called = false;
        var exitCode = NetworkStateRecoveryCommand.TryRun(["--no-hardware"], () =>
        {
            called = true;
            return (true, "unused");
        }, new StringWriter());

        exitCode.Should().BeNull();
        called.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Recovery_ReportsResultAndReturnsItsExitCode(bool success, int expected)
    {
        using var output = new StringWriter();
        var exitCode = NetworkStateRecoveryCommand.TryRun(["--restore-network-state"],
            () => (success, "snapshot recovery result"), output);

        exitCode.Should().Be(expected);
        output.ToString().Should().Contain("snapshot recovery result");
    }

    [Fact]
    public void RecoveryException_IsReportedAndFails()
    {
        using var output = new StringWriter();
        var exitCode = NetworkStateRecoveryCommand.TryRun(["--restore-network-state"],
            () => throw new IOException("snapshot unavailable"), output);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("snapshot unavailable");
    }
}
