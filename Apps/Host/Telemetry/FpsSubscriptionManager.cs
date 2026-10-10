using System.Text.Json;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>Serializes FPS monitoring and keeps named subscriptions separate from legacy references.</summary>
internal sealed class FpsSubscriptionManager(Func<bool> isActive)
{
    private readonly SemaphoreSlim _changes = new(1, 1);
    private readonly HashSet<string> _subscriberIds = new(StringComparer.Ordinal);
    private int _legacySubscribers;
    private bool? _running = false;
    private Func<Task>? _startMonitoring;
    private Action? _stopMonitoring;
    private bool HasSubscribers => _legacySubscribers > 0 || _subscriberIds.Count > 0;

    internal async Task<bool> SubscribeAsync(string? subscriberId, Func<Task> startMonitoring,
        Action stopMonitoring, CancellationToken cancellationToken = default)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var firstSubscriber = !HasSubscribers;
            var added = subscriberId is null || _subscriberIds.Add(subscriberId);
            if (subscriberId is null) _legacySubscribers++;
            if (firstSubscriber)
            {
                _startMonitoring = startMonitoring;
                _stopMonitoring = stopMonitoring;
            }
            try
            {
                await SynchronizeMonitoringAsync().ConfigureAwait(false);
            }
            catch
            {
                if (added) RemoveSubscriber(subscriberId);
                if (!HasSubscribers) ClearCallbacks();
                throw;
            }
            return HasSubscribers;
        }
        finally { _changes.Release(); }
    }

    internal async Task<bool> UnsubscribeAsync(string? subscriberId, CancellationToken cancellationToken = default)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removed = RemoveSubscriber(subscriberId);
            try
            {
                await SynchronizeMonitoringAsync().ConfigureAwait(false);
            }
            catch
            {
                if (removed)
                {
                    if (subscriberId is null) _legacySubscribers++;
                    else _subscriberIds.Add(subscriberId);
                }
                throw;
            }
            if (!HasSubscribers) ClearCallbacks();
            return HasSubscribers;
        }
        finally { _changes.Release(); }
    }

    internal async Task SynchronizeActivityAsync(CancellationToken cancellationToken = default)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SynchronizeMonitoringAsync().ConfigureAwait(false);
        }
        finally { _changes.Release(); }
    }

    private async Task SynchronizeMonitoringAsync()
    {
        // Read activity inside the gate: an older background restore must not
        // restart monitoring after the UI has already hidden again.
        var desired = HasSubscribers && isActive();
        if (desired == _running) return;
        if (desired && _startMonitoring is { } startMonitoring)
            await startMonitoring().ConfigureAwait(false);
        else if (!desired)
        {
            try { _stopMonitoring?.Invoke(); }
            catch
            {
                // Stop may fail after changing the controller. Neither a new
                // subscribe nor an unsubscribe retry can trust its old state.
                _running = null;
                throw;
            }
        }
        _running = desired;
    }

    private bool RemoveSubscriber(string? subscriberId)
    {
        if (subscriberId is not null) return _subscriberIds.Remove(subscriberId);
        if (_legacySubscribers == 0) return false;
        _legacySubscribers--;
        return true;
    }

    private void ClearCallbacks()
    {
        _startMonitoring = null;
        _stopMonitoring = null;
    }

    internal static bool TryReadSubscriberId(JsonElement parameters, out string? subscriberId)
    {
        subscriberId = null;
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("subscriberId", out var property))
            return true;
        if (property.ValueKind != JsonValueKind.String) return false;
        var value = property.GetString();
        if (string.IsNullOrEmpty(value) || value.Length > 128 || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            return false;
        subscriberId = value;
        return true;
    }
}
