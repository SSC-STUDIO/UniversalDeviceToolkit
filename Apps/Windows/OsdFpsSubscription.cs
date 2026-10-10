namespace UniversalDeviceToolkit.Windows;

/// <summary>Retries uncertain FPS mutations with the same idempotent Host identity.</summary>
internal sealed class OsdFpsSubscription(Func<string, object, CancellationToken, Task> invoke)
{
    private static readonly object Parameters = new { subscriberId = "webview2-osd" };
    private bool? _subscribed = false;

    internal void Reset() => _subscribed = false;

    internal async Task SynchronizeAsync(bool desired, CancellationToken cancellationToken)
    {
        if (_subscribed == desired) return;
        // A missing response cannot establish whether the Host applied the
        // change. Both show and hide must retry an uncertain subscription.
        _subscribed = null;
        await invoke(desired ? "sensors.subscribeFps" : "sensors.unsubscribeFps", Parameters, cancellationToken);
        _subscribed = desired;
    }
}
