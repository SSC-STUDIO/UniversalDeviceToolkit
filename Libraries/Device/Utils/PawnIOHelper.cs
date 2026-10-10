// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Settings;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Utils;

public static class PawnIOHelper
{
    private static ApplicationSettings? _applicationSettings;
    private static ApplicationSettings ApplicationSettings => _applicationSettings ??= IoCContainer.Resolve<ApplicationSettings>();

    private const string REG_KEY_PAWN_IO = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";
    private const string REG_VAL_INSTALL_LOC = "InstallLocation";
    private const string REG_KEY_PAWN_IO_WOW64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\PawnIO";
    private const string REG_VAL_INSTALL_DIR = "Install_Dir";
    private const string FOLDER_PAWN_IO = "PawnIO";

    private static readonly object _delegateLock = new();
    private static Func<Task<bool>>? _requestShowDialogAsync;

    public static Func<Task<bool>>? RequestShowDialogAsync
    {
        get
        {
            lock (_delegateLock)
                return _requestShowDialogAsync;
        }
        set
        {
            lock (_delegateLock)
                _requestShowDialogAsync = value;
        }
    }

    public static void OpenPawnIODownloadPage()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "https://pawnio.eu/",
            UseShellExecute = true
        });
    }

    public static async Task TryShowPawnIONotFoundDialogAsync(bool disableHardwareSensors = true)
    {
        if (RequestShowDialogAsync != null)
        {
            bool userClickedYes = await RequestShowDialogAsync.Invoke().ConfigureAwait(false);

            if (userClickedYes)
            {
                OpenPawnIODownloadPage();
                return;
            }

            if (disableHardwareSensors)
            {
                ApplicationSettings.Store.EnableHardwareSensors = false;
                ApplicationSettings.SynchronizeStore();
            }
        }
    }

    public static void ShowPawnIONotify()
    {
        TryShowPawnIONotFoundDialogAsync().ConfigureAwait(false);
    }

    public static bool IsPawnIOInstalled()
    {
        string? path = Registry.GetValue(REG_KEY_PAWN_IO, REG_VAL_INSTALL_LOC, null) as string
                       ?? Registry.GetValue(REG_KEY_PAWN_IO_WOW64, REG_VAL_INSTALL_DIR, null) as string
                       ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), FOLDER_PAWN_IO);
        return Directory.Exists(path);
    }
}