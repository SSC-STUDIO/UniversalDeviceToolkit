using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Small Win32 tray icon used by the WebView2 shell.</summary>
internal sealed class NativeTray : IDisposable
{
    private const uint NotifyAdd = 0;
    private const uint NotifyModify = 1;
    private const uint NotifyDelete = 2;
    private const uint NotifyMessage = 0x8002;
    private const uint IconMessage = 0x0200;
    private const uint LeftButtonUp = 0x0202;
    private const uint RightButtonUp = 0x0205;
    private const uint IconFlagMessage = 1;
    private const uint IconFlagIcon = 2;
    private const uint IconFlagTip = 4;
    private const uint MenuString = 0;
    private const uint MenuRightButton = 0x0002;
    private const uint MenuBottomAlign = 0x0008;
    private const uint MenuReturnCommand = 0x0100;
    private const nuint OpenCommand = 1;
    private const nuint QuitCommand = 2;

    private readonly NativeWindow _window;
    private readonly Action _restore;
    private readonly Action _quit;
    private readonly Action<string> _log;
    private Win32.NotifyIconData _data;
    private bool _visible;
    private string _openLabel;
    private string _quitLabel;

    public NativeTray(NativeWindow window, string language, Action restore, Action quit, Action<string> log)
    {
        _window = window;
        _restore = restore;
        _quit = quit;
        _log = log;
        (_openLabel, _quitLabel) = Labels(language);
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

    public void SetLanguage(string language)
    {
        (_openLabel, _quitLabel) = Labels(language);
        if (!_visible) return;
        _data.Flags = IconFlagTip;
        _data.Tip = "Universal Device Toolkit";
        Win32.Shell_NotifyIcon(NotifyModify, ref _data);
    }

    private void OnWindowMessage(uint message, nuint word, nint data)
    {
        if (message != NotifyMessage || word != _data.Id) return;
        var mouseMessage = unchecked((uint)data.ToInt64());
        if (mouseMessage == LeftButtonUp) _restore();
        else if (mouseMessage == RightButtonUp) ShowMenu();
    }

    private void ShowMenu()
    {
        if (!Win32.GetCursorPos(out var point)) return;
        var menu = Win32.CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            if (!Win32.AppendMenu(menu, MenuString, OpenCommand, _openLabel)
                || !Win32.AppendMenu(menu, MenuString, QuitCommand, _quitLabel))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create the tray menu.");
            Win32.SetForegroundWindow(_window.Handle);
            // Without TPM_RETURNCMD the result is only success/failure (1/0),
            // which makes every selection look like OpenCommand.
            var command = Win32.TrackPopupMenu(menu, MenuRightButton | MenuBottomAlign | MenuReturnCommand, point.X, point.Y, 0, _window.Handle, 0);
            if (command == OpenCommand) _restore();
            else if (command == QuitCommand) _quit();
        }
        catch (Exception error) { _log(error.ToString()); }
        finally { Win32.DestroyMenu(menu); }
    }

    public void Dispose()
    {
        _window.MessageReceived -= OnWindowMessage;
        if (!_visible) return;
        _visible = false;
        Win32.Shell_NotifyIcon(NotifyDelete, ref _data);
    }

    private static (string Open, string Quit) Labels(string language)
    {
        return language.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
            ? ("開啟", "退出")
            : language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? ("打开", "退出")
                : ("Open", "Quit");
    }
}
