using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Lib.Optimization;

public static class WindowsOptimizationDefinitions
{
    private static readonly string LocalAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string RoamingAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string ProgramDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static readonly string WindowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static readonly string SystemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    private static readonly string TempFolderPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static RegistryValueDefinition Reg(string hive, string subKey, string valueName, object value, RegistryValueKind kind)
        => new(hive, subKey, valueName, value, kind);

    private static string Quote(string value) => $"\"{value}\"";

    private static string Del(string pathPattern) => $"del /f /s /q {Quote(pathPattern)}";

    private static string DelFile(string pathPattern) => $"del /f /q {Quote(pathPattern)}";

    private static string Rd(string pathPattern) => $"rd /s /q {Quote(pathPattern)}";

    public static readonly IReadOnlyList<RegistryValueDefinition> ExplorerTaskbarTweaks =
    [
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer", "EnableAutoTray", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarGlomLevel", 2, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> StartMenuDisableTweaks =
    [
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 1, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoStartMenuPinnedList", 1, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoStartMenuMorePrograms", 1, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoStartMenuMFUprogramsList", 1, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> ExplorerResponsivenessTweaks =
    [
        Reg("HKEY_CURRENT_USER", @"Control Panel\Desktop", "MenuShowDelay", "0", RegistryValueKind.String),
        Reg("HKEY_CURRENT_USER", @"Control Panel\Desktop", "AutoEndTasks", "1", RegistryValueKind.String),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", 1, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> ExplorerVisibilityTweaks =
    [
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 1, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> ExplorerSuggestionsTweaks =
    [
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> TelemetryTweaks =
    [
        Reg("HKEY_CURRENT_USER", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", 1, RegistryValueKind.DWord),
        Reg("HKEY_CURRENT_USER", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableSuggestionsWindowsTips", 1, RegistryValueKind.DWord),
        Reg("HKEY_LOCAL_MACHINE", @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> MultimediaTweaks =
    [
        Reg("HKEY_LOCAL_MACHINE", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord),
        Reg("HKEY_LOCAL_MACHINE", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> MemoryTweaks =
    [
        Reg("HKEY_LOCAL_MACHINE", @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", 1, RegistryValueKind.DWord),
        Reg("HKEY_LOCAL_MACHINE", @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<RegistryValueDefinition> NotificationTweaks =
    [
        Reg("HKEY_LOCAL_MACHINE", @"SOFTWARE\Policies\Microsoft\Windows\System", "DisableAcrylicBackgroundOnLogon", 1, RegistryValueKind.DWord),
        Reg("HKEY_LOCAL_MACHINE", @"SOFTWARE\Policies\Microsoft\Windows\Explorer", "DisableNotificationCenter", 1, RegistryValueKind.DWord)
    ];

    public static readonly IReadOnlyList<string> DiagnosticsServices = ["DiagTrack", "diagnosticshub.standardcollector.service", "DoSvc"];
    public static readonly IReadOnlyList<string> SysMainService = ["SysMain"];
    public static readonly IReadOnlyList<string> SearchService = ["WSearch"];
    public static readonly IReadOnlyList<string> RemoteRegistryService = ["RemoteRegistry"];
    public static readonly IReadOnlyList<string> ErrorReportingService = ["WerSvc"];

    public static readonly IReadOnlyList<string> RemoteDesktopCacheCommands =
    [
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Terminal Server Client", "Cache", "*"))
    ];

    public static readonly IReadOnlyList<string> WindowsUpdateCacheCommands =
    [
        Del(Path.Combine(WindowsPath, "SoftwareDistribution", "Download", "*")),
        Del(Path.Combine(WindowsPath, "SoftwareDistribution", "DeliveryOptimization", "*"))
    ];

    public static readonly IReadOnlyList<string> BrowserCacheCommands =
    [
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Windows", "INetCache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Windows", "INetCookies", "*")),
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Edge", "User Data", "Default", "Cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Edge", "User Data", "Default", "Code Cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Google", "Chrome", "User Data", "Default", "Cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Google", "Chrome", "User Data", "Default", "Code Cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Mozilla", "Firefox", "Profiles", "*", "cache2", "*"))
    ];

    public static readonly IReadOnlyList<string> AppLeftoverCommands =
    [
        Del(Path.Combine(LocalAppDataPath, "Temp", "*")),
        Del(Path.Combine(Path.GetDirectoryName(RoamingAppDataPath) ?? LocalAppDataPath, "Local", "Temp", "*")),
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Windows", "WER", "*")),
        Del(Path.Combine(ProgramDataPath, "Microsoft", "Windows", "WER", "*"))
    ];

    public static readonly IReadOnlyList<string> ThumbnailCacheCommands =
    [
        Del(Path.Combine(LocalAppDataPath, "Microsoft", "Windows", "Explorer", "thumbcache_*.db")),
        Del(Path.Combine(LocalAppDataPath, "Local", "D3DSCache", "*"))
    ];

    public static readonly IReadOnlyList<string> DotnetNativeImageCommands =
    [
        Rd(Path.Combine(WindowsPath, "assembly", "NativeImages_v4.0.30319_32")),
        Rd(Path.Combine(WindowsPath, "assembly", "NativeImages_v4.0.30319_64"))
    ];

    public static readonly IReadOnlyList<string> SystemLogCommands =
    [
        Del(Path.Combine(WindowsPath, "Logs", "*")),
        Del(Path.Combine(ProgramDataPath, "Microsoft", "Windows", "WER", "ReportQueue", "*")),
        Del(Path.Combine(ProgramDataPath, "Microsoft", "Diagnosis", "*"))
    ];

    public static readonly IReadOnlyList<string> CrashDumpCommands =
    [
        Del(Path.Combine(WindowsPath, "Minidump", "*.dmp")),
        DelFile(Path.Combine(WindowsPath, "memory.dmp")),
        Del(Path.Combine(SystemDrivePath, "*.dmp"))
    ];

    public static readonly IReadOnlyList<string> DefenderCommands =
    [
        Del(Path.Combine(ProgramDataPath, "Microsoft", "Windows Defender", "Scans", "*"))
    ];

    public static readonly IReadOnlyList<string> TempCommands =
    [
        Del(Path.Combine(WindowsPath, "Temp", "*")),
        Del(Path.Combine(SystemDrivePath, "Windows", "Temp", "*")),
        Del(Path.Combine(TempFolderPath, "*"))
    ];

    public static readonly IReadOnlyList<string> RecycleBinCommands =
    [
        Rd(Path.Combine(SystemDrivePath, "$Recycle.bin"))
    ];

    public static readonly IReadOnlyList<string> PrefetchCommands =
    [
        Del(Path.Combine(WindowsPath, "Prefetch", "*"))
    ];

    public static readonly IReadOnlyList<string> PowerPlanCommands =
    [
        "powercfg -setactive SCHEME_MAX",
        "powercfg -h off"
    ];

    public static readonly IReadOnlyList<string> ComponentStoreCommands =
    [
        "dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase",
        Del(Path.Combine(WindowsPath, "WinSxS", "Temp", "*"))
    ];

    public static readonly IReadOnlyList<string> ShaderCacheCommands =
    [
        Del(Path.Combine(LocalAppDataPath, "D3DSCache", "*")),
        Del(Path.Combine(LocalAppDataPath, "NVIDIA", "DXCache", "*")),
        Del(Path.Combine(LocalAppDataPath, "NVIDIA", "GLCache", "*")),
        Del(Path.Combine(LocalAppDataPath, "AMD", "DxCache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Intel", "ShaderCache", "*"))
    ];

    public static readonly IReadOnlyList<string> InstantMessagingCacheCommands =
    [
        Del(Path.Combine(RoamingAppDataPath, "Tencent", "QQNT", "nt_qq", "nt_temp", "*")),
        Del(Path.Combine(RoamingAppDataPath, "Telegram Desktop", "tdata", "temp", "*")),
        Del(Path.Combine(RoamingAppDataPath, "DingTalk", "temp", "*")),
        Del(Path.Combine(LocalAppDataPath, "Tencent", "WeChat", "Caches", "*"))
    ];

    public static readonly IReadOnlyList<string> DevCacheCommands =
    [
        Del(Path.Combine(LocalAppDataPath, "pip", "cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "npm-cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "Yarn", "Cache", "*")),
        Del(Path.Combine(LocalAppDataPath, "NuGet", "v3-cache", "*"))
    ];
}

