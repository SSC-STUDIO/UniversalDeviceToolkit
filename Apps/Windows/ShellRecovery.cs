using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

internal static class ShellRecovery
{
    private const string RuntimeUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";
    private const string ReleasesUrl = "https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/latest";
    internal static string Version => Assembly.GetExecutingAssembly().GetName().Version is { } version
        ? $"{version.Major}.{version.Minor}.{version.Build}" : "unknown";

    internal static bool IsBrowserFailure(Exception error) => error is WebView2RuntimeNotFoundException or COMException;
    private static uint DirectionFlags => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? 0x180000U : 0;

    internal static void Show(Exception error, nint owner = 0)
    {
        var language = CultureInfo.CurrentUICulture.Name;
        if (Win32.MessageBox(owner, ShellStrings.Get(language, "runtimeRecovery") + "\n\n" + error.Message,
            "Universal Device Toolkit", 0x34 | DirectionFlags) == 6) Open(RuntimeUrl);
        else if (Win32.MessageBox(owner, ShellStrings.Get(language, "compatibilityDownload"), "Universal Device Toolkit", 0x24 | DirectionFlags) == 6)
            Open(ReleasesUrl);
    }

    internal static bool Retry(nint owner, string detail) => Win32.MessageBox(owner,
        ShellStrings.Get(CultureInfo.CurrentUICulture.Name, "interfaceRetry") + "\n\n" + detail,
        "Universal Device Toolkit", 0x35 | DirectionFlags) == 4;

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) { Win32.MessageBox(0, error.Message + "\n" + url, "Universal Device Toolkit", 0x10); }
    }
}
