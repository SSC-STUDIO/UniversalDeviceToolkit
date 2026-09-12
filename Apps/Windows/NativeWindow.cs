using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Win32 window and STA dispatcher; no Windows Desktop runtime is needed.</summary>
internal sealed class NativeWindow : SynchronizationContext, IDisposable
{
    private const uint DispatchMessageId = 0x8001;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _work = new();
    private readonly Win32.WindowProcedure _procedure;
    private readonly Action<Exception> _reportError;
    private readonly string _statePath;
    private bool _disposed;
    public nint Handle { get; private set; }
    public event Action? Resized;
    public event Action? Closing;
    public event Action<uint, nuint, nint>? MessageReceived;

    public NativeWindow(string statePath, Action<Exception> reportError)
    {
        _statePath = statePath;
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
        var bounds = LoadBounds(statePath);
        Handle = Win32.CreateWindowEx(0, windowClass.Name, "Universal Device Toolkit", 0x800F0000,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0, 0, windowClass.Instance, 0);
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
            if (message == 0x0232) SaveBounds();
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
        if (Handle != 0)
        {
            SaveBounds();
            Win32.DestroyWindow(Handle);
        }
        Handle = 0;
        GC.KeepAlive(_procedure);
    }

    private void SaveBounds()
    {
        if (Handle == 0 || Win32.IsZoomed(Handle) || !Win32.GetWindowRect(Handle, out var bounds)) return;
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var value = new WindowState(bounds.Left, bounds.Top, Math.Max(640, bounds.Right - bounds.Left), Math.Max(480, bounds.Bottom - bounds.Top));
            File.WriteAllText(_statePath, JsonSerializer.Serialize(value), new System.Text.UTF8Encoding(false));
        }
        catch (IOException error) { _reportError(error); }
        catch (UnauthorizedAccessException error) { _reportError(error); }
    }

    private static WindowState LoadBounds(string path)
    {
        try
        {
            var state = JsonSerializer.Deserialize<WindowState>(File.ReadAllText(path));
            if (state is { Width: >= 640, Height: >= 480 }) return state;
        }
        catch (JsonException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return new WindowState(120, 100, 1180, 780);
    }

    private sealed record WindowState(int Left, int Top, int Width, int Height);
}
