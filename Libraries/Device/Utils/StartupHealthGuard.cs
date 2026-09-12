using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Minimal logging abstraction consumed by <see cref="StartupHealthGuard"/>. When
/// callers do not supply an implementation (e.g. library callers without DI), the
/// guard falls back to <c>Serilog.Log.Logger</c> for direct sink writes that
/// avoid pulling the singleton <see cref="Log"/> on hot startup paths.
/// </summary>
public interface ISafeStartLogger
{
    void Trace(string message, Exception? ex = null);
    void Warning(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
}

internal sealed class SerilogSafeStartLogger : ISafeStartLogger
{
    public void Trace(string message, Exception? ex = null)
    {
        try { global::Serilog.Log.Logger.Verbose(ex, "{Message}", message); }
        catch { /* Logging must never throw - swallow */ }
    }

    public void Warning(string message, Exception? ex = null)
    {
        try { global::Serilog.Log.Logger.Warning(ex, "{Message}", message); }
        catch { /* Logging must never throw - swallow */ }
    }

    public void Error(string message, Exception? ex = null)
    {
        try { global::Serilog.Log.Logger.Error(ex, "{Message}", message); }
        catch { /* Logging must never throw - swallow */ }
    }
}

/// <summary>
/// Tracks per-step startup health, enforces step-level timeouts, and decides
/// whether the host should fall back to <c>--safe-start</c>. All members are
/// thread-safe; every method swallows non-fatal exceptions and routes them to
/// the configured <see cref="ISafeStartLogger"/> (Serilog when no logger is
/// supplied). <see cref="OutOfMemoryException"/> and
/// <see cref="StackOverflowException"/> intentionally propagate so the process
/// can crash instead of continuing in a corrupt state.
/// </summary>
public class StartupHealthGuard
{
    public const int DefaultConsecutiveFailureThreshold = 3;

    private readonly ISafeStartLogger _logger;
    private readonly int _consecutiveFailureThreshold;
    private readonly Dictionary<string, TimeSpan> _stepTimeouts = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private int _consecutiveFailureCount;
    private bool _shouldEnterSafeMode;

    /// <summary>
    /// Raised whenever <see cref="ConsecutiveFailureCount"/> transitions to a
    /// new value. The argument is the up-to-date failure count.
    /// </summary>
    public event EventHandler<int>? ConsecutiveFailuresChanged;

    public StartupHealthGuard()
        : this(logger: null, consecutiveFailureThreshold: DefaultConsecutiveFailureThreshold)
    {
    }

    public StartupHealthGuard(int consecutiveFailureThreshold)
        : this(logger: null, consecutiveFailureThreshold: consecutiveFailureThreshold)
    {
    }

    public StartupHealthGuard(ISafeStartLogger? logger)
        : this(logger, DefaultConsecutiveFailureThreshold)
    {
    }

    public StartupHealthGuard(ISafeStartLogger? logger, int consecutiveFailureThreshold)
    {
        _logger = logger ?? new SerilogSafeStartLogger();
        _consecutiveFailureThreshold = consecutiveFailureThreshold > 0
            ? consecutiveFailureThreshold
            : DefaultConsecutiveFailureThreshold;
    }

    /// <summary>
    /// Number of consecutive step failures observed since the last reset. The
    /// counter is incremented on every failed <see cref="TryRunStep"/> call and
    /// cleared by <see cref="ResetFailureCount"/> or by a single successful run.
    /// </summary>
    public int ConsecutiveFailureCount
    {
        get
        {
            lock (_gate) return _consecutiveFailureCount;
        }
    }

    /// <summary>
    /// When true, the host should switch into safe-start mode: skip non-critical
    /// initialization steps, log a diagnostic message, and surface the state to
    /// the user. Becomes true once the threshold is exceeded; cleared only via
    /// <see cref="ResetFailureCount"/>.
    /// </summary>
    public bool ShouldEnterSafeMode
    {
        get
        {
            lock (_gate) return _shouldEnterSafeMode;
        }
    }

    /// <summary>
    /// Registers the per-step timeout. Duplicate registrations keep the most
    /// recent value. The call is thread-safe.
    /// </summary>
    public void RegisterStep(string name, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Step name must not be null or whitespace.", nameof(name));

        lock (_gate)
        {
            _stepTimeouts[name] = timeout;
        }
    }

