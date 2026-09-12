using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.SoftwareDisabler;

public class FnKeysDisabler : AbstractSoftwareDisabler
{
    protected override IEnumerable<string> ScheduledTasksPaths => [];
    protected override IEnumerable<string> ServiceNames => ["LenovoFnAndFunctionKeys"];
    protected override IEnumerable<string> ProcessNames => ["LenovoUtilityUI", "LenovoUtilityService", "LenovoSmartKey"];

    public override async Task EnableAsync()
    {
        await base.EnableAsync().ConfigureAwait(false);
        SetUwpStartup("LenovoUtility", "LenovoUtilityID", true);
    }

    public override async Task DisableAsync()
    {
        await base.DisableAsync().ConfigureAwait(false);
        SetUwpStartup("LenovoUtility", "LenovoUtilityID", false);
    }

    protected override IEnumerable<string> RunningProcesses()
    {
        var result = base.RunningProcesses().ToList();

        try
        {
            foreach (var process in Process.GetProcessesByName("utility"))
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (description is null)
                    continue;

                if (description.Equals("Lenovo Hotkeys", StringComparison.OrdinalIgnoreCase))
                    result.Add(process.ProcessName);
            }
        }
        catch
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to get FnKeys processes");
        }

        return result;
    }

    protected override async Task KillProcessesAsync()
    {
        await base.KillProcessesAsync().ConfigureAwait(false);

        try
        {
            foreach (var process in Process.GetProcessesByName("utility"))
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (description is null)
                    continue;

                if (!description.Equals("Lenovo Hotkeys", StringComparison.OrdinalIgnoreCase))
                    continue;

                process.Kill(true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to kill FnKeys processes");
        }
    }

    private static void SetUwpStartup(string appPattern, string subKeyName, bool enabled)
    {
        const string hive = "HKEY_CURRENT_USER";
        const string subKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";
        const string valueName = "State";

        var startupKey = Registry.GetSubKeys(hive, subKey).FirstOrDefault(s => s.Contains(appPattern, StringComparison.CurrentCultureIgnoreCase));
        if (startupKey is null)
            return;

        startupKey = Path.Combine(startupKey, subKeyName);

        Registry.SetValue(hive, startupKey, valueName, enabled ? 0x2 : 0x1);
    }
}
