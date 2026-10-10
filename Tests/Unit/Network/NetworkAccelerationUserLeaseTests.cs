using System.Diagnostics;
using System.Text;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Network;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public sealed class NetworkAccelerationUserLeaseTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "udt-user-lease-" + Guid.NewGuid().ToString("N"));

    public NetworkAccelerationUserLeaseTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Lease_AcrossAwait_AllowsOwnedRecoveryButRejectsAnotherHolder()
    {
        var path = Path.Combine(_directory, "user.lease");
        using (var lease = NetworkAccelerationUserLease.Acquire(path))
        {
            using (lease.EnterScope())
            {
                await Task.Yield();
                using var operation = NetworkAccelerationUserLease.AcquireOperation(path);
                Action competing = () => NetworkAccelerationUserLease.Acquire(path);
                competing.Should().Throw<IOException>();
            }
        }
        using var replacement = NetworkAccelerationUserLease.Acquire(path);
    }

    [Fact]
    public async Task Lease_SharedByDifferentDataDirectories_RejectsCaptureAndRestoreFromAnotherContext()
    {
        var leasePath = Path.Combine(_directory, "user.lease");
        var firstDirectory = Path.Combine(_directory, "first-data");
        var secondDirectory = Path.Combine(_directory, "second-data");
        var firstProxy = new SystemProxySnapshot { Server = "original:8080" };
        var secondProxy = new SystemProxySnapshot { Server = "other:8080" };
        var first = new NetworkStateRecoveryService(firstDirectory, () => string.Empty, _ => { },
            () => firstProxy, value => firstProxy = value ?? throw new InvalidOperationException(), leasePath);
        var second = new NetworkStateRecoveryService(secondDirectory, () => string.Empty, _ => { },
            () => secondProxy, value => secondProxy = value ?? throw new InvalidOperationException(), leasePath);
        using (var lease = NetworkAccelerationUserLease.Acquire(leasePath))
        using (lease.EnterScope())
        {
            await first.CaptureSnapshotAsync();
            Task competingCapture;
            Task<bool> competingRestore;
            using (ExecutionContext.SuppressFlow())
            {
                competingCapture = Task.Run(async () => await second.CaptureSnapshotAsync());
                competingRestore = Task.Run(() => second.TryRestoreFromSnapshot(out _));
            }
            Func<Task> capture = () => competingCapture;
            await capture.Should().ThrowAsync<IOException>();
            (await competingRestore).Should().BeFalse();
            File.Exists(second.SnapshotPath).Should().BeFalse();
            first.TryRestoreFromSnapshot(out _).Should().BeTrue();
        }

        await second.CaptureSnapshotAsync();
        second.TryRestoreFromSnapshot(out _).Should().BeTrue();
        firstProxy.Server.Should().Be("original:8080");
        secondProxy.Server.Should().Be("other:8080");
    }

    [Fact]
    public async Task Lease_WhenOwningProcessCrashes_ReleasesItsFileHandle()
    {
        var path = Path.Combine(_directory, "user.lease");
        var script = "$stream = [System.IO.File]::Open('" + path.Replace("'", "''", StringComparison.Ordinal) +
                     "', [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None); " +
                     "[Console]::WriteLine('held'); [Console]::ReadLine() | Out-Null; $stream.Dispose()";
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        using var owner = new Process { StartInfo = startInfo };
        owner.Start().Should().BeTrue();
        try
        {
            (await owner.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))).Should().Be("held");
            Action competing = () => NetworkAccelerationUserLease.Acquire(path);
            competing.Should().Throw<IOException>();
            owner.Kill(entireProcessTree: true);
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            owner.HasExited.Should().BeTrue();
            using var replacement = await AcquireAfterProcessExitAsync(path);
        }
        finally
        {
            if (!owner.HasExited)
            {
                owner.Kill(entireProcessTree: true);
                await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static async Task<NetworkAccelerationUserLease> AcquireAfterProcessExitAsync(string path)
    {
        // Windows may signal process exit before its file handles finish closing.
        // Keep production acquisition exclusive; wait only for sharing/lock errors
        // in this crash fixture, and fail if cleanup exceeds a bounded deadline.
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try { return NetworkAccelerationUserLease.Acquire(path); }
            catch (IOException error) when (elapsed.Elapsed < TimeSpan.FromSeconds(5) &&
                error.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021))
            {
                await Task.Delay(20);
            }
        }
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