    /// <summary>
    /// Executes <paramref name="action"/>, applying the registered timeout.
    /// On success returns true with a null error. On failure (timeout, thrown
    /// exception, or <see cref="OperationCanceledException"/>), returns false,
    /// populates <paramref name="error"/>, and updates the consecutive failure
    /// counter. The method never throws for non-critical exceptions; only OOM
    /// and stack overflow are allowed to escape.
    /// </summary>
    public bool TryRunStep(string name, Action action, out Exception? error)
    {
        if (action is null)
        {
            error = new ArgumentNullException(nameof(action));
            LogError($"Step '{name}' has a null action.", error);
            RegisterFailureLocked();
            return false;
        }

        return TryRunStep(name, _ => action(), out error);
    }

    /// <summary>
    /// Token-observing variant of <see cref="TryRunStep(string, Action, out Exception?)"/>.
    /// The timeout token is passed into <paramref name="action"/> so cooperative
    /// steps can abort; a finite timeout still races the call and marks failure
    /// if the step overruns.
    /// </summary>
    public bool TryRunStep(string name, Action<CancellationToken> action, out Exception? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = new ArgumentException("Step name must not be null or whitespace.", nameof(name));
            LogError("Refusing to run step with empty name.", error);
            RegisterFailureLocked();
            return false;
        }

        if (action is null)
        {
            error = new ArgumentNullException(nameof(action));
            LogError($"Step '{name}' has a null action.", error);
            RegisterFailureLocked();
            return false;
        }

        TimeSpan timeout;
        lock (_gate)
        {
            if (!_stepTimeouts.TryGetValue(name, out timeout))
                timeout = Timeout.InfiniteTimeSpan;
        }

        if (timeout <= TimeSpan.Zero)
            timeout = Timeout.InfiniteTimeSpan;

        using var cts = timeout == Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource()
            : new CancellationTokenSource(timeout);

        try
        {
            var startedMs = Environment.TickCount64;
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                action(cts.Token);
            }
            else
            {
                var run = Task.Run(() => action(cts.Token), cts.Token);
                var waitMs = timeout.TotalMilliseconds >= int.MaxValue
                    ? Timeout.Infinite
                    : Math.Max(1, (int)timeout.TotalMilliseconds);
                if (!run.Wait(waitMs))
                {
                    error = new TimeoutException(
                        $"Step '{name}' exceeded its timeout of {timeout}.");
                    LogWarning($"Step '{name}' exceeded its timeout of {timeout}; marking failed.");
                    RegisterFailureLocked();
                    return false;
                }

                run.GetAwaiter().GetResult();
            }

            RegisterSuccessLocked(name);
            return true;
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (StackOverflowException)
        {
            throw;
        }
        catch (AggregateException ex)
        {
            var inner = ex.InnerException ?? ex;
            if (inner is OperationCanceledException && cts.IsCancellationRequested)
            {
                error = new TimeoutException(
                    $"Step '{name}' exceeded its timeout of {timeout}.", inner);
                LogWarning($"Step '{name}' cancelled by timeout after {timeout}; marking failed.", inner);
                RegisterFailureLocked();
                return false;
            }

            error = inner;
            LogError($"Step '{name}' threw {inner.GetType().Name}.", inner);
            RegisterFailureLocked();
            return false;
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            error = new TimeoutException(
                $"Step '{name}' exceeded its timeout of {timeout}.", ex);
            LogWarning($"Step '{name}' cancelled by timeout after {timeout}; marking failed.", ex);
            RegisterFailureLocked();
            return false;
        }
        catch (Exception ex)
        {
            error = ex;
            LogError($"Step '{name}' threw {ex.GetType().Name}.", ex);
            RegisterFailureLocked();
            return false;
        }
    }

    /// <summary>
    /// Async-friendly variant of <see cref="TryRunStep(string, Action, out Exception?)"/>.
    /// The timeout token races the task via <see cref="Task.WaitAsync(CancellationToken)"/>
    /// so a hung step cannot block initialization indefinitely.
    /// </summary>
    public Task<(bool Ok, Exception? Error)> TryRunStepAsync(string name, Func<Task> action)
    {
        if (action is null)
        {
            LogError($"Step '{name}' has a null async action.");
            RegisterFailureLocked();
            return Task.FromResult<(bool, Exception?)>((false, new ArgumentNullException(nameof(action))));
        }

        return TryRunStepAsync(name, _ => action());
    }

