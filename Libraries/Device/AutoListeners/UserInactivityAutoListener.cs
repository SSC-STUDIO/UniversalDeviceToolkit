using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Timer = System.Threading.Timer;

namespace UniversalDeviceToolkit.Lib.AutoListeners;

public class UserInactivityAutoListener(IMainThreadDispatcher mainThreadDispatcher)
    : AbstractAutoListener<UserInactivityAutoListener.ChangedEventArgs>
{
    public class ChangedEventArgs(TimeSpan timerResolution, uint tickCount) : EventArgs
    {
        public TimeSpan TimerResolution { get; } = timerResolution;
        public uint TickCount { get; } = tickCount;
    }

    private readonly TimeSpan _timerResolution = TimeSpan.FromSeconds(10);
    private readonly object _lock = new();

    // Hook handles are touched only on the pump thread while the window lives.
    private PumpedMessageWindow? _window;
    private HOOKPROC? _hookProc;
    private HHOOK _kbHook;
    private HHOOK _mouseHook;
    private uint _tickCount;
    private Timer? _timer;

    public TimeSpan InactivityTimeSpan => _timerResolution * _tickCount;

    protected override Task StartAsync() => mainThreadDispatcher.DispatchAsync(() =>
    {
        lock (_lock)
        {
            _timer = new Timer(TimerCallback, null, _timerResolution, _timerResolution);
            _tickCount = 0;
            _hookProc = HookProc;

            // The low-level hooks are installed on the pump thread inside the
            // window: hook callbacks only flow while the installing thread
            // pumps messages, which the headless dispatcher thread never does.
            _window = new PumpedMessageWindow(
                "UniversalDeviceToolkit_UserInactivityListenerWindow",
                DefaultWndProc,
                onStarted: _ =>
                {
                    _kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookProc!, HINSTANCE.Null, 0);
                    _mouseHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _hookProc!, HINSTANCE.Null, 0);
                },
                onStopped: () =>
                {
                    var kbHookLocal = _kbHook;
                    var mouseHookLocal = _mouseHook;
                    _kbHook = HHOOK.Null;
                    _mouseHook = HHOOK.Null;

                    PInvoke.UnhookWindowsHookEx(kbHookLocal);
                    PInvoke.UnhookWindowsHookEx(mouseHookLocal);
                });

            if (!_window.Start(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("Failed to start the user-inactivity message window within the timeout.");
        }

        return Task.CompletedTask;
    });

    protected override Task StopAsync() => mainThreadDispatcher.DispatchAsync(() =>
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _tickCount = 0;

            _window?.Dispose();
            _window = null;
        }

        return Task.CompletedTask;
    });

    private static LRESULT DefaultWndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
        => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

    private LRESULT HookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        WindowCallback();
        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    private void WindowCallback()
    {
        lock (_lock)
        {
            _timer?.Change(_timerResolution, _timerResolution);

            if (_tickCount < 1)
                return;

            _tickCount = 0;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"User became active.");

            RaiseChanged(new ChangedEventArgs(_timerResolution, 0));
        }
    }

    private void TimerCallback(object? state)
    {
        try
        {
            lock (_lock)
            {
                _tickCount++;

                RaiseChanged(new ChangedEventArgs(_timerResolution, _tickCount));
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"TimerCallback failed", ex);
        }
    }
}
