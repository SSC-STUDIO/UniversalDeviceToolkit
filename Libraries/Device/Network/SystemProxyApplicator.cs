using System;
using System.IO;
using System.Runtime.InteropServices;
using UniversalDeviceToolkit.Lib.Utils;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>
/// Applies / clears Windows system proxy or PAC pointing at the loopback NetworkProxy worker.
/// Only call from an explicit user Start/Stop path — never on app launch.
/// </summary>
public static class SystemProxyApplicator
{
    private const int InternetOptionSettingsChanged = 39;
    private const int InternetOptionRefresh = 37;

    public static string PacDirectory => Folders.GetAppDataSubdirectory("network");

    public static string PacFilePath => Path.Combine(PacDirectory, "udt-network-acceleration.pac");

    public static SystemProxySnapshot CreatePacProxy(int port, string[]? proxiedDomains = null)
    {
        Directory.CreateDirectory(PacDirectory);
        var pac = PacFileGenerator.Generate(port, proxiedDomains);
        File.WriteAllText(PacFilePath, pac);

        // file:/// URL with forward slashes — WinINET accepts this for local PAC.
        var uri = new Uri(PacFilePath).AbsoluteUri;
        return new SystemProxySnapshot
        {
            Enabled = false,
            Server = string.Empty,
            Override = string.Empty,
            AutoConfigUrl = uri
        };
    }

    public static void Apply(SystemProxySnapshot snapshot)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
        key.SetValue("ProxyEnable", snapshot.Enabled ? 1 : 0);

        if (snapshot.Server is not null)
            key.SetValue("ProxyServer", snapshot.Server);

        if (snapshot.Override is not null)
            key.SetValue("ProxyOverride", snapshot.Override);

        if (string.IsNullOrEmpty(snapshot.AutoConfigUrl))
        {
            if (key.GetValue("AutoConfigURL") is not null)
                key.DeleteValue("AutoConfigURL", throwOnMissingValue: false);
        }
        else
        {
            key.SetValue("AutoConfigURL", snapshot.AutoConfigUrl);
        }

        NotifyWinInetChanged();
    }

    public static void NotifyWinInetChanged()
    {
        InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}