    /// <summary>
    /// Token-observing async variant. <paramref name="action"/> receives the
    /// timeout token so cooperative I/O can abort; <see cref="Task.WaitAsync(CancellationToken)"/>
    /// still bounds the wait if the action ignores cancellation.
    /// </summary>
    public async Task<(bool Ok, Exception? Error)> TryRunStepAsync(string name, Func<CancellationToken, Task> action)
    {
        if (action is null)
        {
            LogError($"Step '{name}' has a null async action.");
            RegisterFailureLocked();
            return (false, new ArgumentNullException(nameof(action)));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            var error = new ArgumentException("Step name must not be null or whitespace.", nameof(name));
            LogError("Refusing to run step with empty name.", error);
            RegisterFailureLocked();
            return (false, error);
        }

        TimeSpan timeout;
        lock (_gate)
        {
            if (!_stepTimeouts.TryGetValue(name, out timeout))
                timeout = Timeout.InfiniteTimeSpan;
        }

        if (timeout <= TimeSpan.Zero)
            timeout = Timeout.InfiniteTimeSpan;

        using var cts = timeout == Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource()
            : new CancellationTokenSource(timeout);

        try
        {
            await action(cts.Token).WaitAsync(cts.Token).ConfigureAwait(false);

            if (cts.IsCancellationRequested)
            {
                var ex = new TimeoutException($"Step '{name}' exceeded its timeout of {timeout}.");
                LogWarning($"Step '{name}' exceeded its timeout of {timeout}; marking failed.");
                RegisterFailureLocked();
                return (false, ex);
            }

            RegisterSuccessLocked(name);
            return (true, null);
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (StackOverflowException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            var wrapped = new TimeoutException($"Step '{name}' exceeded its timeout of {timeout}.", ex);
            LogWarning($"Step '{name}' exceeded its timeout of {timeout}; marking failed.", wrapped);
            RegisterFailureLocked();
            return (false, wrapped);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            var wrapped = new TimeoutException($"Step '{name}' exceeded its timeout of {timeout}.", ex);
            LogWarning($"Step '{name}' cancelled by timeout after {timeout}; marking failed.", wrapped);
            RegisterFailureLocked();
            return (false, wrapped);
        }
        catch (Exception ex)
        {
            LogError($"Step '{name}' threw {ex.GetType().Name}.", ex);
            RegisterFailureLocked();
            return (false, ex);
        }
    }

    /// <summary>
    /// Clears the consecutive failure counter and the safe-mode latched flag.
    /// Use after a successful full startup sequence to start fresh.
    /// </summary>
    public void ResetFailureCount()
    {
        int previous;
        bool wasSafeMode;
        lock (_gate)
        {
            previous = _consecutiveFailureCount;
            _consecutiveFailureCount = 0;
            wasSafeMode = _shouldEnterSafeMode;
            _shouldEnterSafeMode = false;
        }

        if (previous != 0)
            LogTrace($"Consecutive failure count reset (was {previous}).");

        if (wasSafeMode)
            LogWarning("Safe-mode latch cleared after reset.");

        TryRaiseConsecutiveFailuresChanged(0);
    }

    /// <summary>
    /// Allows tests / orchestrators to manually flag that the next startup
    /// should enter safe mode. Mirrors running past the threshold but lets
    /// external signals (e.g. a persisted last-run crash) participate in the
    /// decision.
    /// </summary>
    public void MarkShouldEnterSafeMode()
    {
        lock (_gate)
        {
            _shouldEnterSafeMode = true;
        }
        LogWarning("Safe-mode latch explicitly set by caller.");
    }

    /// <summary>
    /// Reads the previously persisted consecutive-failure count. Returns 0
    /// when no marker exists yet or when the file is unreadable; never throws.
    /// </summary>
    public static int ReadPersistedConsecutiveFailureCount()
    {
        try
        {
            var path = GetPersistedStatePath();
            if (!File.Exists(path))
                return 0;

            var text = File.ReadAllText(path).Trim();
            return int.TryParse(text, out var count) && count > 0 ? count : 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            try { global::Serilog.Log.Logger.Warning(ex, "{Message}", "Failed to read persisted startup-health state; assuming zero."); }
            catch { /* Persistence read failure must never throw. */ }
            return 0;
        }
    }

    /// <summary>
    /// Persists <paramref name="consecutiveFailureCount"/> and
    /// <paramref name="shouldEnterSafeMode"/> to disk so the next process
    /// invocation can reason about the previous run. A non-positive failure
    /// count deletes the marker. Never throws.
    /// </summary>
    public static void WritePersistedState(int consecutiveFailureCount, bool shouldEnterSafeMode)
    {
        try
        {
            var path = GetPersistedStatePath();

            if (consecutiveFailureCount <= 0 && !shouldEnterSafeMode)
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            var payload = consecutiveFailureCount.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllText(path, payload);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            try { global::Serilog.Log.Logger.Warning(ex, "{Message}", "Failed to persist startup-health state."); }
            catch { /* Persistence write failure must never throw. */ }
        }
    }

    private static string GetPersistedStatePath()
    {
        try
        {
            return Path.Combine(Folders.AppData, "startup_health.json");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "startup_health.json");
        }
    }

