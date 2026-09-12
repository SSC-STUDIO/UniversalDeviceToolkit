namespace UniversalDeviceToolkit.Host.Rpc;

/// <summary>
/// Single source of truth for the bridge method names.
///
/// Windows builds register a concrete handler for every name listed here
/// (checked at startup by Program.VerifyRpcSurface). Portable builds register
/// real handlers for <see cref="PortableCapable"/> (via IPlatformServices and
/// related abstractions) and "-32099 not supported" stubs for
/// <see cref="WindowsOnly"/>. Keeping one list prevents the two surfaces from
/// drifting apart (an unknown method surfaces to the Electron client as -32601
/// instead of a clean "not supported").
///
/// Not listed here:
/// - Always-on methods registered on every platform (see <see cref="AlwaysOn"/>).
/// - macro.*: MacroHandlers compiles on every platform. Windows uses global
///   hooks; portable hosts persist sequences via IConfigurationStore and
///   report -32099 only for playback/recording.
/// - Electron-main methods that never reach the Host (powerPlans.*, power.*,
///   update.getRelease/download/launchInstaller, device.info, dialog:*).
/// </summary>
public static class RpcMethodNames
{
    /// <summary>
    /// Methods registered on every platform before domain handlers.
    /// </summary>
    public static readonly string[] AlwaysOn =
    [
        "ping",
        "app.getStatus",
        "app.getLogPath",
        "app.quit",
        "localization.getCulture",
        "localization.setCulture",
        "host.getCapabilities",
        "app.setUiActive",
    ];

    /// <summary>
    /// Core Electron RPC that portable hosts implement via existing platform
    /// abstractions when those backends are registered. Missing backends still
    /// answer -32099; they do not return empty success payloads.
    /// </summary>
    public static readonly string[] PortableCapable =
    [
        "system.info",
        "system.powerAdapterStatus",
        "settings.getAll",
        "settings.get",
        "settings.set",
        "settings.save",
        "settings.reload",
        "sensors.getStatus",
        "sensors.getSnapshot",
        "sensors.getDetailed",
        "sensors.subscribe",
        "sensors.unsubscribe",
        "sensors.getSettings",
        "sensors.setSettings",
        "dashboard.getConfig",
        "dashboard.saveConfig",
        "feature.list",
        "feature.getSupported",
        "feature.getStates",
        "feature.getState",
        "feature.setState",
        "feature.isHdrBlocked",
        "app.getAutorun",
        "app.setAutorun",
        "automation.getState",
        "automation.setEnabled",
        "automation.savePipelines",
        "automation.runNow",
        "automation.getSupportedSteps",
    ];

    /// <summary>Windows-only methods; stubbed with -32099 on other platforms.</summary>
    public static readonly string[] WindowsOnly =
    [
        "wmi.getGodModeFnQ",
        "wmi.setGodModeFnQ",
        "godMode.getState",
        "godMode.setState",
        "godMode.apply",
        "software.getStatus",
        "software.setEnabled",
        "app.update.check",
        "app.update.status",
        "system.accentColor.get",
        "system.accentColor.set",
        "sensors.getFps",
        "sensors.subscribeFps",
        "sensors.unsubscribeFps",
        "bootLogo.getStatus",
        "bootLogo.enable",
        "bootLogo.disable",
        "dashboardHardware.getState",
        "dashboardHardware.setMonitoring",
        "dashboardHardware.killGpuProcesses",
        "dashboardHardware.restartGpu",
        "dashboardHardware.setOverclockEnabled",
        "dashboardHardware.setOverclock",
        "dashboardHardware.turnOffMonitors",
        "keyboard.detect",
        "rgb.isSupported",
        "rgb.getState",
        "rgb.setState",
        "rgb.setPreset",
        "rgb.nextPreset",
        "rgb.takeOwnership",
        "spectrum.isSupported",
        "spectrum.getLayout",
        "spectrum.getState",
        "spectrum.getBrightness",
        "spectrum.setBrightness",
        "spectrum.getLogoStatus",
        "spectrum.setLogoStatus",
        "spectrum.getProfile",
        "spectrum.setProfile",
        "spectrum.getProfileDescription",
        "spectrum.setProfileDescription",
        "optimization.getCategories",
        "optimization.apply",
        "optimization.revert",
        "optimization.applyRecommended",
        "optimization.getActionStatus",
        "cleanup.estimate",
        "cleanup.run",
        "cleanup.getCustomRules",
        "cleanup.saveCustomRules",
        "network.getStatus",
        "network.saveConfig",
        "network.start",
        "network.stop",
        "network.restore",
        "network.detectNat",
        "network.detectDns",
        "network.detectIpv6",
        "network.getTrafficSnapshot",
        "network.getRuntimeSnapshot",
        "ai.getStatus",
        "ai.setEnabled",
        "driver.getSettings",
        "driver.getPackages",
        "driver.getPackageStatuses",
        "driver.start",
        "driver.pause",
        "driver.install",
        "driver.uninstall",
        "driver.setDownloadPath",
        "driver.setOnlyShowUpdates",
        "driver.setHiddenPackageIds",
        "gameBoost.getStatus",
        "gameBoost.getConfig",
        "gameBoost.saveConfig",
        "gameBoost.boostNow",
        "gameBoost.revertNow",
        "mouse.getState",
        "mouse.applyWindows",
        "mouse.setCursorThemeMode",
        "mouse.applyCursorThemeNow",
        "mouse.syncFromWindows",
        "mouse.restoreWindowsDefault",
    ];
}
