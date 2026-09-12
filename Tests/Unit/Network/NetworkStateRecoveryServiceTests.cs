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
                });
        }

        public string Directory { get; }

        public string Hosts { get; set; }

        public SystemProxySnapshot? Proxy { get; set; }

        public Action<SystemProxySnapshot?>? ProxyWrite { get; set; }

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
