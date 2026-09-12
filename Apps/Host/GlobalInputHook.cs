using System;
using System.Runtime.InteropServices;
using System.Threading;
using UniversalDeviceToolkit.Lib.Macro;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Dedicated message-pumping thread that hosts the Lib macro recorder's global
/// WH_KEYBOARD_LL / WH_MOUSE_LL hooks.
///
/// Low-level hooks only deliver callbacks to the installing thread while that
/// thread pumps messages, and the headless host has no UI thread. This class
/// gives each recording session a private pump thread: the hooks are installed
/// on it, a GetMessage loop drives them, and a posted WM_QUIT tears the session
/// down. Captured events flow out through MacroController.RecorderReceived.
/// </summary>
public sealed class GlobalInputHook : IDisposable
{
    private const uint WM_QUIT = 0x0012;

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

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMsg lpMsg);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static extern IntPtr DispatchMessage(ref NativeMsg lpMsg);

    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private readonly MacroController _controller;
    private readonly MacroRecorderSettings _settings;
    private readonly object _sync = new();
    private readonly ManualResetEventSlim _installedEvent = new(false);
    private Thread? _thread;
    private uint _threadId;
    private bool _installed;
    private bool _disposed;

    public GlobalInputHook(MacroController controller, MacroRecorderSettings settings)
    {
        _controller = controller;
        _settings = settings;
    }

    /// <summary>Gets whether the hook thread is currently alive.</summary>
    public bool IsActive
    {
        get
        {
            lock (_sync)
                return _thread is { IsAlive: true };
        }
    }

    /// <summary>
    /// Starts the pump thread and installs the recorder hooks on it. Returns
    /// true when the hooks were successfully installed.
    /// </summary>
    public bool Start()
    {
        lock (_sync)
        {
            if (_thread is { IsAlive: true })
                return false;

            _thread = new Thread(HookThread)
            {
                IsBackground = true,
                Name = "MacroRecordingHook",
            };
            _thread.Start();
        }

        if (!_installedEvent.Wait(TimeSpan.FromSeconds(5)))
        {
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _thread.Join(TimeSpan.FromSeconds(2));
            return false;
        }

        return _installed;
    }

    /// <summary>
    /// Stops the recording session: posts WM_QUIT to the pump thread and joins
    /// it. Safe to call from any thread except the pump thread itself (the ESC
    /// auto-stop path), in which case only the quit message is posted.
    /// </summary>
    public void Stop()
    {
        Thread? thread;
        lock (_sync)
        {
            thread = _thread;
        }

        if (thread is null || !thread.IsAlive)
            return;

        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);

        if (Thread.CurrentThread != thread)
            thread.Join(TimeSpan.FromSeconds(3));
    }

    private void HookThread()
    {
        _threadId = GetCurrentThreadId();

        try
        {
            // Install the low-level hooks on THIS thread; the GetMessage loop
            // below is what keeps their callbacks flowing.
            _controller.StartRecording(_settings);
            _installed = _controller.IsRecording;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Global input hook install failed: {ex.Message}", ex);
            _installed = false;
        }

        _installedEvent.Set();

        if (!_installed)
            return;

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        try
        {
            // Unhook on the installing thread. No-op when the recorder already
            // stopped itself (ESC interrupt).
            _controller.StopRecording();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Global input hook teardown failed: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Stop();
        _installedEvent.Dispose();
    }
}
