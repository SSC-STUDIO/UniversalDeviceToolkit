using System.Diagnostics;

namespace UniversalDeviceToolkit.Lib.System;

public static class AirplaneMode
{
    public static void Open()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "ms-settings:network-airplanemode",
            UseShellExecute = true,
            Verb = "open",
        });
    }
}
