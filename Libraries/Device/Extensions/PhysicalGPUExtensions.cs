using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class NVAPIExtensions
{
    private static readonly string[] Exclusions =
    [
        "dwm",
        "dwm.exe",
        "explorer",
        "explorer.exe",
    ];

    internal static List<Process> GetActiveProcesses(NvPhysicalGpuHandle gpu)
    {
        var processes = new List<Process>();
        var apps = NVAPI.GetActiveApps(gpu).Where(app =>
            !string.IsNullOrEmpty(app.ProcessName) &&
            !Exclusions.Contains(app.ProcessName, StringComparer.OrdinalIgnoreCase));

        foreach (var app in apps)
        {
            try
            {
                var process = Process.GetProcessById((int)app.ProcessId);
                processes.Add(process);
            }
            catch (Exception ex)
            {
                // Process may have exited or access denied, skip this process
                Log.Instance.TraceOnce(
                    "nvapi-active-process-gone",
                    "NVAPI active app process already exited or inaccessible while enumerating.",
                    ex);
            }
        }

        return processes;
    }
}
