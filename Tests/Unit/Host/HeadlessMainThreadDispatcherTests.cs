using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class HeadlessMainThreadDispatcherTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ContainerDisposal_StopsTheNativeDispatcherThread()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<HeadlessMainThreadDispatcher>().As<IMainThreadDispatcher>().SingleInstance();
        using var container = builder.Build();
        var dispatcher = (HeadlessMainThreadDispatcher)container.Resolve<IMainThreadDispatcher>();
        var callerThread = Environment.CurrentManagedThreadId;
        var callbackThread = 0;
        dispatcher.IsPumpThreadAlive.Should().BeTrue();

        await dispatcher.DispatchAsync(() =>
        {
            callbackThread = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        }).WaitAsync(OperationTimeout);
        callbackThread.Should().NotBe(0).And.NotBe(callerThread);

        container.Dispose();

        dispatcher.IsPumpThreadAlive.Should().BeFalse();
    }

    [Fact]
    public void Disposal_RejectsNewSyncAndAsyncWork()
    {
        using var dispatcher = new HeadlessMainThreadDispatcher();
        var executed = 0;
        dispatcher.Dispose();
        Action postSync = () => dispatcher.Dispatch(() => Interlocked.Increment(ref executed));
        Action postAsync = () => _ = dispatcher.DispatchAsync(() =>
        {
            Interlocked.Increment(ref executed);
            return Task.CompletedTask;
        });

        postSync.Should().Throw<ObjectDisposedException>();
        postAsync.Should().Throw<ObjectDisposedException>();
        executed.Should().Be(0);
        dispatcher.IsPumpThreadAlive.Should().BeFalse();
    }

    [Fact]
    public async Task Disposal_FailsQueuedAsyncWorkWithoutRunningIt()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var dispatcher = new HeadlessMainThreadDispatcher();
        dispatcher.IsPumpThreadAlive.Should().BeTrue();
        dispatcher.Dispatch(() =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Test dispatcher callback was not released.");
        });
        var executed = 0;
        Task disposal = Task.CompletedTask;
        try
        {
            entered.Wait(OperationTimeout).Should().BeTrue();
            var queued = dispatcher.DispatchAsync(() =>
            {
                Interlocked.Increment(ref executed);
                return Task.CompletedTask;
            });
            disposal = Task.Run(dispatcher.Dispose);
            var waitForQueued = () => queued.WaitAsync(OperationTimeout);

            await waitForQueued.Should().ThrowAsync<ObjectDisposedException>();
            executed.Should().Be(0);
        }
        finally
        {
            release.Set();
            await disposal.WaitAsync(OperationTimeout);
        }

        dispatcher.IsPumpThreadAlive.Should().BeFalse();
    }

    [Fact]
    public async Task Disposal_FailsTheAwaiterOfAnUnfinishedAsyncCallback()
    {
        using var dispatcher = new HeadlessMainThreadDispatcher();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatched = dispatcher.DispatchAsync(() =>
        {
            started.TrySetResult();
            return callbackCompletion.Task;
        });
        try
        {
            await started.Task.WaitAsync(OperationTimeout);

            dispatcher.Dispose();

            var waitForDispatched = () => dispatched.WaitAsync(OperationTimeout);
            await waitForDispatched.Should().ThrowAsync<ObjectDisposedException>();
            dispatcher.IsPumpThreadAlive.Should().BeFalse();
        }
        finally
        {
            callbackCompletion.TrySetResult();
        }
    }

    [Fact]
    public async Task Disposal_FromThePumpThread_DoesNotWaitForItself()
    {
        using var dispatcher = new HeadlessMainThreadDispatcher();
        dispatcher.IsPumpThreadAlive.Should().BeTrue();
        var disposedOnPump = dispatcher.DispatchAsync(() =>
        {
            dispatcher.Dispose();
            return Task.CompletedTask;
        });
        var waitForDisposal = () => disposedOnPump.WaitAsync(OperationTimeout);

        await waitForDisposal.Should().ThrowAsync<ObjectDisposedException>();
        SpinWait.SpinUntil(() => !dispatcher.IsPumpThreadAlive, OperationTimeout).Should().BeTrue();
    }
}
