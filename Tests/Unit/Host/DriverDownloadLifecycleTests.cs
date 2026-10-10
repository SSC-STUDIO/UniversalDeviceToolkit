using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class DriverDownloadLifecycleTests : TemporaryFileTestBase
{
    private static readonly byte[] InstallerBytes = [1, 2, 3, 4];

    [Theory]
    [InlineData("success")]
    [InlineData("cancelled")]
    [InlineData("error")]
    public async Task PausedDownload_LateOutcomeCannotOverwriteNewRun(string outcome)
    {
        var directory = CreateTempDirectory();
        var package = CreatePackage();
        var oldDownloader = new ControlledDownloader();
        var newDownloader = new ControlledDownloader();
        var progressContext = new QueuedProgressContext();
        var launches = 0;
        Process? StartProcess(ProcessStartInfo _) { launches++; return StartExitedProcess(); }
        var oldRun = StartWithProgressContext(package, directory, oldDownloader, progressContext, StartProcess);

        DriverDownloadHandlers.PausePackage(package.Id);
        oldDownloader.Token.IsCancellationRequested.Should().BeTrue();
        var newRun = StartWithProgressContext(package, directory, newDownloader, progressContext, StartProcess);

        try
        {
            oldDownloader.ReportProgress(0.9f);
            progressContext.Drain();
            DriverDownloadHandlers.GetRunState(package.Id)?.Progress.Should().Be(0);
            newDownloader.ReportProgress(0.3f);
            progressContext.Drain();

            var filePath = Path.Combine(directory, package.Title + " - " + package.FileName);
            switch (outcome)
            {
                case "success":
                    await File.WriteAllBytesAsync(filePath, InstallerBytes);
                    oldDownloader.Complete(filePath);
                    break;
                case "cancelled":
                    oldDownloader.Fail(new OperationCanceledException(oldDownloader.Token));
                    break;
                default:
                    oldDownloader.Fail(new IOException("Old download failed."));
                    break;
            }

            await oldRun.WaitAsync(TimeSpan.FromSeconds(10));
            var state = DriverDownloadHandlers.GetRunState(package.Id);
            state.Should().NotBeNull();
            state?.Status.Should().Be("Downloading");
            state?.Progress.Should().Be(0.3f);
            state?.Error.Should().BeNull();
            state?.DownloadedFilePath.Should().BeNull();
            launches.Should().Be(0);

            await File.WriteAllBytesAsync(filePath, InstallerBytes);
            newDownloader.Complete(filePath);
            await newRun.WaitAsync(TimeSpan.FromSeconds(10));
            DriverDownloadHandlers.GetRunState(package.Id)?.Status.Should().Be("Completed");
            launches.Should().Be(1);
        }
        finally
        {
            DriverDownloadHandlers.PausePackage(package.Id);
            oldDownloader.Fail(new OperationCanceledException());
            newDownloader.Fail(new OperationCanceledException());
            await Task.WhenAll(oldRun, newRun).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task InstallerAlreadyExited_IsCompletedAndReleased()
    {
        var directory = CreateTempDirectory();
        var package = CreatePackage();
        var downloader = new ControlledDownloader();
        var run = DriverDownloadHandlers.DownloadAndInstallAsync(package, directory, downloader, _ => StartExitedProcess());
        try
        {
            var filePath = Path.Combine(directory, package.Title + " - " + package.FileName);
            await File.WriteAllBytesAsync(filePath, InstallerBytes);
            downloader.Complete(filePath);
            await run.WaitAsync(TimeSpan.FromSeconds(10));

            var state = DriverDownloadHandlers.GetRunState(package.Id);
            state.Should().NotBeNull();
            state?.Status.Should().Be("Completed");
            state?.Progress.Should().Be(1);
            state?.Error.Should().BeNull();
            state?.InstallProcess.Should().BeNull();
            state?.DownloadCts.Should().BeNull();
        }
        finally
        {
            DriverDownloadHandlers.PausePackage(package.Id);
            downloader.Fail(new OperationCanceledException());
            await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task InstallerExitCallback_WaitingForStateCannotDeadlockLaunch()
    {
        var directory = CreateTempDirectory();
        var package = CreatePackage();
        var downloader = new ControlledDownloader();
        using var callbackEntered = new ManualResetEventSlim();
        Process StartProcess(ProcessStartInfo startInfo)
        {
            var process = StartExitedProcess();
            process.Exited += (_, _) =>
            {
                callbackEntered.Set();
                _ = DriverDownloadHandlers.GetRunState(package.Id);
            };
            process.EnableRaisingEvents = true;
            if (!callbackEntered.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Installer callback did not start.");
            return process;
        }
        var run = DriverDownloadHandlers.DownloadAndInstallAsync(package, directory, downloader, StartProcess);
        var filePath = Path.Combine(directory, package.Title + " - " + package.FileName);
        await File.WriteAllBytesAsync(filePath, InstallerBytes);
        downloader.Complete(filePath);

        await run.WaitAsync(TimeSpan.FromSeconds(10));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DriverDownloadHandlers.GetRunState(package.Id)?.Status == "Installing" && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        DriverDownloadHandlers.GetRunState(package.Id)?.Status.Should().Be("Completed");
    }

    private static Package CreatePackage() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = "Lifecycle Test",
        FileName = "driver.exe",
        FileCrc = Convert.ToHexString(SHA256.HashData(InstallerBytes)),
    };

    private static Task StartWithProgressContext(Package package, string directory, ControlledDownloader downloader,
        SynchronizationContext context, Func<ProcessStartInfo, Process?> starter)
    {
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            return DriverDownloadHandlers.DownloadAndInstallAsync(package, directory, downloader, starter);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static Process StartExitedProcess()
    {
        var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new IOException("Could not start test process.");
        if (process.WaitForExit(5000))
            return process;
        process.Kill(entireProcessTree: true);
        process.Dispose();
        throw new TimeoutException("Test process did not exit.");
    }

    private sealed class ControlledDownloader : IPackageDownloader
    {
        private readonly TaskCompletionSource<string> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IProgress<float>? _progress;
        internal CancellationToken Token { get; private set; }
        internal void Complete(string path) => _completion.TrySetResult(path);
        internal void Fail(Exception error) => _completion.TrySetException(error);
        internal void ReportProgress(float progress) => _progress?.Report(progress);

        public Task<string> DownloadPackageFileAsync(Package package, string location, IProgress<float>? progress = null,
            CancellationToken token = default)
        {
            Token = token;
            _progress = progress;
            return _completion.Task;
        }

        public Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null,
            CancellationToken token = default) => Task.FromResult(new List<Package>());
    }

    private sealed class QueuedProgressContext : SynchronizationContext
    {
        private readonly Queue<Action> _callbacks = new();
        public override void Post(SendOrPostCallback callback, object? state) => _callbacks.Enqueue(() => callback(state));
        internal void Drain()
        {
            while (_callbacks.TryDequeue(out var callback))
                callback();
        }
    }
}
