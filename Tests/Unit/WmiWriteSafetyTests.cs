using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.System.Management;
using Xunit;

namespace UniversalDeviceToolkit.Tests;

[Trait("Category", TestCategories.Unit)]
public sealed class WmiWriteSafetyTests
{
    [Fact]
    public async Task TimedOutInvocation_KeepsSameKeySerializedUntilItActuallyCompletes()
    {
        var firstTimeout = NewSignal<object?>();
        var neverTimeout = NewSignal<object?>();
        var timeoutRequestCount = 0;
        var coordinator = new WmiWriteCoordinator(
            _ => Interlocked.Increment(ref timeoutRequestCount) == 1
                ? firstTimeout.Task
                : neverTimeout.Task);

        var firstInvocation = NewSignal<WmiWriteResult>();
        var invocationCount = 0;
        var firstInvocationOwnerReleased = 0;

        async Task<WmiWriteResult> InvokeFirstAsync()
        {
            Interlocked.Increment(ref invocationCount);
            try
            {
                return await firstInvocation.Task;
            }
            finally
            {
                Interlocked.Exchange(ref firstInvocationOwnerReleased, 1);
            }
        }

        var firstCall = coordinator.ExecuteAsync(
            "ROOT\\WMI\u001fSELECT * FROM TEST_METHOD\u001fSetValue",
            InvokeFirstAsync,
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        firstTimeout.SetResult(null);
        var firstResult = await firstCall;

        firstResult.Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);
        invocationCount.Should().Be(1, "a mutating call must not retry after timing out");
        Volatile.Read(ref firstInvocationOwnerReleased).Should().Be(
            0,
            "the timed-out invocation still owns its WMI resources");

        var startOrder = new List<int>();
        var secondCall = coordinator.ExecuteAsync(
            "root\\wmi\u001fselect * from test_method\u001fsetvalue",
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                startOrder.Add(2);
                return Task.FromResult(WmiWriteResult.Success);
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        var thirdCall = coordinator.ExecuteAsync(
            "ROOT\\WMI\u001fSELECT * FROM TEST_METHOD\u001fSetValue",
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                startOrder.Add(3);
                return Task.FromResult(WmiWriteResult.Success);
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        startOrder.Should().BeEmpty("later same-key writes must not start while the first invocation is live");
        invocationCount.Should().Be(1);

        firstInvocation.SetResult(WmiWriteResult.Success);

        (await secondCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        (await thirdCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        startOrder.Should().Equal(2, 3);
        invocationCount.Should().Be(3);
        Volatile.Read(ref firstInvocationOwnerReleased).Should().Be(1);
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task TimedOutInvocation_DoesNotBlockDifferentKeys()
    {
        var firstTimeout = NewSignal<object?>();
        var neverTimeout = NewSignal<object?>();
        var timeoutRequestCount = 0;
        var coordinator = new WmiWriteCoordinator(
            _ => Interlocked.Increment(ref timeoutRequestCount) == 1
                ? firstTimeout.Task
                : neverTimeout.Task);
        var firstInvocation = NewSignal<WmiWriteResult>();

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetA",
            () => firstInvocation.Task,
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        firstTimeout.SetResult(null);

        (await firstCall).Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);

        var differentKeyCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetB",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        (await differentKeyCall).Status.Should().Be(WmiWriteStatus.Succeeded);

        firstInvocation.SetResult(WmiWriteResult.Success);
        var sameKeyFlush = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetA",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        (await sameKeyFlush).Status.Should().Be(WmiWriteStatus.Succeeded);
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task FaultedInvocation_ReleasesKeyForNextWrite()
    {
        var neverTimeout = NewSignal<object?>();
        var coordinator = new WmiWriteCoordinator(_ => neverTimeout.Task);
        var expected = new InvalidOperationException("test failure");

        Func<Task> firstCall = async () =>
        {
            _ = await coordinator.ExecuteAsync(
                "scope\u001fquery\u001fSetValue",
                () => Task.FromException<WmiWriteResult>(expected),
                TimeSpan.FromSeconds(1),
                WmiWriteResult.TimedOutIndeterminate,
                WmiWriteResult.NotStartedBusy);
        };

        (await firstCall.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(expected);

        var nextResult = await coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        nextResult.Status.Should().Be(WmiWriteStatus.Succeeded);
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task TimedOutInvocation_LateFailureIsObservedAndReleasesKey()
    {
        var firstTimeout = NewSignal<object?>();
        var neverTimeout = NewSignal<object?>();
        var timeoutRequestCount = 0;
        var coordinator = new WmiWriteCoordinator(
            _ => Interlocked.Increment(ref timeoutRequestCount) == 1
                ? firstTimeout.Task
                : neverTimeout.Task);
        var firstInvocation = NewSignal<WmiWriteResult>();
        var observedFailure = NewSignal<Exception>();
        var expected = new InvalidOperationException("late test failure");

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => firstInvocation.Task,
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy,
            ex => observedFailure.TrySetResult(ex));
        firstTimeout.SetResult(null);

        (await firstCall).Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);

        var nextCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        firstInvocation.SetException(expected);

        (await observedFailure.Task).Should().BeSameAs(expected);
        (await nextCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task QueuedCaller_DeadlineExpiresWithoutEverStarting()
    {
        var timeoutSignals = new List<TaskCompletionSource<object?>>();
        var coordinator = new WmiWriteCoordinator(_ =>
        {
            var signal = NewSignal<object?>();
            timeoutSignals.Add(signal);
            return signal.Task;
        });
        var firstInvocation = NewSignal<WmiWriteResult>();
        var queuedStarted = 0;

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => firstInvocation.Task,
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        timeoutSignals[0].SetResult(null);
        (await firstCall).Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);

        var queuedCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () =>
            {
                Interlocked.Exchange(ref queuedStarted, 1);
                return Task.FromResult(WmiWriteResult.Success);
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        coordinator.PendingOperationCount.Should().Be(1);
        timeoutSignals[1].SetResult(null);

        (await queuedCall).Status.Should().Be(WmiWriteStatus.NotStartedBusy);
        Volatile.Read(ref queuedStarted).Should().Be(0);
        coordinator.PendingOperationCount.Should().Be(0);

        firstInvocation.SetResult(WmiWriteResult.Success);
        var flushCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        (await flushCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        Volatile.Read(ref queuedStarted).Should().Be(0, "an expired queued write must never run later");
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task HungPredecessor_ExpiredQueuedCallersAreRemoved()
    {
        var timeoutSignals = new List<TaskCompletionSource<object?>>();
        var coordinator = new WmiWriteCoordinator(_ =>
        {
            var signal = NewSignal<object?>();
            timeoutSignals.Add(signal);
            return signal.Task;
        });
        var firstInvocation = NewSignal<WmiWriteResult>();
        var queuedInvocationCount = 0;

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => firstInvocation.Task,
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        timeoutSignals[0].SetResult(null);
        (await firstCall).Status.Should().Be(WmiWriteStatus.TimedOutIndeterminate);

        var queuedCalls = new Task<WmiWriteResult>[WmiWriteCoordinator.MaxPendingOperationsPerKey];
        for (var index = 0; index < queuedCalls.Length; index++)
        {
            queuedCalls[index] = coordinator.ExecuteAsync(
                "scope\u001fquery\u001fSetValue",
                () =>
                {
                    Interlocked.Increment(ref queuedInvocationCount);
                    return Task.FromResult(WmiWriteResult.Success);
                },
                TimeSpan.FromSeconds(1),
                WmiWriteResult.TimedOutIndeterminate,
                WmiWriteResult.NotStartedBusy);
        }

        coordinator.PendingOperationCount.Should().Be(queuedCalls.Length);
        for (var index = 1; index < timeoutSignals.Count; index++)
            timeoutSignals[index].SetResult(null);

        var results = await Task.WhenAll(queuedCalls);

        results.Should().OnlyContain(result => result.Status == WmiWriteStatus.NotStartedBusy);
        Volatile.Read(ref queuedInvocationCount).Should().Be(0);
        coordinator.PendingOperationCount.Should().Be(0);
        coordinator.ActiveKeyCount.Should().Be(1, "the still-live predecessor must retain its key");

        firstInvocation.SetResult(WmiWriteResult.Success);
        var flushCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => Task.FromResult(WmiWriteResult.Success),
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        await flushCall;

        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task SameKeySequence_HoldsSerializationThroughFallback()
    {
        var neverTimeout = NewSignal<object?>();
        var coordinator = new WmiWriteCoordinator(_ => neverTimeout.Task);
        var cimStarted = NewSignal<object?>();
        var releaseCim = NewSignal<object?>();
        var order = new List<string>();

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            async () =>
            {
                order.Add("classic-1");
                cimStarted.TrySetResult(null);
                await releaseCim.Task;
                order.Add("cim-1");
                return WmiWriteResult.Success;
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);
        await cimStarted.Task;

        var secondCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () =>
            {
                order.Add("classic-2");
                return Task.FromResult(WmiWriteResult.Success);
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        order.Should().Equal("classic-1");
        releaseCim.SetResult(null);

        (await firstCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        (await secondCall).Status.Should().Be(WmiWriteStatus.Succeeded);
        order.Should().Equal("classic-1", "cim-1", "classic-2");
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task PendingCap_RejectsExcessWithoutLaterExecution()
    {
        var neverTimeout = NewSignal<object?>();
        var coordinator = new WmiWriteCoordinator(_ => neverTimeout.Task);
        var firstInvocation = NewSignal<WmiWriteResult>();
        var startOrder = new List<int>();

        var firstCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () => firstInvocation.Task,
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        var pendingCalls = new Task<WmiWriteResult>[WmiWriteCoordinator.MaxPendingOperationsPerKey];
        for (var index = 0; index < pendingCalls.Length; index++)
        {
            var capturedIndex = index;
            pendingCalls[index] = coordinator.ExecuteAsync(
                "scope\u001fquery\u001fSetValue",
                () =>
                {
                    startOrder.Add(capturedIndex);
                    return Task.FromResult(WmiWriteResult.Success);
                },
                TimeSpan.FromSeconds(1),
                WmiWriteResult.TimedOutIndeterminate,
                WmiWriteResult.NotStartedBusy);
        }

        var excessStarted = false;
        var excessCall = coordinator.ExecuteAsync(
            "scope\u001fquery\u001fSetValue",
            () =>
            {
                excessStarted = true;
                return Task.FromResult(WmiWriteResult.Success);
            },
            TimeSpan.FromSeconds(1),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy);

        (await excessCall).Status.Should().Be(WmiWriteStatus.NotStartedBusy);
        coordinator.PendingOperationCount.Should().Be(WmiWriteCoordinator.MaxPendingOperationsPerKey);
        excessStarted.Should().BeFalse();

        firstInvocation.SetResult(WmiWriteResult.Success);
        await firstCall;
        var pendingResults = await Task.WhenAll(pendingCalls);

        pendingResults.Should().OnlyContain(result => result.Status == WmiWriteStatus.Succeeded);
        startOrder.Should().Equal(Enumerable.Range(0, WmiWriteCoordinator.MaxPendingOperationsPerKey));
        excessStarted.Should().BeFalse();
        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public async Task CompletedUniqueKeys_AreRemovedInsteadOfAccumulating()
    {
        var neverTimeout = NewSignal<object?>();
        var coordinator = new WmiWriteCoordinator(_ => neverTimeout.Task);

        for (var index = 0; index < 64; index++)
        {
            var result = await coordinator.ExecuteAsync(
                $"scope\u001fquery-{index}\u001fSetValue",
                () => Task.FromResult(WmiWriteResult.Success),
                TimeSpan.FromSeconds(1),
                WmiWriteResult.TimedOutIndeterminate,
                WmiWriteResult.NotStartedBusy);

            result.Status.Should().Be(WmiWriteStatus.Succeeded);
        }

        coordinator.ActiveKeyCount.Should().Be(0);
    }

    [Fact]
    public void WriteResult_PropagatesUnavailableIndeterminateAndBusyOutcomes()
    {
        Action unavailable = () => WmiWriteResult.Unavailable.ThrowIfNotSucceeded(
            "root\\WMI",
            "SELECT * FROM TEST_METHOD",
            "SetValue",
            800);
        Action indeterminate = () => WmiWriteResult.TimedOutIndeterminate.ThrowIfNotSucceeded(
            "root\\WMI",
            "SELECT * FROM TEST_METHOD",
            "SetValue",
            800);
        Action busy = () => WmiWriteResult.NotStartedBusy.ThrowIfNotSucceeded(
            "root\\WMI",
            "SELECT * FROM TEST_METHOD",
            "SetValue",
            800);
        Action failedIndeterminate = () => WmiWriteResult.FailedIndeterminate.ThrowIfNotSucceeded(
            "root\\WMI",
            "SELECT * FROM TEST_METHOD",
            "SetValue",
            800);
        Action success = () => WmiWriteResult.Success.ThrowIfNotSucceeded(
            "root\\WMI",
            "SELECT * FROM TEST_METHOD",
            "SetValue",
            800);
        var valueResult = WmiWriteResult<int>.Success(42);

        unavailable.Should().Throw<WmiWriteUnavailableException>()
            .Which.MethodName.Should().Be("SetValue");
        indeterminate.Should().Throw<WmiWriteIndeterminateException>()
            .Which.TimeoutMilliseconds.Should().Be(800);
        busy.Should().Throw<WmiWriteBusyException>()
            .Which.Status.Should().Be(WmiWriteStatus.NotStartedBusy);
        failedIndeterminate.Should().Throw<WmiWriteFailedIndeterminateException>()
            .Which.Status.Should().Be(WmiWriteStatus.FailedIndeterminate);
        success.Should().NotThrow();
        valueResult.GetValueOrThrow(
                "root\\WMI",
                "SELECT * FROM TEST_METHOD",
                "SetValue",
                800)
            .Should().Be(42);
    }

    [Theory]
    [InlineData(ManagementStatus.InvalidClass, true)]
    [InlineData(ManagementStatus.InvalidNamespace, true)]
    [InlineData(ManagementStatus.NotFound, true)]
    [InlineData(ManagementStatus.NotSupported, true)]
    [InlineData(ManagementStatus.AccessDenied, false)]
    [InlineData(ManagementStatus.Failed, false)]
    public void PreInvocationProviderFailure_OnlyKnownUnavailableStatusesFallback(
        ManagementStatus status,
        bool expectedUnavailable)
    {
        WMI.IsPreInvocationWriteUnavailable(status).Should().Be(expectedUnavailable);
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
