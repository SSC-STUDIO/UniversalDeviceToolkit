using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

/// <summary>Represents the Always-On USB charging state.</summary>
public enum AlwaysOnUSBState
{
    [Display(ResourceType = typeof(Resource), Name = "AlwaysOnUSBState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "AlwaysOnUSBState_OnWhenSleeping")]
    OnWhenSleeping,
    [Display(ResourceType = typeof(Resource), Name = "AlwaysOnUSBState_OnAlways")]
    OnAlways
}

/// <summary>Represents the battery night-charge setting.</summary>
public enum BatteryNightChargeState
{
    [Display(ResourceType = typeof(Resource), Name = "BatteryNightChargeState_On")]
    On,
    [Display(ResourceType = typeof(Resource), Name = "BatteryNightChargeState_Off")]
    Off
}

/// <summary>Represents the battery charging mode (Conservation, Normal, Rapid Charge).</summary>
public enum BatteryState
{
    [Display(ResourceType = typeof(Resource), Name = "BatteryState_Conservation")]
    Conservation,
    [Display(ResourceType = typeof(Resource), Name = "BatteryState_Normal")]
    Normal,
    [Display(ResourceType = typeof(Resource), Name = "BatteryState_RapidCharge")]
    RapidCharge
}

/// <summary>Represents the Flip-to-Start (open lid to power on) state.</summary>
public enum FlipToStartState
{
    [Display(ResourceType = typeof(Resource), Name = "FlipToStartState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "FlipToStartState_On")]
    On
}

/// <summary>Represents the Fn key lock state.</summary>
public enum FnLockState
{
    [Display(ResourceType = typeof(Resource), Name = "FnLockState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "FnLockState_On")]
    On
}

/// <summary>Represents the discrete GPU state.</summary>
public enum GPUState
{
    Unknown,
    NvidiaGpuNotFound,
    MonitorConnected,
    Active,
    Inactive,
    PoweredOff
}

/// <summary>Represents the G-Sync display technology state.</summary>
public enum GSyncState
{
    Off,
    On
}

/// <summary>Represents the HDR (High Dynamic Range) display state.</summary>
public enum HDRState
{
    [Display(ResourceType = typeof(Resource), Name = "HDRState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "HDRState_On")]
    On
}

/// <summary>Represents the hybrid GPU mode (iGPU + dGPU switching).</summary>
public enum HybridModeState
{
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_On")]
    On,
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_OnIGPUOnly")]
    OnIGPUOnly,
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_OnAuto")]
    OnAuto,
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_Off")]
    Off
}

/// <summary>Represents the integrated GPU operating mode.</summary>
public enum IGPUModeState
{
    Default,
    IGPUOnly,
    Auto
}

/// <summary>Represents the Intelligent Thermal Solution (ITS) operating mode.</summary>
public enum ITSMode
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "ITSMode_Intelligent_Cooling")]
    ItsAuto,
    [Display(ResourceType = typeof(Resource), Name = "ITSMode_Intelligent_Battery_Saving")]
    MmcCool,
    [Display(ResourceType = typeof(Resource), Name = "ITSMode_Intelligent_Extreme_Performance")]
    MmcPerformance,
    [Display(ResourceType = typeof(Resource), Name = "ITSMode_Intelligent_Geek")]
    MmcGeek
}

/// <summary>Represents the instant-boot power source configuration.</summary>
public enum InstantBootState
{
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_AcAdapter")]
    AcAdapter,
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_UsbPowerDelivery")]
    UsbPowerDelivery,
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_AcAdapterAndUsbPowerDelivery")]
    AcAdapterAndUsbPowerDelivery
}

/// <summary>Identifies well-known Windows shell folders.</summary>
public enum KnownFolder
{
    Contacts,
    Downloads,
    Favorites,
    Links,
    SavedGames,
    SavedSearches
}

/// <summary>Represents the microphone mute state.</summary>
public enum MicrophoneState
{
    [Display(ResourceType = typeof(Resource), Name = "MicrophoneState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "MicrophoneState_On")]
    On
}

/// <summary>Represents keyboard modifier keys (Shift, Ctrl, Alt) as flags.</summary>
[Flags]
public enum ModifierKey
{
    None = 0,
    [Display(ResourceType = typeof(Resource), Name = "ModifierKey_Shift")]
    Shift = 1,
    [Display(ResourceType = typeof(Resource), Name = "ModifierKey_Ctrl")]
    Ctrl = 2,
    [Display(ResourceType = typeof(Resource), Name = "ModifierKey_Alt")]
    Alt = 4
}

/// <summary>Identifies native Windows system messages for device and display events.</summary>
public enum NativeWindowsMessage
{
    LidOpened,
    LidClosed,
    MonitorOn,
    MonitorOff,
    DeviceConnected,
    DeviceDisconnected,
    MonitorConnected,
    MonitorDisconnected,
    ExternalMonitorConnected,
    ExternalMonitorDisconnected,
    OnDisplayDeviceArrival,
    BatterySaverEnabled
}

/// <summary>Represents the AMD OverDrive feature state.</summary>
public enum OverDriveState
{
    [Display(ResourceType = typeof(Resource), Name = "OverdriveState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "OverdriveState_On")]
    On
}

/// <summary>Represents the AC power adapter connection status.</summary>
public enum PowerAdapterStatus
{
    Connected,
    ConnectedLowWattage,
    Disconnected
}

/// <summary>Represents how the application maps to Windows power mode/plan settings.</summary>
public enum PowerModeMappingMode
{
    [Display(ResourceType = typeof(Resource), Name = "PowerModeMappingMode_Disabled")]
    Disabled,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeMappingMode_WindowsPowerMode")]
    WindowsPowerMode,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeMappingMode_WindowsPowerPlan")]
    WindowsPowerPlan,
}

/// <summary>Represents the device power mode (Quiet, Balance, Performance, Extreme, GodMode).</summary>
public enum PowerModeState
{
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Quiet")]
    Quiet,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Balance")]
    Balance,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Performance")]
    Performance,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Extreme")]
    Extreme = 223,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_GodMode")]
    GodMode = 254
}

/// <summary>Represents system power state events (suspend, resume, status change).</summary>
public enum PowerStateEvent
{
    Unknown = -1,
    StatusChange,
    Suspend,
    Resume,
}

/// <summary>Indicates whether a process started or stopped.</summary>
public enum ProcessEventInfoType
{
    Started,
    Stopped
}

/// <summary>Represents the type of system reboot required.</summary>
public enum RebootType
{
    NotRequired = 0,
    Forced = 1,
    Requested = 3,
    ForcedPowerOff = 4,
    Delayed = 5
}

/// <summary>Represents the speaker mute/unmute state.</summary>
public enum SpeakerState
{
    [Display(ResourceType = typeof(Resource), Name = "SpeakerState_Mute")]
    Mute,
    [Display(ResourceType = typeof(Resource), Name = "SpeakerState_Unmute")]
    Unmute
}

/// <summary>Represents the software/service availability status.</summary>
public enum SoftwareStatus
{
    Enabled,
    Disabled,
    NotFound
}

/// <summary>Identifies special function key codes sent by the embedded controller.</summary>
public enum SpecialKey
{
    FnF9 = 1,
    FnLockOn = 2,
    FnLockOff = 3,
    FnPrtSc = 4,
    FnPrtSc2 = 45,
    CameraOn = 12,
    CameraOff = 13,
    FnR = 16,
    FnR2 = 0x0041002A,
    SpectrumBacklightOff = 24,
    SpectrumBacklight1 = 25,
    SpectrumBacklight2 = 26,
    SpectrumBacklight3 = 38,
    SpectrumPreset1 = 32,
    SpectrumPreset2 = 33,
    SpectrumPreset3 = 34,
    SpectrumPreset4 = 35,
    SpectrumPreset5 = 36,
    SpectrumPreset6 = 37,
    FnN = 42,
    FnF4 = 62,
    FnF8 = 63,
    WhiteBacklightOff = 64,
    WhiteBacklight1 = 65,
    WhiteBacklight2 = 66
}

/// <summary>Represents the touchpad lock (disable) state.</summary>
public enum TouchpadLockState
{
    [Display(ResourceType = typeof(Resource), Name = "TouchpadLockState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "TouchpadLockState_On")]
    On
}

/// <summary>Represents the Windows OS power mode setting.</summary>
public enum WindowsPowerMode
{
    [Display(Name = "Best power efficiency")]
    BestPowerEfficiency,
    [Display(Name = "Balanced")]
    Balanced,
    [Display(Name = "Best performance")]
    BestPerformance
}

/// <summary>Represents the Windows key lock state.</summary>
public enum WinKeyState
{
    [Display(ResourceType = typeof(Resource), Name = "WinKeyState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "WinKeyState_On")]
    On
}

/// <summary>Placeholder enum for Windows key change tracking.</summary>
public enum WinKeyChanged
{
    None
}
