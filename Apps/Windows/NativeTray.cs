using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Small Win32 tray icon used by the WebView2 shell.</summary>
internal sealed class NativeTray : IDisposable
{
    private const uint NotifyAdd = 0;
    private const uint NotifyDelete = 2;
    private const uint NotifyMessage = 0x8002;
    private const uint LeftButtonUp = 0x0202;
    private const uint RightButtonUp = 0x0205;
    private const uint IconFlagMessage = 1;
    private const uint IconFlagIcon = 2;
    private const uint IconFlagTip = 4;
    private const uint MenuRightButton = 0x0002;
    private const uint MenuBottomAlign = 0x0008;
    private const uint MenuReturnCommand = 0x0100;

    private readonly NativeWindow _window;
    private readonly Action<string?> _restore;
    private readonly Action _quit;
    private readonly Action<string> _log;
    private readonly TrayMenu _menu;
    private readonly CancellationTokenSource _lifetime = new();
    private Win32.NotifyIconData _data;
    private bool _visible;
    private string _language;
    private bool _showing;

    public NativeTray(NativeWindow window, string language, TrayMenu menu, Action<string?> restore, Action quit, Action<string> log)
    {
        _window = window;
        _restore = restore;
        _quit = quit;
        _log = log;
        _language = language;
        _menu = menu;
        _data = new Win32.NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<Win32.NotifyIconData>(),
            Window = window.Handle,
            Id = 1,
            Flags = IconFlagMessage | IconFlagIcon | IconFlagTip,
            CallbackMessage = NotifyMessage,
            Icon = window.SmallIcon,
            Tip = "Universal Device Toolkit",
            Info = string.Empty,
            InfoTitle = string.Empty
        };
        _window.MessageReceived += OnWindowMessage;
        if (!Win32.Shell_NotifyIcon(NotifyAdd, ref _data))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create the system tray icon.");
        _visible = true;
    }

    public void SetLanguage(string language) => _language = language;

    private void OnWindowMessage(uint message, nuint word, nint data)
    {
        if (message != NotifyMessage || word != _data.Id) return;
        var mouseMessage = unchecked((uint)data.ToInt64());
        if (mouseMessage == LeftButtonUp) _restore(null);
        else if (mouseMessage == RightButtonUp) _ = ShowMenuAsync();
    }

    private async Task ShowMenuAsync()
    {
        if (_showing || !_visible || !Win32.GetCursorPos(out var point)) return;
        _showing = true;
        nint menu = 0;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            var entries = await _menu.LoadAsync(_language, timeout.Token);
            if (!_visible) return;
            var commands = new Dictionary<nuint, string>();
            menu = NativeMenu.Build(entries, commands);
            Win32.SetForegroundWindow(_window.Handle);
            var command = Win32.TrackPopupMenu(menu, MenuRightButton | MenuBottomAlign | MenuReturnCommand, point.X, point.Y, 0, _window.Handle, 0);
            // Finish native menu dismissal before dispatching a route or RPC.
            Win32.PostMessage(_window.Handle, 0, 0, 0);
            if (commands.TryGetValue(command, out var action))
                await _menu.ExecuteAsync(action, _restore, _quit, _lifetime.Token);
        }
        catch (OperationCanceledException error) { _log(error.Message); }
        catch (Exception error)
        {
            _log(error.ToString());
            if (_visible) Win32.MessageBox(_window.Handle, error.Message, "Universal Device Toolkit", 0x10);
        }
        finally
        {
            if (menu != 0) Win32.DestroyMenu(menu);
            _showing = false;
        }
    }

    public void Dispose()
    {
        _window.MessageReceived -= OnWindowMessage;
        if (!_visible) return;
        _visible = false;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Win32.Shell_NotifyIcon(NotifyDelete, ref _data);
    }
}
