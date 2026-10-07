using System.IO;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class NetworkStateRecoveryServiceTests
{
    [Fact]
    public void TryRestoreFromSnapshot_WhenMissing_IsIdempotentSuccess()
    {
        using var fixture = RecoveryFixture.Create();
        var ok = fixture.Service.TryRestoreFromSnapshot(out var report);
        ok.Should().BeTrue();
        report.Should().Contain("idempotent");
        report.Should().Contain("Result: OK");
    }

    [Fact]
    public void TryRestoreFromSnapshot_WhenEmptyFile_IsIdempotentSuccess()
    {
        using var fixture = RecoveryFixture.Create();
        File.WriteAllText(fixture.Service.SnapshotPath, "   ");

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeTrue();
        report.Should().Contain("empty");
    }

    [Fact]
    public void TryRestoreFromSnapshot_WhenCorruptJson_DoesNotConsume()
    {
        using var fixture = RecoveryFixture.Create();
        File.WriteAllText(fixture.Service.SnapshotPath, "{ not-json");

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("failed to load");
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public void HardwareStateRecovery_ResetNetwork_WithEmptySnapshot_Succeeds()
    {
        var service = new HardwareStateRecoveryService(new HardwareStateRecoveryImplementation(
            _ => null,
            _ => { }));

        var ok = service.TryResetNetwork(out var report);
        ok.Should().BeTrue();
        report.Should().Contain("Network state");
    }

    [Fact]
    public async Task CaptureAndRestore_SystemProxy_RoundTripsAndConsumesSnapshot()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "proxy.example:8080",
            Override = "localhost",
            AutoConfigUrl = null
        });

        var snapshot = await fixture.Service.CaptureSnapshotAsync();
        snapshot.SchemaVersion.Should().Be(NetworkAccelerationDefaults.SnapshotSchemaVersion);
        snapshot.Phase.Should().Be(NetworkSnapshotPhase.Pending);
        snapshot.OwnerProcess.Should().NotBeNull();
        snapshot.OwnerProcess?.ProcessId.Should().Be(Environment.ProcessId);
        snapshot.SystemProxy.Should().NotBeNull();
        snapshot.SystemProxy!.Server.Should().Be("proxy.example:8080");

        fixture.Proxy = new SystemProxySnapshot
        {
            Enabled = true,
            Server = "127.0.0.1:34123",
            Override = "localhost",
            AutoConfigUrl = null
        };
        fixture.Hosts = HostsMarkedBlock.Upsert(fixture.Hosts, ["127.0.0.1 steamcommunity.com"]);

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeTrue();
        report.Should().Contain("Result: OK");
        report.Should().Contain("consumed");
        fixture.Proxy!.Server.Should().Be("proxy.example:8080");
        fixture.Proxy.Enabled.Should().BeTrue();
        fixture.Hosts.Should().NotContain(HostsMarkedBlock.BeginMarker);
        fixture.Hosts.Should().Contain("127.0.0.1 localhost");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryRestoreFromSnapshot_WhenProxyWriteFails_DoesNotConsume()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "proxy.example:8080"
        });
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Proxy = UdtLoopbackProxy();
        fixture.ProxyWrite = _ => throw new IOException("registry locked");

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("PARTIAL");
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task TryRestoreFromSnapshot_WhenCurrentIsNotUdtOwned_SkipsProxyAndConsumes()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "proxy.example:8080"
        });
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Proxy = new SystemProxySnapshot
        {
            Enabled = true,
            Server = "127.0.0.1:7890"
        };

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeTrue();
        report.Should().Contain("not UDT-owned");
        fixture.Proxy!.Server.Should().Be("127.0.0.1:7890");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryRestoreFromSnapshot_WhenUdtPacUrl_RestoresOriginalProxy()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "corporate-proxy:8080"
        });
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Proxy = new SystemProxySnapshot
        {
            Enabled = false,
            Server = string.Empty,
            AutoConfigUrl = "file:///C:/Users/test/AppData/udt-network-acceleration.pac"
        };

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeTrue();
        report.Should().Contain("restored from snapshot");
        fixture.Proxy!.Server.Should().Be("corporate-proxy:8080");
        fixture.Proxy.Enabled.Should().BeTrue();
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryRestoreFromSnapshot_WhenPhaseRestored_DoesNotReapply()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "proxy.example:8080"
        });
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Service.TryMarkPhase(NetworkSnapshotPhase.Restored, out _).Should().BeTrue();
        fixture.Proxy = UdtLoopbackProxy();

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeTrue();
        report.Should().Contain("already restored");
        fixture.Proxy!.Server.Should().Be("127.0.0.1:34123");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryRestoreFromSnapshot_WhenUnsupportedSchema_LeavesSnapshotUntouched()
    {
        using var fixture = RecoveryFixture.Create();
        var snapshot = new NetworkStateSnapshot
        {
            SchemaVersion = 99,
            Phase = NetworkSnapshotPhase.Applied,
            SystemProxy = new SystemProxySnapshot { Enabled = true, Server = "original:1" }
        };
        await fixture.Service.SaveSnapshotAsync(snapshot);
        fixture.Proxy = UdtLoopbackProxy();

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("unsupported schema");
        fixture.Proxy!.Server.Should().Be("127.0.0.1:34123");
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task TryMarkPhase_Applied_RecordsFingerprints()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "proxy.example:8080"
        });
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Proxy = new SystemProxySnapshot
        {
            Enabled = false,
            AutoConfigUrl = "file:///C:/tmp/udt-network-acceleration.pac"
        };

        fixture.Service.TryMarkPhase(NetworkSnapshotPhase.Applied, out var report, listenPort: 34123)
            .Should().BeTrue();
        report.Should().Contain("Applied");

        var loaded = await fixture.Service.LoadSnapshotAsync();
        loaded.Should().NotBeNull();
        loaded!.Phase.Should().Be(NetworkSnapshotPhase.Applied);
        loaded.AppliedListenPort.Should().Be(34123);
        loaded.AppliedAutoConfigUrl.Should().Contain(NetworkStateRecoveryService.UdtPacFileName);
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public void IsUdtOwnedProxy_RecognizesPacAndUdtPort_NotForeignLoopback()
    {
        NetworkStateRecoveryService.IsUdtOwnedProxy(new SystemProxySnapshot
        {
            AutoConfigUrl = "file:///x/udt-network-acceleration.pac"
        }).Should().BeTrue();

        NetworkStateRecoveryService.IsUdtOwnedProxy(UdtLoopbackProxy()).Should().BeTrue();

        NetworkStateRecoveryService.IsUdtOwnedProxy(new SystemProxySnapshot
        {
            Enabled = true,
            Server = "127.0.0.1:7890"
        }).Should().BeFalse();
    }

    [Fact]
    public async Task CaptureSnapshot_LegacyUnversionedFile_IsReadableAsPending()
    {
        using var fixture = RecoveryFixture.Create();
        File.WriteAllText(fixture.Service.SnapshotPath, """
            {
              "capturedAtUtc": "2024-01-01T00:00:00+00:00",
              "systemProxy": { "enabled": true, "server": "legacy-proxy:8080" }
            }
            """);

        var loaded = await fixture.Service.LoadSnapshotAsync();
        loaded.Should().NotBeNull();
        loaded!.SchemaVersion.Should().Be(0);
        loaded.Phase.Should().Be(NetworkSnapshotPhase.Pending);
        NetworkStateRecoveryService.IsSupportedSchemaVersion(loaded.SchemaVersion).Should().BeTrue();
    }

    [Theory]
    [InlineData((int)NetworkProcessState.Alive)]
    [InlineData((int)NetworkProcessState.Unknown)]
    public async Task SnapshotMutations_WhenOwnerIsAliveOrUnknown_LeaveStateUntouched(int ownerState)
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot { Server = "original:8080" });
        var snapshot = await fixture.Service.CaptureSnapshotAsync();
        var originalSnapshot = File.ReadAllText(fixture.Service.SnapshotPath);
        fixture.Proxy = UdtLoopbackProxy();
        fixture.Hosts = HostsMarkedBlock.Upsert(fixture.Hosts, ["127.0.0.1 example.com"]);
        var originalHosts = fixture.Hosts;
        fixture.ProcessState = (NetworkProcessState)ownerState;

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("owner is active or cannot be verified");
        fixture.Service.TryMarkPhase(NetworkSnapshotPhase.Restored, out _).Should().BeFalse();
        fixture.Service.TryConsumeSnapshot(out _).Should().BeFalse();
        Func<Task> save = () => fixture.Service.SaveSnapshotAsync(snapshot);
        await save.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> capture = () => fixture.Service.CaptureSnapshotAsync();
        await capture.Should().ThrowAsync<InvalidOperationException>();

        File.ReadAllText(fixture.Service.SnapshotPath).Should().Be(originalSnapshot);
        fixture.Proxy?.Server.Should().Be("127.0.0.1:34123");
        fixture.Hosts.Should().Be(originalHosts);
        fixture.StoppedWorkers.Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_AfterHostRestart_StopsOnlyRecordedWorkerBeforeRestoring()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot { Server = "original:8080" });
        var snapshot = await fixture.Service.CaptureSnapshotAsync();
        var worker = new NetworkProcessIdentity
        {
            ProcessId = 12345,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ExecutablePath = @"C:\UDT\UniversalDeviceToolkit.NetworkProxy.exe"
        };
        snapshot.WorkerProcess = worker;
        await fixture.Service.SaveSnapshotAsync(snapshot);
        fixture.Proxy = UdtLoopbackProxy();
        fixture.ProcessState = NetworkProcessState.Exited;
        fixture.ProxyWrite = value =>
        {
            fixture.StoppedWorkers.Should().ContainSingle();
            fixture.Proxy = value;
        };

        fixture.Service.TryRestoreFromSnapshot(out _).Should().BeTrue();
        fixture.StoppedWorkers.Should().ContainSingle().Which.ProcessId.Should().Be(worker.ProcessId);
        fixture.Proxy?.Server.Should().Be("original:8080");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task Restore_WhenRecordedWorkerCannotBeVerifiedOrStopped_LeavesSnapshotForRetry()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot { Server = "original:8080" });
        var snapshot = await fixture.Service.CaptureSnapshotAsync();
        snapshot.WorkerProcess = new NetworkProcessIdentity { ProcessId = 12345, StartedAtUtc = DateTimeOffset.UtcNow };
        await fixture.Service.SaveSnapshotAsync(snapshot);
        fixture.ProcessState = NetworkProcessState.Exited;
        fixture.StopWorkerSucceeds = false;
        fixture.Proxy = UdtLoopbackProxy();

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("orphaned worker could not be verified or stopped");
        fixture.Proxy?.Server.Should().Be("127.0.0.1:34123");
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();

        fixture.StopWorkerSucceeds = true;
        fixture.Service.TryRestoreFromSnapshot(out _).Should().BeTrue();
        fixture.Proxy?.Server.Should().Be("original:8080");
    }

    [Fact]
    public async Task Restore_AfterHostRestartWhileAnotherWorkerIsActive_PreservesOtherHostsProxyAndSnapshot()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot { Server = "original:8080" });
        var snapshot = await fixture.Service.CaptureSnapshotAsync();
        snapshot.WorkerProcess = new NetworkProcessIdentity
        {
            ProcessId = 12345,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ExecutablePath = @"C:\UDT\UniversalDeviceToolkit.NetworkProxy.exe"
        };
        await fixture.Service.SaveSnapshotAsync(snapshot);
        fixture.ProcessState = NetworkProcessState.Exited;
        fixture.HasUnidentifiedWorker = true;
        fixture.Proxy = new SystemProxySnapshot
        {
            AutoConfigUrl = "file:///C:/other-data/network/udt-network-acceleration.pac"
        };
        var originalSnapshot = File.ReadAllText(fixture.Service.SnapshotPath);

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("another NetworkProxy worker is active");
        fixture.Service.TryConsumeSnapshot(out _).Should().BeFalse();
        fixture.Proxy?.AutoConfigUrl.Should().Be("file:///C:/other-data/network/udt-network-acceleration.pac");
        File.ReadAllText(fixture.Service.SnapshotPath).Should().Be(originalSnapshot);
        fixture.StoppedWorkers.Should().ContainSingle().Which.ProcessId.Should().Be(12345);

        fixture.HasUnidentifiedWorker = false;
        fixture.Service.TryRestoreFromSnapshot(out _).Should().BeTrue();
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public void IsUdtOwnedProxy_WithRecordedPacFingerprint_DoesNotAdoptAnotherInstallationsPac()
    {
        var snapshot = new NetworkStateSnapshot
        {
            AppliedAutoConfigUrl = "file:///C:/first-data/network/udt-network-acceleration.pac"
        };
        var current = new SystemProxySnapshot
        {
            AutoConfigUrl = "file:///C:/other-data/network/udt-network-acceleration.pac"
        };

        NetworkStateRecoveryService.IsUdtOwnedProxy(current, snapshot).Should().BeFalse();
        current.AutoConfigUrl = snapshot.AppliedAutoConfigUrl;
        NetworkStateRecoveryService.IsUdtOwnedProxy(current, snapshot).Should().BeTrue();
    }

    [Fact]
    public async Task Restore_LegacySnapshotWithUnidentifiedWorker_DoesNotMutateOrKill()
    {
        using var fixture = RecoveryFixture.Create();
        await fixture.Service.SaveSnapshotAsync(new NetworkStateSnapshot
        {
            SchemaVersion = NetworkAccelerationDefaults.SnapshotSchemaVersion,
            SystemProxy = new SystemProxySnapshot { Server = "original:8080" }
        });
        fixture.HasUnidentifiedWorker = true;
        fixture.Proxy = UdtLoopbackProxy();

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("legacy owner is unknown");
        fixture.Proxy?.Server.Should().Be("127.0.0.1:34123");
        fixture.StoppedWorkers.Should().BeEmpty();
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task Restore_WhenOwnerExitedBeforeWorkerIdentityWasSaved_DoesNotGuessWorker()
    {
        using var fixture = RecoveryFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        fixture.ProcessState = NetworkProcessState.Exited;
        fixture.HasUnidentifiedWorker = true;
        fixture.Proxy = UdtLoopbackProxy();

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("worker identity is missing");
        fixture.StoppedWorkers.Should().BeEmpty();
        fixture.Proxy?.Server.Should().Be("127.0.0.1:34123");
    }

    [Fact]
    public async Task SnapshotLock_WhenHeldByAnotherOperation_RefusesRestoreAndCapture()
    {
        using var fixture = RecoveryFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Proxy = UdtLoopbackProxy();
        using var heldLock = new FileStream(fixture.Service.SnapshotPath + ".lock", FileMode.Open,
            FileAccess.ReadWrite, FileShare.None);

        fixture.Service.TryRestoreFromSnapshot(out var report).Should().BeFalse();
        report.Should().Contain("recovery refused");
        Func<Task> capture = () => fixture.Service.CaptureSnapshotAsync();
        await capture.Should().ThrowAsync<IOException>();
        fixture.Proxy?.Server.Should().Be("127.0.0.1:34123");
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task Capture_WhenExistingSnapshotWasNotRestored_DoesNotReplaceOriginalBaseline()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot { Server = "original:8080" });
        await fixture.Service.CaptureSnapshotAsync();
        fixture.Proxy = UdtLoopbackProxy();

        Func<Task> capture = () => fixture.Service.CaptureSnapshotAsync();
        await capture.Should().ThrowAsync<InvalidOperationException>();
        var snapshot = await fixture.Service.LoadSnapshotAsync();
        snapshot?.SystemProxy?.Server.Should().Be("original:8080");
    }

    [Fact]
    public async Task Capture_WithoutSnapshotWhileUnidentifiedWorkerIsActive_DoesNotCaptureSystemState()
    {
        using var fixture = RecoveryFixture.Create(new SystemProxySnapshot { Server = "other-host:34123" });
        fixture.HasUnidentifiedWorker = true;

        Func<Task> capture = () => fixture.Service.CaptureSnapshotAsync();
        await capture.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unidentified*");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
        fixture.Proxy?.Server.Should().Be("other-host:34123");
        fixture.StoppedWorkers.Should().BeEmpty();
    }

    private static SystemProxySnapshot UdtLoopbackProxy() => new()
    {
        Enabled = true,
        Server = "127.0.0.1:34123",
        Override = "localhost"
    };

    private sealed class RecoveryFixture : IDisposable
    {
        private RecoveryFixture(string directory)
        {
            Directory = directory;
            Hosts = "127.0.0.1 localhost\n# other\n";
            Service = new NetworkStateRecoveryService(
                directory,
                () => Hosts,
                content => Hosts = content,
                () => Proxy,
                value =>
                {
                    if (ProxyWrite is not null)
                    {
                        ProxyWrite(value);
                        return;
                    }

                    Proxy = value;
                },
                _ => ProcessState,
                () => HasUnidentifiedWorker,
                identity =>
                {
                    StoppedWorkers.Add(identity);
                    return StopWorkerSucceeds;
                });
        }

        public string Directory { get; }

        public string Hosts { get; set; }

        public SystemProxySnapshot? Proxy { get; set; }

        public Action<SystemProxySnapshot?>? ProxyWrite { get; set; }

        public NetworkProcessState ProcessState { get; set; } = NetworkProcessState.Current;
        public bool HasUnidentifiedWorker { get; set; }
        public bool StopWorkerSucceeds { get; set; } = true;
        public List<NetworkProcessIdentity> StoppedWorkers { get; } = [];

        public NetworkStateRecoveryService Service { get; }

        public static RecoveryFixture Create(SystemProxySnapshot? proxy = null)
        {
            var dir = Path.Combine(Path.GetTempPath(), "udt-net-recovery-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return new RecoveryFixture(dir) { Proxy = proxy };
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch { /* ignore */ }
        }
    }
}
