using System;
using System.Runtime.InteropServices;
using System.Threading;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace UniversalDeviceToolkit.Lib.Macro.Utils;

/// <summary>
/// Dedicated thread that owns a low-level input hook and pumps messages for
/// its lifetime. WH_KEYBOARD_LL / WH_MOUSE_LL callbacks are delivered only
/// while the installing thread runs a GetMessage loop; a thread-pool
/// <c>Task.Run</c> has no pump, so the hook appears installed but never fires.
/// </summary>
internal sealed class MacroHookPump : IDisposable
{
    private const uint WM_QUIT = 0x0012;
    private const uint PmNoremove = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMsg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    private readonly Func<bool> _install;
    private readonly Action _uninstall;
    private readonly string _threadName;
    private readonly object _sync = new();
    private readonly ManualResetEventSlim _installedEvent = new(false);

    private Thread? _thread;
    private uint _threadId;
    private volatile bool _installed;
    private bool _disposed;

    public MacroHookPump(string threadName, Func<bool> install, Action uninstall)
    {
        ArgumentNullException.ThrowIfNull(threadName);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(uninstall);
        _threadName = threadName;
        _install = install;
        _uninstall = uninstall;
    }

    public bool IsActive
    {
        get
        {
            lock (_sync)
                return _installed && _thread is { IsAlive: true };
        }
    }

    /// <summary>
    /// Starts the pump thread, installs the hook on it, and waits until the
    /// install result is known. Returns false on timeout or install failure.
    /// </summary>
    public bool Start(TimeSpan timeout)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_thread is { IsAlive: true })
                return _installed;

            _installed = false;
            _installedEvent.Reset();
            _thread = new Thread(HookThread)
            {
                IsBackground = true,
                Name = _threadName,
            };
            _thread.Start();
        }

        if (!_installedEvent.Wait(timeout))
        {
            PostQuit();
            JoinThread(TimeSpan.FromSeconds(2));
            return false;
        }

        return _installed;
    }

    public void Stop()
    {
        PostQuit();
        JoinThread(TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Stop();
        _installedEvent.Dispose();
    }

    private void HookThread()
    {
        _threadId = PInvoke.GetCurrentThreadId();

        try
        {
            // Create the thread message queue before install/signal so Stop()
            // and the start-timeout path can PostThreadMessage(WM_QUIT).
            _ = PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoremove);
            _installed = _install();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Macro hook pump '{_threadName}' install failed: {ex.Message}", ex);
            _installed = false;
        }
        finally
        {
            _installedEvent.Set();
        }

        if (!_installed)
            return;

        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0) > 0)
        {
            _ = PInvoke.TranslateMessage(in msg);
            _ = PInvoke.DispatchMessage(in msg);
        }

        try
        {
            _uninstall();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Macro hook pump '{_threadName}' uninstall failed: {ex.Message}", ex);
        }
        finally
        {
            _installed = false;
        }
    }

    private void PostQuit()
    {
        var threadId = _threadId;
        if (threadId == 0)
            return;

        _ = PInvoke.PostThreadMessage(threadId, WM_QUIT, default, default);
    }

    private void JoinThread(TimeSpan joinTimeout)
    {
        Thread? thread;
        lock (_sync)
        {
            thread = _thread;
            _thread = null;
        }

        if (thread is null || !thread.IsAlive)
            return;

        if (Thread.CurrentThread != thread)
            _ = thread.Join(joinTimeout);
    }
}
