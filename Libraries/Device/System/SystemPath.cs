using System;
using System.Linq;
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.System;

public static class SystemPath
{
    private static string CLIPath => Folders.Program;

    public static bool HasCLI()
    {
        return Registry.GetValue("HKEY_CURRENT_USER", "Environment", "PATH", string.Empty, true)
            .Split(';')
            .Contains(CLIPath);
    }

    public static void SetCLI(bool enabled)
    {
        var value = Registry.GetValue("HKEY_CURRENT_USER", "Environment", "PATH", string.Empty, true)
            .Split(';')
            .ToList();

        if (enabled)
        {
            if (value.Contains(CLIPath))
                return;

            value.Add(CLIPath);
        }
        else
        {
            value.Remove(CLIPath);
        }

        Registry.SetValue("HKEY_CURRENT_USER",
            "Environment",
            "PATH",
            string.Join(';', value),
            valueKind: RegistryValueKind.ExpandString);

        Notify();
    }

    /// <summary>
    /// Broadcast WM_SETTINGCHANGE so other processes pick up PATH updates.
    /// Uses SendMessageTimeout (synchronous) so the string remains valid for the full call;
    /// SendNotifyMessage is async and would race a fixed/local string deallocation.
    /// </summary>
    private static void Notify()
    {
        const uint HWND_BROADCAST = 0xFFFF;
        const uint WM_SETTINGCHANGE = 0x001A;
        const uint SMTO_ABORTIFHUNG = 0x0002;

        _ = SendMessageTimeout(
            new IntPtr(unchecked((int)HWND_BROADCAST)),
            WM_SETTINGCHANGE,
            UIntPtr.Zero,
            "Environment",
            SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);
}
