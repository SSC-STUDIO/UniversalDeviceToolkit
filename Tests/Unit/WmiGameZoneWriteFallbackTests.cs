using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.System.Management;
using Xunit;

namespace UniversalDeviceToolkit.Tests;

[Trait("Category", TestCategories.Unit)]
public sealed class WmiGameZoneWriteFallbackTests
{
    [Fact]
    public async Task ClassicSuccess_DoesNotLaunchCimWrite()
    {
        var cimCalls = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneWriteWithCimFallbackAsync(
            () => Task.FromResult(WmiWriteResult.Success),
            () =>
            {
                cimCalls++;
                return Task.FromResult(WmiWriteResult.Success);
            });

        result.Status.Should().Be(WmiWriteStatus.Succeeded);
        cimCalls.Should().Be(0);
    }

    [Fact]
    public async Task ClassicUnavailable_UsesExplicitCimWriteResult()
    {
        var cimCalls = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneWriteWithCimFallbackAsync(
            () => Task.FromResult(WmiWriteResult.Unavailable),
            () =>
            {
                cimCalls++;
                return Task.FromResult(WmiWriteResult.Success);
            });

        result.Status.Should().Be(WmiWriteStatus.Succeeded);
        cimCalls.Should().Be(1);
    }

    [Fact]
    public async Task ClassicIndeterminate_DoesNotLaunchCimWrite()
    {
        var cimCalls = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneWriteWithCimFallbackAsync(
            () => Task.FromResult(WmiWriteResult.TimedOutIndeterminate),
            () =>
            {
                cimCalls++;
                return Task.FromResult(WmiWriteResult.Success);
            });

        result.Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);
        cimCalls.Should().Be(0);
    }

    [Fact]
    public async Task ClassicNotStartedBusy_DoesNotLaunchCimWrite()
    {
        var cimCalls = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneWriteWithCimFallbackAsync(
            () => Task.FromResult(WmiWriteResult.NotStartedBusy),
            () =>
            {
                cimCalls++;
                return Task.FromResult(WmiWriteResult.Success);
            });

        result.Status.Should().Be(WmiWriteStatus.NotStartedBusy);
        cimCalls.Should().Be(0);
    }

    [Fact]
    public async Task ClassicPostInvokeFailure_DoesNotLaunchCimWrite()
    {
        var cimCalls = 0;

        var result = await WMI.LenovoGameZoneData.ResolveGameZoneWriteWithCimFallbackAsync(
            () => Task.FromResult(WmiWriteResult.FailedIndeterminate),
            () =>
            {
                cimCalls++;
                return Task.FromResult(WmiWriteResult.Success);
            });

        result.Status.Should().Be(WmiWriteStatus.FailedIndeterminate);
        cimCalls.Should().Be(0);
    }

    [Fact]
    public async Task CimUnavailable_RemainsAnExplicitFailure()
    {
        var result = await WMI.LenovoGameZoneData.ResolveGameZoneWriteWithCimFallbackAsync(
            () => Task.FromResult(WmiWriteResult.Unavailable),
            () => Task.FromResult(WmiWriteResult.Unavailable));

        result.Status.Should().Be(WmiWriteStatus.Unavailable);
    }

    [Theory]
    [InlineData(true, "UDT_WRITE_OK", WmiWriteStatus.Succeeded)]
    [InlineData(true, "", WmiWriteStatus.FailedIndeterminate)]
    [InlineData(false, "UDT_WRITE_OK", WmiWriteStatus.FailedIndeterminate)]
    public void CimWriteResult_RequiresExplicitCompletionSentinel(
        bool processSucceeded,
        string output,
        WmiWriteStatus expectedStatus)
    {
        var result = WMI.ClassifyGameZoneCimWriteResult(processSucceeded, output);

        result.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task CimWriteTimeout_KillsProcessTreeAndObservesExit()
    {
        var timeoutSignal = NewSignal<object?>();
        var process = new FakeGameZoneCimProcess();

        var call = WMI.InvokeGameZoneWriteViaCimProcessAsync(
            "SetSmartFanMode",
            1,
            "Data",
            TimeSpan.FromSeconds(1),
            _ => process,
            _ => timeoutSignal.Task,
            CancellationToken.None);

        process.Started.Should().BeTrue();
        timeoutSignal.SetResult(null);

        var result = await call;

        result.Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);
        process.KillCalled.Should().BeTrue();
        process.EntireProcessTree.Should().BeTrue();
        process.WaitObserved.Should().BeTrue();
        process.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task StalledCimExit_KeepsKeyOwnedUntilProcessActuallyExits()
    {
        var outerTimeout = NewSignal<object?>();
        var queuedTimeout = NewSignal<object?>();
        var neverTimeout = NewSignal<object?>();
        var coordinatorTimeoutCount = 0;
        var coordinator = new WmiWriteCoordinator(
            _ => Interlocked.Increment(ref coordinatorTimeoutCount) switch
            {
                1 => outerTimeout.Task,
                2 => queuedTimeout.Task,
                _ => neverTimeout.Task
            });
        var processTimeout = NewSignal<object?>();
        var cleanupThreshold = NewSignal<object?>();
        var processTimeoutCount = 0;
        var process = new FakeGameZoneCimProcess(exitOnKill: false);

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetSmartFanMode",
            () => WMI.InvokeGameZoneWriteViaCimProcessAsync(
                "SetSmartFanMode",
                1,
                "Data",
                TimeSpan.FromSeconds(1),
                _ => process,
                _ => Interlocked.Increment(ref processTimeoutCount) == 1
                    ? processTimeout.Task
                    : cleanupThreshold.Task,
                CancellationToken.None),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        processTimeout.SetResult(null);
        await process.KillObserved;
        cleanupThreshold.SetResult(null);
        outerTimeout.SetResult(null);
        (await firstCall).Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);
        process.KillCalled.Should().BeTrue();
        process.Disposed.Should().BeFalse();

        var secondStarted = false;
        var secondCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetSmartFanMode",
            () =>
            {
                secondStarted = true;
                return Task.FromResult(WmiWriteResult.Success);
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        queuedTimeout.SetResult(null);

        (await secondCall).Status.Should().Be(WmiWriteStatus.NotStartedBusy);
        secondStarted.Should().BeFalse();
        coordinator.ActiveKeyCount.Should().Be(1);

        process.CompleteExit();
        var flushCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetSmartFanMode",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        (await flushCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        process.Disposed.Should().BeTrue();
        secondStarted.Should().BeFalse();
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task CallerCancellationBeforeLaunch_RemainsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var processCreated = false;

        Func<Task> action = async () =>
        {
            _ = await WMI.InvokeGameZoneWriteViaCimProcessAsync(
                "SetSmartFanMode",
                1,
                "Data",
                TimeSpan.FromSeconds(1),
                _ =>
                {
                    processCreated = true;
                    return new FakeGameZoneCimProcess();
                },
                _ => Task.Delay(Timeout.InfiniteTimeSpan),
                cancellation.Token);
        };

        await action.Should().ThrowAsync<OperationCanceledException>();
        processCreated.Should().BeFalse();
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FakeGameZoneCimProcess : IGameZoneCimProcess
    {
        private readonly TaskCompletionSource<object?> _exit = NewSignal<object?>();
        private readonly TaskCompletionSource<object?> _killObserved = NewSignal<object?>();
        private readonly bool _exitOnKill;

        public FakeGameZoneCimProcess(bool exitOnKill = true) => _exitOnKill = exitOnKill;

        public bool Started { get; private set; }
        public bool KillCalled { get; private set; }
        public bool EntireProcessTree { get; private set; }
        public bool WaitObserved { get; private set; }
        public bool Disposed { get; private set; }
        public bool HasExited { get; private set; }
        public int ExitCode => HasExited ? -1 : throw new InvalidOperationException();
        public Task KillObserved => _killObserved.Task;

        public void Start() => Started = true;

        public void Kill(bool entireProcessTree)
        {
            KillCalled = true;
            EntireProcessTree = entireProcessTree;
            _killObserved.TrySetResult(null);
            if (_exitOnKill)
                CompleteExit();
        }

        public void CompleteExit()
        {
            HasExited = true;
            _exit.TrySetResult(null);
        }

        public async Task WaitForExitAsync()
        {
            WaitObserved = true;
            await _exit.Task;
        }

        public Task<string> ReadStandardOutputToEndAsync() => Task.FromResult(string.Empty);
        public Task<string> ReadStandardErrorToEndAsync() => Task.FromResult(string.Empty);
        public void Dispose() => Disposed = true;
    }
}
