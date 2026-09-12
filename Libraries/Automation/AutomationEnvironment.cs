using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalDeviceToolkit.Lib.Automation;

public class AutomationEnvironment
{
    // Primary keys are UDT_*; LLT_* aliases are dual-written for script compatibility.
    private const string AC_ADAPTER_CONNECTED = "UDT_IS_AC_ADAPTER_CONNECTED";
    private const string LOW_POWER_AC_ADAPTER = "UDT_IS_AC_ADAPTER_LOW_POWER";
    private const string DISPLAY_ON = "UDT_IS_DISPLAY_ON";
    private const string EXTERNAL_DISPLAY_CONNECTED = "UDT_IS_EXTERNAL_DISPLAY_CONNECTED";
    private const string GAME_RUNNING = "UDT_IS_GAME_RUNNING";
    private const string HDR_ON = "UDT_IS_HDR_ON";
    private const string LID_OPEN = "UDT_IS_LID_OPEN";
    private const string STARTUP = "UDT_STARTUP";
    private const string RESUME = "UDT_RESUME";
    private const string POWER_MODE = "UDT_POWER_MODE";
    private const string POWER_MODE_NAME = "UDT_POWER_MODE_NAME";
    private const string PROCESSES_STARTED = "UDT_PROCESSES_STARTED";
    private const string PROCESSES = "UDT_PROCESSES";
    private const string DEVICE_CONNECTED = "UDT_DEVICE_CONNECTED";
    private const string DEVICE_INSTANCE_IDS = "UDT_DEVICE_INSTANCE_IDS";
    private const string IS_SUNSET = "UDT_IS_SUNSET";
    private const string IS_SUNRISE = "UDT_IS_SUNRISE";
    private const string TIME = "UDT_TIME";
    private const string DAYS = "UDT_DAYS";
    private const string PERIOD = "UDT_PERIOD";
    private const string USER_ACTIVE = "UDT_IS_USER_ACTIVE";
    private const string WIFI_CONNECTED = "UDT_WIFI_CONNECTED";
    private const string WIFI_SSID = "UDT_WIFI_SSID";
    private const string SESSION_LOCKED = "UDT_SESSION_LOCKED";

    private const string VALUE_TRUE = "TRUE";
    private const string VALUE_FALSE = "FALSE";

    public bool AcAdapterConnected { set => Set(AC_ADAPTER_CONNECTED, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool LowPowerAcAdapter { set => Set(LOW_POWER_AC_ADAPTER, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool DisplayOn { set => Set(DISPLAY_ON, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool ExternalDisplayConnected { set => Set(EXTERNAL_DISPLAY_CONNECTED, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool GameRunning { set => Set(GAME_RUNNING, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool HDROn { set => Set(HDR_ON, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool LidOpen { set => Set(LID_OPEN, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool Startup { set => Set(STARTUP, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool Resume { set => Set(RESUME, value ? VALUE_TRUE : VALUE_FALSE); }

    public PowerModeState PowerMode
    {
        set
        {
            Set(POWER_MODE, value switch
            {
                PowerModeState.Quiet => "1",
                PowerModeState.Balance => "2",
                PowerModeState.Performance => "3",
                PowerModeState.GodMode => "255",
                _ => string.Empty
            });
            Set(POWER_MODE_NAME, value switch
            {
                PowerModeState.Quiet => "QUIET",
                PowerModeState.Balance => "BALANCE",
                PowerModeState.Performance => "PERFORMANCE",
                PowerModeState.GodMode => "CUSTOM",
                _ => string.Empty
            });
        }
    }

    public bool ProcessesStarted { set => Set(PROCESSES_STARTED, value ? VALUE_TRUE : VALUE_FALSE); }

    public ProcessInfo[] Processes { set => Set(PROCESSES, string.Join(",", value.Select(p => p.Name))); }

    public bool DeviceConnected { set => Set(DEVICE_CONNECTED, value ? VALUE_TRUE : VALUE_FALSE); }

    public string[] DeviceInstanceIds { set => Set(DEVICE_INSTANCE_IDS, string.Join(",", value)); }

    public bool IsSunset { set => Set(IS_SUNSET, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool IsSunrise { set => Set(IS_SUNRISE, value ? VALUE_TRUE : VALUE_FALSE); }

    public Time? Time { set => Set(TIME, value is null ? null : $"{value.Value.Hour}:{value.Value.Minute}"); }

    public DayOfWeek[] Days { set => Set(DAYS, value.Length < 1 ? null : string.Join(",", value.Select(v => v.ToString().ToUpperInvariant()))); }

    public TimeSpan Period { set => Set(PERIOD, $"{(int)value.TotalSeconds}"); }

    public bool UserActive { set => Set(USER_ACTIVE, value ? VALUE_TRUE : VALUE_FALSE); }

    public bool WiFiConnected { set => Set(WIFI_CONNECTED, value ? VALUE_TRUE : VALUE_FALSE); }

    public string? WiFiSsid { set => Set(WIFI_SSID, value); }

    public bool SessionLocked { set => Set(SESSION_LOCKED, value ? VALUE_TRUE : VALUE_FALSE); }

    public Dictionary<string, string?> Dictionary => new(_dictionary);

    private readonly Dictionary<string, string?> _dictionary = [];

    private static void Set(Dictionary<string, string?> dictionary, string primaryKey, string? value)
    {
        dictionary[primaryKey] = value;
        dictionary[ToLltAlias(primaryKey)] = value;
    }

    private void Set(string primaryKey, string? value) => Set(_dictionary, primaryKey, value);

    private static string ToLltAlias(string primaryKey) =>
        primaryKey.StartsWith("UDT_", StringComparison.Ordinal)
            ? "LLT_" + primaryKey[4..]
            : primaryKey;
}
