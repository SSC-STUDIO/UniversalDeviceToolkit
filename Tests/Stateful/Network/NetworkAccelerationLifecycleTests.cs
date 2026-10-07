using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Unit)]
public sealed class NetworkAccelerationLifecycleTests : IAsyncLifetime
{
    private readonly string _directory;
    private readonly string? _previousAppDataOverride;

    public NetworkAccelerationLifecycleTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "udt-network-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _previousAppDataOverride = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _directory);
        Log.ResetForTests();
    }

    [Fact]
    public async Task Restore_HoldsLifecycleGateUntilSnapshotRecoveryAndModeSaveFinish()
    {
        var proxy = new SystemProxySnapshot { Server = "original:8080" };
        var recoveryEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var finishRecovery = new ManualResetEventSlim(false);
        var pauseRecovery = false;
        var recovery = new NetworkStateRecoveryService(_directory, () => string.Empty, _ => { },
            () =>
            {
                if (pauseRecovery)
                {
                    recoveryEntered.TrySetResult(true);
                    if (!finishRecovery.Wait(TimeSpan.FromSeconds(10)))
                        throw new TimeoutException("Recovery test was not released.");
                }
                return proxy;
            },
            value => proxy = value ?? throw new InvalidOperationException("Original state was not restored."),
            Path.Combine(_directory, "user-network.lease"));
        await recovery.CaptureSnapshotAsync();
        proxy = new SystemProxySnapshot { Enabled = true, Server = "127.0.0.1:34123" };
        var service = new NetworkAccelerationService(new NetworkAccelerationSettings(), recovery,
            () => NetworkAccelerationUserLease.Acquire(Path.Combine(_directory, "user-network.lease")));
        service.Config.Mode = NetworkAccelerationMode.SystemProxy;
        pauseRecovery = true;

        var restore = Task.Run(() => service.RestoreAsync());
        Task<bool>? start = null;
        var startWasBlocked = false;
        bool? startResult = null;
        NetworkStateRestoreResult? restoreResult = null;
        try
        {
            await recoveryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            start = service.StartAsync();
            startWasBlocked = !start.IsCompleted;
        }
        finally
        {
            finishRecovery.Set();
            restoreResult = await restore.WaitAsync(TimeSpan.FromSeconds(5));
            if (start is not null)
                startResult = await start.WaitAsync(TimeSpan.FromSeconds(5));
        }

        startWasBlocked.Should().BeTrue("Start must wait through snapshot recovery, not only worker stop");
        (restoreResult?.Success).Should().BeTrue();
        startResult.Should().BeFalse();
        service.Config.Mode.Should().Be(NetworkAccelerationMode.Off);
        proxy.Server.Should().Be("original:8080");
        File.Exists(recovery.SnapshotPath).Should().BeFalse();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Restore_WhenSnapshotLockIsBusy_ReturnsFailureAndPreservesSnapshotForRetry()
    {
        var proxy = new SystemProxySnapshot { Server = "original:8080" };
        var recovery = new NetworkStateRecoveryService(_directory, () => string.Empty, _ => { },
            () => proxy, value => proxy = value ?? throw new InvalidOperationException("Original state was not restored."),
            Path.Combine(_directory, "user-network.lease"));
        await recovery.CaptureSnapshotAsync();
        proxy = new SystemProxySnapshot { Enabled = true, Server = "127.0.0.1:34123" };
        var service = new NetworkAccelerationService(new NetworkAccelerationSettings(), recovery,
            () => NetworkAccelerationUserLease.Acquire(Path.Combine(_directory, "user-network.lease")));
        service.Config.Mode = NetworkAccelerationMode.SystemProxy;
        using (var snapshotLock = new FileStream(recovery.SnapshotPath + ".lock", FileMode.Open,
                   FileAccess.ReadWrite, FileShare.None))
        {
            var result = await service.RestoreAsync();
            result.Success.Should().BeFalse();
            result.Report.Should().Contain("recovery refused");
            File.Exists(recovery.SnapshotPath).Should().BeTrue();
            proxy.Server.Should().Be("127.0.0.1:34123");
            service.Config.Mode.Should().Be(NetworkAccelerationMode.Off);
            Action competing = () => NetworkAccelerationUserLease.Acquire(Path.Combine(_directory, "user-network.lease"));
            competing.Should().Throw<IOException>("failed recovery must keep the lease until a successful retry");
        }

        (await service.RestoreAsync()).Success.Should().BeTrue();
        proxy.Server.Should().Be("original:8080");
        using (NetworkAccelerationUserLease.Acquire(Path.Combine(_directory, "user-network.lease")))
        {
            File.Exists(recovery.SnapshotPath).Should().BeFalse();
        }
        await service.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_WhenSettingsWriteThrows_EntersCatchAndBorrowsItsLeaseForRecovery(bool failRecoveryRead)
    {
        var leasePath = Path.Combine(_directory, "user-network.lease");
        var proxy = new SystemProxySnapshot { Server = "original:8080" };
        var proxyReads = 0;
        var recovery = new NetworkStateRecoveryService(_directory, () => string.Empty, _ => { },
            () =>
            {
                proxyReads++;
                if (failRecoveryRead && proxyReads > 1)
                    throw new IOException("Recovery proxy read failed.");
                return proxy;
            }, value => proxy = value ?? throw new InvalidOperationException(), leasePath);
        var settings = new NetworkAccelerationSettings();
        settings.Store.AccelerationEnabled = true;
        settings.Store.Mode = NetworkAccelerationMode.SystemProxy;
        settings.Store.DomainGroups =
        [
            new NetworkDomainGroup { Id = "test", Enabled = true, Domains = ["example.com"] }
        ];
        var blockedSettingsPath = Path.Combine(Folders.AppData, NetworkAccelerationDefaults.SettingsFileName);
        Directory.CreateDirectory(blockedSettingsPath);
        var service = new NetworkAccelerationService(settings, recovery,
            () => NetworkAccelerationUserLease.Acquire(leasePath), () => true);

        (await service.StartAsync()).Should().BeFalse();
        proxyReads.Should().BeGreaterThan(1, "failed-start recovery should reach its fake IO while borrowing the lease");
        proxy.Server.Should().Be("original:8080");
        if (failRecoveryRead)
        {
            File.Exists(recovery.SnapshotPath).Should().BeTrue();
            Action competing = () => NetworkAccelerationUserLease.Acquire(leasePath);
            competing.Should().Throw<IOException>();
            failRecoveryRead = false;
            Directory.Delete(blockedSettingsPath);
            (await service.RestoreAsync()).Success.Should().BeTrue();
        }
        File.Exists(recovery.SnapshotPath).Should().BeFalse();
        using (NetworkAccelerationUserLease.Acquire(leasePath))
        {
            proxy.Server.Should().Be("original:8080");
        }
        await service.DisposeAsync();
    }

    [Fact]
    public void DefaultUserLeasePath_DoesNotFollowDataDirectoryOverrides()
    {
        var first = NetworkAccelerationUserLease.DefaultPath;
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, Path.Combine(_directory, "other-data"));
        try
        {
            NetworkAccelerationUserLease.DefaultPath.Should().Be(first);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _directory);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await Log.Instance.ShutdownAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _previousAppDataOverride);
            Log.ResetForTests();
        }
        Directory.Delete(_directory, recursive: true);
    }
}
