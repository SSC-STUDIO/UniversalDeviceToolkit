using System.Diagnostics;
using System.Globalization;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.NetworkProxy.Host;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public sealed class NetworkProxyOwnerMonitorTests
{
    [Fact]
    public async Task Watch_WithoutOwnerEnvironment_PreservesStandaloneLifetime()
    {
        NetworkProxyOwnerMonitor.ParseOwnerIdentity(null, null).Should().BeNull();
        using var lifetime = new CancellationTokenSource();
        await NetworkProxyOwnerMonitor.WatchAsync(null, lifetime);
        lifetime.IsCancellationRequested.Should().BeFalse();
    }

    [Theory]
    [InlineData("123", null)]
    [InlineData(null, "123")]
    [InlineData("0", "123")]
    [InlineData("123", "9223372036854775807")]
    [InlineData("invalid", "123")]
    public void ParseOwnerIdentity_WhenEnvironmentIsIncompleteOrInvalid_FailsClosed(string? id, string? ticks)
    {
        Action parse = () => NetworkProxyOwnerMonitor.ParseOwnerIdentity(id, ticks);
        parse.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Watch_WhenOwnerPidHasBeenReused_StopsWorkerLifetime()
    {
        using var current = Process.GetCurrentProcess();
        var owner = CaptureOwner(current);
        owner.StartedAtUtc = owner.StartedAtUtc.AddSeconds(-1);
        using var lifetime = new CancellationTokenSource();

        await NetworkProxyOwnerMonitor.WatchAsync(owner, lifetime);
        lifetime.IsCancellationRequested.Should().BeTrue();
        current.HasExited.Should().BeFalse();
    }

    [Fact]
    public async Task Watch_WhenWorkerShutsDown_CancelsWaitWithoutStoppingOwner()
    {
        using var current = Process.GetCurrentProcess();
        using var lifetime = new CancellationTokenSource();
        var monitor = NetworkProxyOwnerMonitor.WatchAsync(CaptureOwner(current), lifetime);
        monitor.IsCompleted.Should().BeFalse();

        lifetime.Cancel();
        await monitor.WaitAsync(TimeSpan.FromSeconds(5));
        current.HasExited.Should().BeFalse();
    }

    [Fact]
    public async Task Watch_WhenOwnerExits_StopsWorkerLifetime()
    {
        using var ownerProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
                Arguments = "/d /c set /p ownerInput=",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            }
        };
        ownerProcess.Start().Should().BeTrue();
        using var lifetime = new CancellationTokenSource();
        var monitor = NetworkProxyOwnerMonitor.WatchAsync(CaptureOwner(ownerProcess), lifetime);
        try
        {
            lifetime.IsCancellationRequested.Should().BeFalse();
            await ownerProcess.StandardInput.WriteLineAsync("continue");
            await ownerProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await monitor.WaitAsync(TimeSpan.FromSeconds(5));
            lifetime.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            lifetime.Cancel();
            if (!ownerProcess.HasExited)
                ownerProcess.Kill(entireProcessTree: true);
            await monitor.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static NetworkProcessIdentity CaptureOwner(Process process)
    {
        var ticks = process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        return NetworkProxyOwnerMonitor.ParseOwnerIdentity(process.Id.ToString(CultureInfo.InvariantCulture), ticks)
            ?? throw new InvalidOperationException("Owner identity was not parsed.");
    }
}
