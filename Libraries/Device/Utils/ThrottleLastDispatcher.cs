using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// ThrottleLastDispatcher - Only executes the last call within a given interval.
/// When a new task arrives within the interval, the previous task will be cancelled.
/// </summary>
public class ThrottleLastDispatcher : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly object _lock = new();
    private long _currentVersion;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    private readonly IDelayProvider? _delayProvider;

    /// <summary>
    /// Initializes a new ThrottleLastDispatcher instance.
    /// </summary>
    /// <param name="interval">The throttle interval.</param>
    /// <param name="tag">Optional tag for logging (reserved for API compatibility).</param>
    /// <param name="delayProvider">Optional delay provider for testing.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when interval is negative.</exception>
    public ThrottleLastDispatcher(TimeSpan interval, string? tag = null, IDelayProvider? delayProvider = null)
    {
        if (interval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval));

        _interval = interval;
        _ = tag;
        _delayProvider = delayProvider;
    }

    /// <summary>
    /// Schedules a task. If a new task arrives within the interval, the previous task will be cancelled.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the task is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher has been disposed.</exception>
    public async Task DispatchAsync(Func<Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        long myVersion;
        CancellationToken token;
        lock (_lock)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThrottleLastDispatcher));

            myVersion = ++_currentVersion;

            var oldCts = _cancellationTokenSource;
            oldCts?.Cancel();

            _cancellationTokenSource = new CancellationTokenSource();
            token = _cancellationTokenSource.Token;

            oldCts?.Dispose();
        }

        try
        {
            if (_interval > TimeSpan.Zero)
            {
                if (_delayProvider is not null)
                    await _delayProvider.Delay(_interval, token).ConfigureAwait(false);
                else
                    await Task.Delay(_interval, token).ConfigureAwait(false);
            }

            lock (_lock)
            {
                if (myVersion != _currentVersion)
                    return;
            }

            await task().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Task cancelled due to throttling
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _currentVersion++;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