    private static string GetHardwareInitInProgressPath()
    {
        try
        {
            return Path.Combine(Folders.AppData, "hardware_init_in_progress.flag");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "hardware_init_in_progress.flag");
        }
    }

    /// <summary>
    /// Marks that hardware background initialization has started. Cleared on success.
    /// If present on next launch, treat previous run as interrupted and enter safe-start.
    /// </summary>
    public static void MarkHardwareInitInProgress()
    {
        try
        {
            File.WriteAllText(GetHardwareInitInProgressPath(), DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            try { global::Serilog.Log.Logger.Warning(ex, "{Message}", "Failed to write hardware-init in-progress flag."); }
            catch { /* never throw */ }
        }
    }

    /// <summary>Clears the incomplete hardware-init marker after a successful pass.</summary>
    public static void ClearHardwareInitInProgress()
    {
        try
        {
            var path = GetHardwareInitInProgressPath();
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            try { global::Serilog.Log.Logger.Warning(ex, "{Message}", "Failed to clear hardware-init in-progress flag."); }
            catch { /* never throw */ }
        }
    }

    /// <summary>True when the previous process died mid hardware initialization.</summary>
    public static bool IsHardwareInitInProgressMarkerPresent()
    {
        try
        {
            return File.Exists(GetHardwareInitInProgressPath());
        }
        catch
        {
            return false;
        }
    }

    private void RegisterSuccessLocked(string name)
    {
        int previous;
        lock (_gate)
        {
            previous = _consecutiveFailureCount;
            _consecutiveFailureCount = 0;
            if (_shouldEnterSafeMode)
                _shouldEnterSafeMode = false;
        }

        if (previous != 0)
            LogTrace($"Step '{name}' succeeded; consecutive failure count reset from {previous} to 0.");
    }

    private void RegisterFailureLocked()
    {
        int updated;
        bool trippedSafeMode;
        int previous;
        lock (_gate)
        {
            previous = _consecutiveFailureCount;
            updated = ++_consecutiveFailureCount;
            trippedSafeMode = updated >= _consecutiveFailureThreshold && !_shouldEnterSafeMode;
            if (trippedSafeMode)
                _shouldEnterSafeMode = true;
        }

        if (updated != previous)
            TryRaiseConsecutiveFailuresChanged(updated);

        if (trippedSafeMode)
            LogWarning(
                $"Consecutive startup failures reached {updated} (threshold {_consecutiveFailureThreshold}); safe-mode latch engaged.");
    }

    private void TryRaiseConsecutiveFailuresChanged(int newCount)
    {
        var handler = ConsecutiveFailuresChanged;
        if (handler is null) return;

        try { handler.Invoke(this, newCount); }
        catch (Exception ex) { LogError("ConsecutiveFailuresChanged handler threw.", ex); }
    }

    private void LogTrace(string message) => SafeInvoke(_logger.Trace, message, null);
    private void LogWarning(string message, Exception? ex = null) => SafeInvoke(_logger.Warning, message, ex);
    private void LogError(string message, Exception? ex = null) => SafeInvoke(_logger.Error, message, ex);

    private void SafeInvoke(Action<string, Exception?> sink, string message, Exception? ex)
    {
        try { sink(message, ex); }
        catch (Exception inner) when (inner is not OutOfMemoryException and not StackOverflowException)
        {
            // A logger that throws must not break the startup guard. As a last
            // resort, write to the static Serilog logger directly.
            try { global::Serilog.Log.Logger.Error(inner, "{Message}", "StartupHealthGuard logger threw; falling back to Serilog."); }
            catch { /* ignore */ }
        }
    }
}
