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
    private readonly WindowMetrics _metrics;
    private readonly NativeAppIcon _icon = new();
    private bool _disposed;
    public nint Handle { get; private set; }
    internal nint SmallIcon => _icon.Small;
    public event Action? Resized;
    public event Action? Closing;
    public event Action<uint, nuint, nint>? MessageReceived;

    public NativeWindow(string statePath, Action<Exception> reportError, WindowMetrics metrics = default)
    {
        _statePath = statePath;
        _reportError = reportError;
        _metrics = metrics.DesignWidth <= 0 || metrics.DesignHeight <= 0 ? WindowMetrics.Application : metrics;
        _procedure = ProcessMessage;
        var windowClass = new Win32.WindowClass
        {
            Size = (uint)Marshal.SizeOf<Win32.WindowClass>(),
            Style = 0x0003, // CS_HREDRAW | CS_VREDRAW: repaint newly exposed resize areas.
            Procedure = _procedure,
            Instance = Win32.GetModuleHandle(null),
            Icon = _icon.Large,
            SmallIcon = _icon.Small,
            Cursor = Win32.LoadCursor(0, 32512),
            // Black is transparent to DWM glass; unlike a null brush it initializes
            // the backing surface instead of leaving stale pixels after resizing.
            Background = Win32.GetStockObject(4), // BLACK_BRUSH
            Name = $"UDT_WebView_{Guid.NewGuid():N}"
        };
        if (Win32.RegisterClassEx(ref windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        // A frameless resizable window. WebView2 app-region handles caption dragging.
        var saved = LoadBounds(statePath);
        var bounds = saved ?? new WindowPlacement(120, 100, _metrics.DesignWidth, _metrics.DesignHeight);
        Handle = Win32.CreateWindowEx(0, windowClass.Name, "Universal Device Toolkit", 0x800F0000,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0, 0, windowClass.Instance, 0);
        if (Handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        Win32.SendMessage(Handle, 0x0080, 1, _icon.Large); // WM_SETICON, ICON_BIG
        Win32.SendMessage(Handle, 0x0080, 0, _icon.Small); // WM_SETICON, ICON_SMALL
        var work = GetMonitor(Handle).Work;
        bounds = WindowPlacement.Fit(saved, work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top, Win32.GetDpiForWindow(Handle), _metrics);
        Win32.SetWindowPos(Handle, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0034);
        if (_metrics == WindowMetrics.Installer) PreferRoundedCorners();
    }

    /// <summary>
    /// Ask DWM for the same corner rounding a normal window gets.
    /// WM_NCCALCSIZE returning 0 removes the frame, and Windows then leaves the
    /// popup square. DWMWCP_ROUND (2) is the preference Electron applies when
    /// BrowserWindow roundedCorners is left at its default.
    /// </summary>
    private void PreferRoundedCorners()
    {
        var preference = 2; // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2
        Win32.DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
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
            // Remove the native non-client frame, including its visible top strip.
            if (message == 0x0083) return 0; // WM_NCCALCSIZE, for both RECT forms.
            // Keep activation and resizing, but do not let DefWindowProc paint
            // the legacy thick-frame rim over the full-client DWM material.
            if (message == 0x0085) return 0; // WM_NCPAINT
            if (message == 0x0086) return Win32.DefWindowProc(window, message, word, -1); // WM_NCACTIVATE
            if (message == 0x0084 && !Win32.IsZoomed(window)) // WM_NCHITTEST
            {
                var hit = HitTestResize(window, data);
                if (hit != 0) return hit;
            }
            if (message == 0x0024)
            {
                var sizing = Marshal.PtrToStructure<Win32.MinMaxInfo>(data);
                var monitor = GetMonitor(window);
                var width = monitor.Work.Right - monitor.Work.Left;
                var height = monitor.Work.Bottom - monitor.Work.Top;
                var scale = Math.Max(96, Win32.GetDpiForWindow(window)) / 96.0;
                sizing.MinTrackSize = new Win32.Point { X = Math.Min(width, (int)(_metrics.MinWidth * scale)), Y = Math.Min(height, (int)(_metrics.MinHeight * scale)) };
                sizing.MaxPosition = new Win32.Point { X = monitor.Work.Left - monitor.Monitor.Left, Y = monitor.Work.Top - monitor.Monitor.Top };
                sizing.MaxSize = new Win32.Point { X = width, Y = height };
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

    public void Hide() => Win32.ShowWindow(Handle, 0);

    internal bool SetBackdrop(int material)
    {
        var applied = Win32.DwmSetWindowAttribute(Handle, 38, ref material, sizeof(int)) >= 0 && material != 1;
        var extent = applied ? -1 : 0;
        var margins = new Win32.Margins { Left = extent, Right = extent, Top = extent, Bottom = extent };
        // Setting DWMWA_SYSTEMBACKDROP_TYPE alone affects only the native frame.
        // Extend the glass over the client area for the transparent WebView.
        return Win32.DwmExtendFrameIntoClientArea(Handle, ref margins) >= 0 && applied;
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
        _icon.Dispose();
        GC.KeepAlive(_procedure);
    }

    private void SaveBounds()
    {
        if (Handle == 0 || Win32.IsZoomed(Handle) || Win32.IsIconic(Handle) || !Win32.GetWindowRect(Handle, out var bounds)) return;
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var value = new WindowPlacement(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, Win32.GetDpiForWindow(Handle));
            File.WriteAllText(_statePath, JsonSerializer.Serialize(value), new System.Text.UTF8Encoding(false));
        }
        catch (IOException error) { _reportError(error); }
        catch (UnauthorizedAccessException error) { _reportError(error); }
    }

    private WindowPlacement? LoadBounds(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var state = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(path));
            if (state is { Width: >= 640, Height: >= 480 }) return state;
        }
        catch (JsonException error) { _reportError(error); }
        catch (IOException error) { _reportError(error); }
        catch (UnauthorizedAccessException error) { _reportError(error); }
        return null;
    }

    internal static Win32.MonitorInfo GetMonitor(nint window)
    {
        var monitor = new Win32.MonitorInfo { Size = (uint)Marshal.SizeOf<Win32.MonitorInfo>() };
        if (!Win32.GetMonitorInfo(Win32.MonitorFromWindow(window, 2), ref monitor))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return monitor;
    }

    private static nint HitTestResize(nint window, nint coordinates)
    {
        if (!Win32.GetWindowRect(window, out var bounds)) return 0;
        var x = (short)((long)coordinates & 0xffff);
        var y = (short)(((long)coordinates >> 16) & 0xffff);
        var border = (int)Math.Ceiling(6 * Win32.GetDpiForWindow(window) / 96.0);
        var left = x < bounds.Left + border;
        var right = x >= bounds.Right - border;
        var top = y < bounds.Top + border;
        var bottom = y >= bounds.Bottom - border;
        if (top) return left ? 13 : right ? 14 : 12;
        if (bottom) return left ? 16 : right ? 17 : 15;
        return left ? 10 : right ? 11 : 0;
    }
}
