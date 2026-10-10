using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Features.CursorPointer;

/// <summary>
/// Watches Windows light/dark preference changes (debounced) and re-applies the
/// matching UDT cursor scheme. Ported from the retired custom-mouse plugin's
/// ThemeWatcherRuntime; logging switched to SharedLog.
/// </summary>
public sealed class SystemThemeWatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Timer? _debounceTimer;
    private string? _lastAppliedTheme;
    private Func<string, Task>? _themeChanged;

    public event Func<string, Task>? ThemeChanged
    {
        add { lock (_gate) _themeChanged += value; }
        remove { lock (_gate) _themeChanged -= value; }
    }

    public void Start(string? initialLastAppliedTheme)
    {
        lock (_gate)
        {
            _lastAppliedTheme = initialLastAppliedTheme;
            if (_cts != null)
                return;

            _cts = new CancellationTokenSource();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    public void NotifyThemeApplied(string? theme)
    {
        lock (_gate)
            _lastAppliedTheme = theme;
    }

    public string? PeekLastAppliedTheme()
    {
        lock (_gate)
            return _lastAppliedTheme;
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Timer? timer;

        lock (_gate)
        {
            cts = _cts;
            timer = _debounceTimer;
            _cts = null;
            _debounceTimer = null;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        if (cts == null)
            return;

        try { cts.Cancel(); }
        catch (ObjectDisposedException) { /* ignore — already disposed */ }
        finally
        {
            timer?.Dispose();
            cts.Dispose();
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _cts != null && !_cts.IsCancellationRequested;
        }
    }

    /// <summary>Cancellation token of the current run, or None when not running.</summary>
    public CancellationToken GetCancellationToken()
    {
        lock (_gate)
            return _cts?.Token ?? CancellationToken.None;
    }

    public void Dispose() => Stop();

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        Timer? timerToStart = null;
        lock (_gate)
        {
            if (_cts == null || _cts.IsCancellationRequested)
                return;

            _debounceTimer?.Dispose();
            timerToStart = new Timer(OnDebounceElapsed, null, DebounceInterval, Timeout.InfiniteTimeSpan);
            _debounceTimer = timerToStart;
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        CancellationToken token;
        lock (_gate)
        {
            if (_cts == null || _cts.IsCancellationRequested)
                return;

            token = _cts.Token;
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var currentTheme = IsSystemLightTheme() ? "light" : "dark";

                Func<string, Task>? handler;
                lock (_gate)
                {
                    if (string.Equals(currentTheme, _lastAppliedTheme, StringComparison.OrdinalIgnoreCase))
                        return;

                    _lastAppliedTheme = currentTheme;
                    handler = _themeChanged;
                }

                if (handler is not null)
                    await handler.Invoke(currentTheme).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* expected during stop/dispose */ }
            catch (Exception ex)
            {
                if (Shared.Logging.SharedLog.IsTraceEnabled)
                    Shared.Logging.SharedLog.Trace("CursorPointer: theme watcher failed.", ex);
            }
        }, token);
    }

    internal static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue ? intValue != 0 : true;
        }
        catch (Exception ex)
        {
            if (Shared.Logging.SharedLog.IsTraceEnabled)
                Shared.Logging.SharedLog.Trace("CursorPointer: Failed to read AppsUseLightTheme.", ex);
            return true;
        }
    }
}
