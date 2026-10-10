using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc.Handlers;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Host;

[Trait("Category", TestCategories.Unit)]
public sealed class OsdFpsSubscriptionTests
{
    [Fact]
    public async Task LostUnsubscribeResponse_ShowingAgainRestartsTheHostSubscription()
    {
        var host = new HostProbe();
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        host.LoseNextUnsubscribeResponse = true;
        await Assert.ThrowsAsync<TimeoutException>(() => host.Osd.SynchronizeAsync(false, CancellationToken.None));
        Assert.Equal(1, host.Stops);

        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        Assert.Equal(2, host.Starts);
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);
        Assert.Equal(2, host.Stops);
        Assert.Equal(new[] { "sensors.subscribeFps", "sensors.unsubscribeFps", "sensors.subscribeFps", "sensors.unsubscribeFps" }, host.Methods);
    }

    [Fact]
    public async Task LostSubscribeResponse_RetryDoesNotAddASecondReference()
    {
        var host = new HostProbe { LoseNextSubscribeResponse = true };
        await Assert.ThrowsAsync<TimeoutException>(() => host.Osd.SynchronizeAsync(true, CancellationToken.None));
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);

        Assert.Equal(1, host.Starts);
        Assert.Equal(1, host.Stops);
        Assert.False(await host.Subscriptions.UnsubscribeAsync("webview2-osd"));
    }

    [Fact]
    public async Task LostSubscribeResponse_HidingStillCleansUpTheAppliedSubscription()
    {
        var host = new HostProbe { LoseNextSubscribeResponse = true };
        await Assert.ThrowsAsync<TimeoutException>(() => host.Osd.SynchronizeAsync(true, CancellationToken.None));
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);

        Assert.Equal(1, host.Starts);
        Assert.Equal(1, host.Stops);
    }

    [Fact]
    public async Task LostUnsubscribeResponse_RetryCannotRemoveAnotherConsumersLegacyReference()
    {
        var host = new HostProbe();
        await host.Subscriptions.SubscribeAsync(null, host.StartAsync, host.Stop);
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        host.LoseNextUnsubscribeResponse = true;
        await Assert.ThrowsAsync<TimeoutException>(() => host.Osd.SynchronizeAsync(false, CancellationToken.None));
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);

        Assert.Equal(0, host.Stops);
        Assert.False(await host.Subscriptions.UnsubscribeAsync(null));
        Assert.Equal(1, host.Stops);
    }

    [Fact]
    public async Task RejectedSubscribe_IsRetriedWhenTheOverlayIsShownAgain()
    {
        var host = new HostProbe { RejectNextSubscribe = true };
        await Assert.ThrowsAsync<IOException>(() => host.Osd.SynchronizeAsync(true, CancellationToken.None));
        Assert.Equal(0, host.Starts);
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);

        Assert.Equal(1, host.Starts);
        Assert.Equal(1, host.Stops);
    }

    [Fact]
    public async Task ConfirmedState_DoesNotRepeatRpcAndInitialHiddenStateNeedsNoRpc()
    {
        var host = new HostProbe();
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);
        Assert.Empty(host.Methods);
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);
        await host.Osd.SynchronizeAsync(false, CancellationToken.None);

        Assert.Equal(new[] { "sensors.subscribeFps", "sensors.unsubscribeFps" }, host.Methods);
    }

    [Fact]
    public async Task HostRestartReset_RequiresAnewSubscribeWithTheSameIdentity()
    {
        var host = new HostProbe();
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);
        host.Subscriptions = new FpsSubscriptionManager(() => true);
        host.Osd.Reset();
        await host.Osd.SynchronizeAsync(true, CancellationToken.None);

        Assert.Equal(2, host.Starts);
        Assert.Equal(new[] { "sensors.subscribeFps", "sensors.subscribeFps" }, host.Methods);
    }

    private sealed class HostProbe
    {
        internal FpsSubscriptionManager Subscriptions { get; set; } = new(() => true);
        internal OsdFpsSubscription Osd { get; }
        internal List<string> Methods { get; } = [];
        internal bool LoseNextSubscribeResponse { get; set; }
        internal bool LoseNextUnsubscribeResponse { get; set; }
        internal bool RejectNextSubscribe { get; set; }
        internal int Starts { get; private set; }
        internal int Stops { get; private set; }

        internal HostProbe() => Osd = new OsdFpsSubscription(InvokeAsync);
        internal Task StartAsync() { Starts++; return Task.CompletedTask; }
        internal void Stop() => Stops++;

        private async Task InvokeAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            Methods.Add(method);
            Assert.True(FpsSubscriptionManager.TryReadSubscriberId(JsonSerializer.SerializeToElement(parameters), out var subscriberId));
            Assert.Equal("webview2-osd", subscriberId);
            if (method == "sensors.subscribeFps")
            {
                if (RejectNextSubscribe)
                {
                    RejectNextSubscribe = false;
                    throw new IOException("Host rejected the request");
                }
                await Subscriptions.SubscribeAsync(subscriberId, StartAsync, Stop, cancellationToken);
                if (LoseNextSubscribeResponse)
                {
                    LoseNextSubscribeResponse = false;
                    throw new TimeoutException("subscribe response lost");
                }
            }
            else
            {
                Assert.Equal("sensors.unsubscribeFps", method);
                await Subscriptions.UnsubscribeAsync(subscriberId, cancellationToken);
                if (LoseNextUnsubscribeResponse)
                {
                    LoseNextUnsubscribeResponse = false;
                    throw new TimeoutException("unsubscribe response lost");
                }
            }
        }
    }
}
