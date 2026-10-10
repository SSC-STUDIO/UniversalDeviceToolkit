using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Services;

public class BatteryDischargeRateMonitorService : IBatteryDischargeRateMonitorService, IDisposable
{
    private readonly IDelayProvider _delayProvider;
    private readonly BatteryHealthAlertSettings _healthAlertSettings;

    public BatteryDischargeRateMonitorService(
        BatteryHealthAlertSettings healthAlertSettings,
        IDelayProvider? delayProvider = null)
    {
        _healthAlertSettings = healthAlertSettings ?? new BatteryHealthAlertSettings();
        _delayProvider = delayProvider ?? new DefaultDelayProvider();
    }
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private readonly object _lock = new();
    private int _disposed;
    private DateTime _lastHealthNotifyUtc = DateTime.MinValue;
    private string? _lastHealthNotifyKey;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
                return _refreshTask is { IsCompleted: false };
        }
    }

    public Task StartStopIfNeededAsync()
    {
        if (!Battery.IsBatteryMonitoringSupported())
            return Task.CompletedTask;

        CancellationTokenSource? newCts = null;
        Task? newTask = null;

        lock (_lock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return Task.CompletedTask;

            if (_refreshTask != null)
                return Task.CompletedTask;

            if (_cts is not null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("CTS already disposed during cancellation", ex);
                }
                _cts.Dispose();
            }

            newCts = new CancellationTokenSource();
            _cts = newCts;

            var token = newCts.Token;

            newTask = Task.Run(async () =>
            {
                var iterationCount = 0;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        iterationCount++;

                        // Discharge min/max sampling stays frequent; health alerts are cheaper to throttle.
                        Battery.SetMinMaxDischargeRate();
                        if (iterationCount == 1 || iterationCount % 20 == 0)
                            EvaluateHealthAlerts();

                        await _delayProvider.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Battery monitoring service failed at iteration {iterationCount}", ex);

                        // Brief backoff then continue — do not permanently stop the service on a single fault.
                        try
                        {
                            await _delayProvider.Delay(TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }, token);

            _refreshTask = newTask;
        }

        _ = newTask.ContinueWith(t =>
        {
            lock (_lock)
            {
                if (ReferenceEquals(_refreshTask, t))
                    _refreshTask = null;
            }

            if (t.IsFaulted && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Battery monitoring service task faulted.", t.Exception);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? ctsToDispose = null;
        Task? taskToWait = null;

        lock (_lock)
        {
            if (_cts is not null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("CTS already disposed during cancellation", ex);
                }
                ctsToDispose = _cts;
                _cts = null;
            }

            taskToWait = _refreshTask;
            _refreshTask = null;
        }

        if (ctsToDispose is not null)
        {
            try
            {
                ctsToDispose.Cancel();
            }
            catch (ObjectDisposedException ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("CTS already disposed during stop cancellation", ex);
            }
        }

        if (taskToWait is not null)
        {
            try
            {
                await taskToWait.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Battery monitoring service did not stop in time.");
            }
            catch (OperationCanceledException ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Battery monitoring service wait cancelled", ex);
            }
        }

        ctsToDispose?.Dispose();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Battery monitoring service stopped");
    }

    /// <summary>
    /// Evaluates design/full-charge health against configured thresholds using the same
    /// Battery information path as the rest of the app (no extra WMI).
    /// </summary>
    private void EvaluateHealthAlerts()
    {
        try
        {
            var store = _healthAlertSettings.Store;
            store.Normalize();
            if (!store.AlertsEnabled)
                return;

            var info = Battery.GetBatteryInformation();
            var health = info.BatteryHealth;
            if (health <= 0)
                return;

            string? key = null;
            string? message = null;
            var priority = NotificationPriority.Normal;

            if (health < store.CriticalHealthThreshold)
            {
                key = "critical";
                priority = NotificationPriority.High;
                message = $"Battery health critical: {health:0.#}% (threshold {store.CriticalHealthThreshold}%).";
            }
            else if (health < store.LowHealthThreshold)
            {
                key = "low";
                message = $"Battery health low: {health:0.#}% (threshold {store.LowHealthThreshold}%).";
            }

            if (store.TemperatureThresholdC > 0 && info.BatteryTemperatureC is { } tempC && tempC >= store.TemperatureThresholdC)
            {
                key = "temp";
                priority = NotificationPriority.High;
                message = $"Battery temperature high: {tempC:0.#} °C (threshold {store.TemperatureThresholdC:0.#} °C).";
            }

            if (key is null || message is null)
                return;

            // Cooldown: at most one notification per key every 6 hours.
            var now = DateTime.UtcNow;
            if (string.Equals(_lastHealthNotifyKey, key, StringComparison.Ordinal)
                && now - _lastHealthNotifyUtc < TimeSpan.FromHours(6))
                return;

            _lastHealthNotifyKey = key;
            _lastHealthNotifyUtc = now;
            MessagingCenter.Publish(new NotificationMessage(NotificationType.AutomationNotification, priority, message));
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Battery health alert evaluation failed.", ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        if (disposing)
        {
            CancellationTokenSource? ctsToDispose = null;
            Task? taskToWait = null;

            lock (_lock)
            {
                if (_cts is not null)
                {
                    ctsToDispose = _cts;
                    _cts = null;
                }
                taskToWait = _refreshTask;
                _refreshTask = null;
            }

            if (ctsToDispose is not null)
            {
                try
                {
                    ctsToDispose.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("CTS already disposed during dispose", ex);
                }
            }

            // Non-blocking dispose wait (Pillar A, BUG-2026-07-09-008): the refresh task runs
            // ConfigureAwait(false) on the threadpool, so a bounded Wait cannot deadlock the
            // Dispatcher, but we still avoid needless blocking in the common completed-task case
            // via an IsCompletedSuccessfully fast path (matching the AIController precedent).
            if (taskToWait is not null)
            {
                try
                {
                    if (!taskToWait.IsCompletedSuccessfully && !taskToWait.Wait(TimeSpan.FromSeconds(5)))
                    {
                        // Timed out: leave the task running and observe any later fault so it is
                        // never raised as an unobserved task exception.
                        taskToWait.ContinueWith(t =>
                        {
                            if (t.IsFaulted && Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace("Battery monitoring service dispose wait timed out and faulted later.", t.Exception);
                        }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    }
                }
                catch (AggregateException ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("Battery monitoring service dispose wait faulted.", ex);
                }
            }

            ctsToDispose?.Dispose();
        }
    }
}
