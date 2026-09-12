using System.IO;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class NetworkAccelerationMutationRollbackTests
{
    [Fact]
    public async Task TryApplySystemMutationOrRollback_WhenApplyThrows_RestoresOriginalAndConsumes()
    {
        using var fixture = MutationFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        var applied = false;

        var ok = NetworkAccelerationService.TryApplySystemMutationOrRollback(
            fixture.Service,
            () =>
            {
                fixture.Proxy = UdtPacProxy();
                throw new IOException("WinINET apply failed");
            },
            NetworkAccelerationDefaults.DefaultListenPort,
            ref applied,
            out var report);

        ok.Should().BeFalse();
        applied.Should().BeFalse();
        report.Should().Contain("IOException");
        fixture.Proxy!.Server.Should().Be("proxy.example:8080");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryApplySystemMutationOrRollback_WhenRestoreFails_KeepsAppliedFlagAndSnapshot()
    {
        using var fixture = MutationFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        fixture.ProxyWrite = _ => throw new IOException("restore failed");
        var applied = false;

        var ok = NetworkAccelerationService.TryApplySystemMutationOrRollback(
            fixture.Service,
            () =>
            {
                fixture.Proxy = UdtPacProxy();
                throw new InvalidOperationException("partial apply");
            },
            NetworkAccelerationDefaults.DefaultListenPort,
            ref applied,
            out var report);

        ok.Should().BeFalse();
        applied.Should().BeTrue();
        report.Should().Contain("restore failed");
        fixture.Proxy!.AutoConfigUrl.Should().Contain(NetworkStateRecoveryService.UdtPacFileName);
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task TryApplySystemMutationOrRollback_WhenApplySucceeds_MarksAppliedAndKeepsSnapshot()
    {
        using var fixture = MutationFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        var applied = false;

        var ok = NetworkAccelerationService.TryApplySystemMutationOrRollback(
            fixture.Service,
            () =>
            {
                fixture.Proxy = UdtPacProxy();
                return true;
            },
            34123,
            ref applied,
            out var report);

        ok.Should().BeTrue();
        applied.Should().BeTrue();
        report.Should().Contain("Applied");
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();

        var loaded = await fixture.Service.LoadSnapshotAsync();
        loaded.Should().NotBeNull();
        loaded!.Phase.Should().Be(NetworkSnapshotPhase.Applied);
        loaded.AppliedListenPort.Should().Be(34123);
        loaded.AppliedAutoConfigUrl.Should().Contain(NetworkStateRecoveryService.UdtPacFileName);
    }

    [Fact]
    public async Task TryApplySystemMutationOrRollback_WhenNoMutation_ConsumesPendingSnapshot()
    {
        using var fixture = MutationFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        var applied = true;

        var ok = NetworkAccelerationService.TryApplySystemMutationOrRollback(
            fixture.Service,
            () => false,
            NetworkAccelerationDefaults.DefaultListenPort,
            ref applied,
            out _);

        ok.Should().BeTrue();
        applied.Should().BeFalse();
        fixture.Proxy!.Server.Should().Be("proxy.example:8080");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public async Task TryApplyThenRestore_ConsumesSnapshotOnlyAfterSuccessfulRestore()
    {
        using var fixture = MutationFixture.Create();
        await fixture.Service.CaptureSnapshotAsync();
        var applied = false;

        NetworkAccelerationService.TryApplySystemMutationOrRollback(
            fixture.Service,
            () =>
            {
                fixture.Proxy = UdtPacProxy();
                return true;
            },
            34123,
            ref applied,
            out _).Should().BeTrue();
        applied.Should().BeTrue();
        File.Exists(fixture.Service.SnapshotPath).Should().BeTrue();

        fixture.Service.TryRestoreFromSnapshot(out var restoreReport).Should().BeTrue();
        restoreReport.Should().Contain("consumed");
        applied = false;
        fixture.Proxy!.Server.Should().Be("proxy.example:8080");
        File.Exists(fixture.Service.SnapshotPath).Should().BeFalse();
    }

    private static SystemProxySnapshot UdtPacProxy() => new()
    {
        Enabled = false,
        Server = string.Empty,
        AutoConfigUrl = "file:///C:/Users/test/AppData/network/udt-network-acceleration.pac"
    };

    private sealed class MutationFixture : IDisposable
    {
        private MutationFixture(string directory)
        {
            Directory = directory;
            Hosts = "127.0.0.1 localhost\n";
            Proxy = new SystemProxySnapshot
            {
                Enabled = true,
                Server = "proxy.example:8080",
                Override = "localhost"
            };
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

        public static MutationFixture Create()
        {
            var dir = Path.Combine(Path.GetTempPath(), "udt-net-mutation-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return new MutationFixture(dir);
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(Directory, recursive: true); }
            catch { /* ignore */ }
        }
    }
}
