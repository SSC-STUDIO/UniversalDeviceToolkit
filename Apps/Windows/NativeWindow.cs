using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Win32 window and STA dispatcher; no Windows Desktop runtime is needed.</summary>
internal sealed class NativeWindow : SynchronizationContext, IDisposable
{
    private const uint DispatchMessageId = 0x8001;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _work = new();
    private readonly Win32.WindowProcedure _procedure;
    private readonly Action<Exception> _reportError;
    private bool _disposed;
    public nint Handle { get; private set; }
    public event Action? Resized;
    public event Action? Closing;
    public event Action<uint, nuint, nint>? MessageReceived;

    public NativeWindow(Action<Exception> reportError)
    {
        _reportError = reportError;
        _procedure = ProcessMessage;
        var windowClass = new Win32.WindowClass
        {
            Size = (uint)Marshal.SizeOf<Win32.WindowClass>(),
            Procedure = _procedure,
            Instance = Win32.GetModuleHandle(null),
            Cursor = Win32.LoadCursor(0, 32512),
            Name = $"UDT_WebView_{Guid.NewGuid():N}"
        };
        if (Win32.RegisterClassEx(ref windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        // A frameless resizable window. WebView2 app-region handles caption dragging.
        Handle = Win32.CreateWindowEx(0, windowClass.Name, "Universal Device Toolkit", 0x800F0000,
            120, 100, 1180, 780, 0, 0, windowClass.Instance, 0);
        if (Handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (_disposed) return;
        _work.Enqueue((callback, state));
        if (!Win32.PostMessage(Handle, DispatchMessageId, 0, 0)) _reportError(new Win32Exception(Marshal.GetLastWin32Error()));
    }

    private nint ProcessMessage(nint window, uint message, nuint word, nint data)
    {
        try
        {
            if (message == DispatchMessageId)
            {
                while (_work.TryDequeue(out var item)) item.Callback(item.State);
                return 0;
            }
            if (message == 0x0010) { Closing?.Invoke(); return 0; }
            if (message == 0x0002) { Win32.PostQuitMessage(0); return 0; }
            if (message == 0x0005) Resized?.Invoke();
            if (message == 0x0024)
            {
                var sizing = Marshal.PtrToStructure<Win32.MinMaxInfo>(data);
                sizing.MinTrackSize = new Win32.Point { X = 640, Y = 480 };
                Marshal.StructureToPtr(sizing, data, false);
                return 0;
            }
            if (message == 0x02E0)
            {
                var bounds = Marshal.PtrToStructure<Win32.Rect>(data);
                Win32.SetWindowPos(window, 0, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0014);
                Resized?.Invoke();
            }
            MessageReceived?.Invoke(message, word, data);
        }
        catch (Exception error) { _reportError(error); }
        return Win32.DefWindowProc(window, message, word, data);
    }

    public void Show()
    {
        Win32.ShowWindow(Handle, 9);
        Win32.SetForegroundWindow(Handle);
    }

    public static void Run()
    {
        int result;
        while ((result = Win32.GetMessage(out var message, 0, 0, 0)) > 0)
        {
            Win32.TranslateMessage(ref message);
            Win32.DispatchMessage(ref message);
        }
        if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Handle != 0) Win32.DestroyWindow(Handle);
        Handle = 0;
        GC.KeepAlive(_procedure);
    }
}
