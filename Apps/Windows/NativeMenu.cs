using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Windows;

internal static class NativeMenu
{
    internal static nint Build(IReadOnlyList<TrayMenuEntry> entries, Dictionary<nuint, string> commands)
    {
        var menu = Win32.CreatePopupMenu();
        if (menu == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            foreach (var entry in entries)
            {
                var flags = entry.Label.Length == 0 ? 0x0800u : entry.Enabled ? 0u : 0x0001u;
                if (entry.Checked) flags |= 0x0008;
                nuint id = 0;
                nint submenu = 0;
                if (entry.Children != null)
                {
                    submenu = Build(entry.Children, commands);
                    id = (nuint)submenu;
                    flags |= 0x0010;
                }
                else if (entry.Command != null)
                {
                    id = (nuint)commands.Count + 1;
                    commands.Add(id, entry.Command);
                }
                // Pipeline titles are literal text, not Windows menu accelerators.
                if (!Win32.AppendMenu(menu, flags, id, entry.Label.Replace("&", "&&")))
                {
                    if (submenu != 0) Win32.DestroyMenu(submenu);
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            return menu;
        }
        catch { Win32.DestroyMenu(menu); throw; }
    }

}
