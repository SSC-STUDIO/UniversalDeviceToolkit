using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.System;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

/// <summary>
/// Integration tests for CMD.RunAsync that execute real system commands.
/// These tests verify the command execution works with actual Windows tools.
/// </summary>
[Trait("Category", "Integration")]
public class CMDIntegrationTests
{
    [Fact]
    public async Task RunAsync_WithWhoami_ShouldReturnUsername()
    {
        var (exitCode, output) = await CMD.RunAsync("whoami.exe", "", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_WithIpconfig_ShouldReturnNetworkInfo()
    {
        var (exitCode, output) = await CMD.RunAsync("ipconfig.exe", "", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_WithDirCommand_ShouldReturnDirectoryListing()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c dir %TEMP%", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_WithSetCommand_ShouldReturnEnvironmentVariables()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c set", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().Contain("PATH");
    }

    [Fact]
    public async Task RunAsync_WithVerCommand_ShouldReturnWindowsVersion()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c ver", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().Contain("Microsoft");
    }

    [Fact]
    public async Task RunAsync_WithAssocCommand_ShouldReturnAssociations()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c assoc .txt", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().Contain(".txt");
    }

    [Fact]
    public async Task RunAsync_WithFtypeCommand_ShouldReturnFileTypes()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c ftype", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_WithPathCommand_ShouldReturnPath()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c path", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().Contain("PATH");
    }

    [Fact]
    public async Task RunAsync_WithModeCommand_ShouldReturnMode()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c mode con", waitForExit: true);
        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithChcpCommand_ShouldReturnCodePage()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c chcp", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().MatchRegex(@"(?i)(code page|\u6d3b\u52a8\u4ee3\u7801\u9875|\d{3,5})");
    }

    [Fact]
    public async Task RunAsync_WithSystemCommand_ShouldExecute()
    {
        var (exitCode, output) = await CMD.RunAsync("powershell.exe", "-Command Write-Host 'PowerShell test'", waitForExit: true);
        exitCode.Should().Be(0);
        output.Should().Contain("PowerShell test");
    }

    [Fact]
    public async Task RunAsync_WithConcurrentCommands_ShouldNotInterfere()
    {
        var tasks = new List<Task<(int exitCode, string output)>>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(CMD.RunAsync("cmd.exe", $"/c echo test{index}", waitForExit: true));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.exitCode == 0);
    }
}
