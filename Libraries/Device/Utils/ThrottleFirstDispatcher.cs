using System;
using System.Threading.Tasks;
using NeoSmart.AsyncLock;

namespace UniversalDeviceToolkit.Lib.Utils;

public class ThrottleFirstDispatcher
{
    private readonly AsyncLock _lock = new();

    private DateTime _lastEvent = DateTime.MinValue;
    private readonly TimeSpan _interval;
    private readonly string? _tag;

    public ThrottleFirstDispatcher(TimeSpan interval, string? tag = null)
    {
        if (interval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));

        _interval = interval;
        _tag = tag;
    }

    public TimeSpan Interval => _interval;

    public async Task DispatchAsync(Func<Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            var diff = DateTime.UtcNow - _lastEvent;

            if (diff < _interval)
            {
                return;
            }

            if (_tag is not null && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Allowing... [tag={_tag}, diff={diff.TotalMilliseconds}ms]");

            _lastEvent = DateTime.UtcNow;

            await task().ConfigureAwait(false);
        }
    }

    public async Task ResetAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            _lastEvent = DateTime.MinValue;
        }
    }
}
