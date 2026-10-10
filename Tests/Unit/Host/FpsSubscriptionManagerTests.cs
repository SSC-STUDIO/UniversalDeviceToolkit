using UniversalDeviceToolkit.Host.Rpc.Handlers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class FpsSubscriptionManagerTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task NamedSubscriptions_AreIdempotentAndCannotRemoveOtherConsumers()
    {
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);

        Assert.True(await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop));
        Assert.True(await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop));
        Assert.True(await subscriptions.SubscribeAsync("dashboard", monitoring.StartAsync, monitoring.Stop));
        Assert.Equal(1, monitoring.Starts);
        Assert.True(await subscriptions.UnsubscribeAsync("osd"));
        Assert.True(await subscriptions.UnsubscribeAsync("osd"));
        Assert.True(await subscriptions.UnsubscribeAsync("missing"));
        Assert.Equal(0, monitoring.Stops);
        Assert.False(await subscriptions.UnsubscribeAsync("dashboard"));
        Assert.False(await subscriptions.UnsubscribeAsync("dashboard"));
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task LegacyReferences_KeepTheirCountAndCannotConsumeNamedSubscriptions()
    {
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);

        await subscriptions.SubscribeAsync(null, monitoring.StartAsync, monitoring.Stop);
        await subscriptions.SubscribeAsync(null, monitoring.StartAsync, monitoring.Stop);
        await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop);

        Assert.True(await subscriptions.UnsubscribeAsync(null));
        Assert.True(await subscriptions.UnsubscribeAsync(null));
        Assert.True(await subscriptions.UnsubscribeAsync(null));
        Assert.Equal(0, monitoring.Stops);
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(1, monitoring.Starts);
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task RepeatedNamedUnsubscribe_DoesNotConsumeLegacyReferences()
    {
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop);
        await subscriptions.SubscribeAsync(null, monitoring.StartAsync, monitoring.Stop);

        Assert.True(await subscriptions.UnsubscribeAsync("osd"));
        Assert.True(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(0, monitoring.Stops);
        Assert.False(await subscriptions.UnsubscribeAsync(null));
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task ConcurrentFirstSubscriptions_StartOnceAndUnsubscribeInOrder()
    {
        var started = NewCompletion();
        var release = NewCompletion();
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        async Task StartAsync()
        {
            await monitoring.StartAsync();
            started.SetResult();
            await release.Task;
        }

        var first = subscriptions.SubscribeAsync("osd", StartAsync, monitoring.Stop);
        await started.Task.WaitAsync(OperationTimeout);
        var second = subscriptions.SubscribeAsync("dashboard", monitoring.StartAsync, monitoring.Stop);
        var unsubscribe = subscriptions.UnsubscribeAsync("osd");
        Assert.False(second.IsCompleted);
        Assert.False(unsubscribe.IsCompleted);
        release.SetResult();

        await Task.WhenAll(first, second, unsubscribe).WaitAsync(OperationTimeout);
        Assert.True(await first);
        Assert.True(await second);
        Assert.True(await unsubscribe);
        Assert.Equal(1, monitoring.Starts);
        Assert.Equal(0, monitoring.Stops);
        Assert.False(await subscriptions.UnsubscribeAsync("dashboard"));
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task ConcurrentRetriesOfTheSameIdentity_KeepOneSubscription()
    {
        var release = NewCompletion();
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        async Task StartAsync()
        {
            await monitoring.StartAsync();
            await release.Task;
        }

        var retries = Enumerable.Range(0, 20)
            .Select(_ => subscriptions.SubscribeAsync("osd", StartAsync, monitoring.Stop)).ToArray();
        release.SetResult();
        Assert.All(await Task.WhenAll(retries).WaitAsync(OperationTimeout), value => Assert.True(value));
        Assert.Equal(1, monitoring.Starts);
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task FailedStartup_DoesNotLeaveAPhantomSubscriptionOrLoseTheGate()
    {
        var subscriptions = new FpsSubscriptionManager(() => true);
        var monitoring = new MonitoringProbe();
        await Assert.ThrowsAsync<InvalidOperationException>(() => subscriptions.SubscribeAsync("osd",
            () => Task.FromException(new InvalidOperationException("startup failed")), monitoring.Stop));

        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(0, monitoring.Stops);
        Assert.True(await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop));
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(1, monitoring.Starts);
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task CancellationWhileWaiting_DoesNotCreateASubscription()
    {
        var release = NewCompletion();
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        async Task StartAsync()
        {
            await monitoring.StartAsync();
            await release.Task;
        }
        var first = subscriptions.SubscribeAsync("dashboard", StartAsync, monitoring.Stop);
        using var cancellation = new CancellationTokenSource();
        var cancelled = subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(OperationTimeout));
        release.SetResult();
        await first.WaitAsync(OperationTimeout);

        Assert.True(await subscriptions.UnsubscribeAsync("osd"));
        Assert.False(await subscriptions.UnsubscribeAsync("dashboard"));
        Assert.Equal(1, monitoring.Starts);
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task CancellationAfterStartupBegins_DoesNotForgetAnAppliedSubscription()
    {
        var release = NewCompletion();
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        async Task StartAsync()
        {
            await monitoring.StartAsync();
            await release.Task;
        }
        using var cancellation = new CancellationTokenSource();
        var subscribe = subscriptions.SubscribeAsync("osd", StartAsync, monitoring.Stop, cancellation.Token);
        cancellation.Cancel();
        release.SetResult();

        Assert.True(await subscribe.WaitAsync(OperationTimeout));
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task BackgroundActivity_PreservesSubscriptionsAndRestartsOnlyWhenActive()
    {
        var active = false;
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => active);
        await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, monitoring.Stop);
        await subscriptions.SubscribeAsync(null, monitoring.StartAsync, monitoring.Stop);
        Assert.Equal(0, monitoring.Starts);

        active = true;
        await subscriptions.SynchronizeActivityAsync();
        await subscriptions.SynchronizeActivityAsync();
        Assert.Equal(1, monitoring.Starts);
        active = false;
        await subscriptions.SynchronizeActivityAsync();
        Assert.True(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(1, monitoring.Stops);
        active = true;
        await subscriptions.SynchronizeActivityAsync();
        Assert.Equal(2, monitoring.Starts);
        Assert.False(await subscriptions.UnsubscribeAsync(null));
        Assert.Equal(2, monitoring.Stops);
    }

    [Fact]
    public async Task QueuedBackgroundRestore_ReadsLatestActivityBeforeRestarting()
    {
        var release = NewCompletion();
        var active = true;
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => active);
        async Task StartAsync()
        {
            await monitoring.StartAsync();
            await release.Task;
        }
        var subscribe = subscriptions.SubscribeAsync("osd", StartAsync, monitoring.Stop);
        var staleRestore = subscriptions.SynchronizeActivityAsync();
        active = false;
        release.SetResult();
        await Task.WhenAll(subscribe, staleRestore).WaitAsync(OperationTimeout);

        Assert.Equal(1, monitoring.Starts);
        Assert.Equal(1, monitoring.Stops);
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task FailedBackgroundRestart_PreservesExistingSubscribersForRetry()
    {
        var active = true;
        var starts = 0;
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => active);
        Task StartAsync()
        {
            starts++;
            return starts == 2 ? Task.FromException(new InvalidOperationException("resume failed")) : monitoring.StartAsync();
        }
        await subscriptions.SubscribeAsync("osd", StartAsync, monitoring.Stop);
        active = false;
        await subscriptions.SynchronizeActivityAsync();
        active = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => subscriptions.SynchronizeActivityAsync());
        await subscriptions.SynchronizeActivityAsync();

        Assert.Equal(3, starts);
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(2, monitoring.Stops);
    }

    [Fact]
    public async Task FailedStop_PreservesTheIdentityUntilItsCleanupSucceeds()
    {
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        var stops = 0;
        void Stop()
        {
            stops++;
            if (stops == 1) throw new InvalidOperationException("stop failed");
            monitoring.Stop();
        }
        await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, Stop);
        await Assert.ThrowsAsync<InvalidOperationException>(() => subscriptions.UnsubscribeAsync("osd"));
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(2, stops);
        Assert.Equal(1, monitoring.Stops);
    }

    [Fact]
    public async Task FailedStop_FollowedBySubscribeRestartsInsteadOfTrustingOldRunningState()
    {
        var monitoring = new MonitoringProbe();
        var subscriptions = new FpsSubscriptionManager(() => true);
        var failStop = true;
        void Stop()
        {
            monitoring.Stop();
            if (failStop)
            {
                failStop = false;
                throw new InvalidOperationException("stop response failed after monitoring stopped");
            }
        }
        await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, Stop);
        await Assert.ThrowsAsync<InvalidOperationException>(() => subscriptions.UnsubscribeAsync("osd"));
        Assert.True(await subscriptions.SubscribeAsync("osd", monitoring.StartAsync, Stop));

        Assert.Equal(2, monitoring.Starts);
        Assert.False(await subscriptions.UnsubscribeAsync("osd"));
        Assert.Equal(2, monitoring.Stops);
    }

    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class MonitoringProbe
    {
        internal int Starts { get; private set; }
        internal int Stops { get; private set; }
        internal Task StartAsync() { Starts++; return Task.CompletedTask; }
        internal void Stop() => Stops++;
    }
}
