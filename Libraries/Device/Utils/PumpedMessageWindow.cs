using System;
using System.Threading;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Hosts a Win32 message-only window on a dedicated message-pumping thread.
/// The headless host has no UI thread (IMainThreadDispatcher dispatches to the
/// thread pool, which never pumps messages), while low-level hooks and
/// device/power broadcast notifications require the creating thread to keep
/// pumping. Mirrors the GlobalInputHook pattern: install on the pump thread,
/// drive with GetMessage, tear down with a posted WM_QUIT.
/// </summary>
internal sealed class PumpedMessageWindow : IDisposable
{
    public delegate LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam);

    private const uint WM_QUIT = 0x0012;
    private static int s_windowClassCounter;

    private readonly string _windowName;
    private readonly WndProc _wndProc;
    private readonly Action<HWND>? _onStarted;
    private readonly Action? _onStopped;
    private readonly string _className;

    private readonly object _sync = new();
    private readonly ManualResetEventSlim _started = new(false);
    private Thread? _thread;
    private uint _threadId;
    private HWND _hwnd;
    private bool _startSucceeded;
    private bool _disposed;

    /// <param name="windowName">Window caption used for diagnostics.</param>
    /// <param name="wndProc">Window procedure invoked on the pump thread.</param>
    /// <param name="onStarted">
    /// Runs on the pump thread right after the window exists; install hooks and
    /// RegisterDeviceNotification/RegisterPowerSettingNotification handles here.
    /// </param>
    /// <param name="onStopped">
    /// Runs on the pump thread after the message loop exits; unhook and
    /// unregister notification handles here.
    /// </param>
    public PumpedMessageWindow(string windowName, WndProc wndProc, Action<HWND>? onStarted = null, Action? onStopped = null)
    {
        _windowName = windowName;
        _wndProc = wndProc;
        _onStarted = onStarted;
        _onStopped = onStopped;
        // One class per instance keeps lpfnWndProc bound to this window without
        // cross-instance routing; two windows per process cost nothing.
        _className = $"UniversalDeviceToolkit_MessageWindow_{Interlocked.Increment(ref s_windowClassCounter)}";
    }

    /// <summary>Window handle; valid only after <see cref="Start"/> returned true.</summary>
    public HWND Hwnd
    {
        get
        {
            lock (_sync)
                return _hwnd;
        }
    }

    /// <summary>
    /// Starts the pump thread, creates the message-only window and runs
    /// <c>onStarted</c>. Returns false when startup did not complete within
    /// <paramref name="timeout"/> or the window creation failed.
    /// </summary>
    public bool Start(TimeSpan timeout)
    {
        lock (_sync)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PumpedMessageWindow));
            if (_thread is { IsAlive: true })
                return false;

            _startSucceeded = false;
            _started.Reset();
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = _windowName,
            };
            _thread.Start();
        }

        if (!_started.Wait(timeout))
        {
            StopThread(TimeSpan.FromSeconds(2));
            return false;
        }

        return _startSucceeded;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        StopThread(TimeSpan.FromSeconds(3));
        _started.Dispose();
    }

    private void Run()
    {
        _threadId = PInvoke.GetCurrentThreadId();

        try
        {
            _hwnd = CreateMessageOnlyWindow();
            _startSucceeded = !_hwnd.IsNull;
            if (_startSucceeded)
                _onStarted?.Invoke(_hwnd);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"PumpedMessageWindow '{_windowName}' failed to start: {ex.Message}", ex);
            _startSucceeded = false;
        }
        finally
        {
            _started.Set();
        }

        if (!_startSucceeded)
        {
            RunStopped();
            return;
        }

        // Low-level hook callbacks and broadcast notifications only arrive
        // while this thread pumps messages.
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            _ = PInvoke.TranslateMessage(in msg);
            _ = PInvoke.DispatchMessage(in msg);
        }

        RunStopped();
    }

    private void RunStopped()
    {
        try
        {
            if (!_hwnd.IsNull)
            {
                _ = PInvoke.DestroyWindow(_hwnd);
                _hwnd = default;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"PumpedMessageWindow '{_windowName}' failed to destroy window: {ex.Message}", ex);
        }

        try
        {
            _onStopped?.Invoke();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"PumpedMessageWindow '{_windowName}' stop callback failed: {ex.Message}", ex);
        }
    }

    private unsafe HWND CreateMessageOnlyWindow()
    {
        var module = PInvoke.GetModuleHandle(null);
        var instance = new HINSTANCE(module.DangerousGetHandle());
        var wndProc = new WNDPROC(WindowProc);
        // Root the delegate for the lifetime of the registered class; the class
        // is unregistered only after the window is destroyed on the same thread.
        _wndProcRoot = wndProc;

        fixed (char* classNamePtr = _className)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)global::System.Runtime.InteropServices.Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = wndProc,
                hInstance = instance,
                lpszClassName = new PCWSTR(classNamePtr),
            };

            var atom = PInvoke.RegisterClassEx(in windowClass);
            if (atom == 0)
                PInvokeExtensions.ThrowIfWin32Error($"Failed to register window class '{_className}'.");

            var hwnd = PInvoke.CreateWindowEx(
                0,
                _className,
                _windowName,
                0,
                0, 0, 0, 0,
                (HWND)(-3), // HWND_MESSAGE: message-only window, never visible.
                null,
                module,
                null);

            if (hwnd.IsNull)
                PInvokeExtensions.ThrowIfWin32Error($"Failed to create message-only window '{_windowName}'.");

            return hwnd;
        }
    }

    private LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            return _wndProc(hwnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"PumpedMessageWindow '{_windowName}' wndproc failed for msg {msg}: {ex.Message}", ex);
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }

    private void StopThread(TimeSpan joinTimeout)
    {
        Thread? thread;
        uint threadId;
        lock (_sync)
        {
            thread = _thread;
            threadId = _threadId;
            _thread = null;
        }

        if (thread is null || !thread.IsAlive)
            return;

        if (threadId != 0)
            _ = PInvoke.PostThreadMessage(threadId, WM_QUIT, default, default);

        if (Thread.CurrentThread != thread)
            _ = thread.Join(joinTimeout);
    }

    private WNDPROC? _wndProcRoot;
}
