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

/// <summary>Represents the autorun/startup behavior setting.</summary>
public enum AutorunState
{
    [Display(ResourceType = typeof(Resource), Name = "AutorunState_Enabled")]
    Enabled,
    [Display(ResourceType = typeof(Resource), Name = "AutorunState_EnabledDelayed")]
    EnabledDelayed,
    [Display(ResourceType = typeof(Resource), Name = "AutorunState_Disabled")]
    Disabled
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

/// <summary>Identifies hardware capability feature IDs used by the embedded controller.</summary>
public enum CapabilityID
{
    IGPUMode = 0x00010000,
    FlipToStart = 0x00030000,
    NvidiaGPUDynamicDisplaySwitching = 0x00040000,
    AMDSmartShiftMode = 0x00050001,
    AMDSkinTemperatureTracking = 0x00050002,
    SupportedPowerModes = 0x00070000,
    LegionZoneSupportVersion = 0x00090000,
    GodModeFnQSwitchable = 0x00100000,
    OverDrive = 0x001A0000,
    AIChip = 0x000E0000,
    IGPUModeChangeStatus = 0x000F0000,
    CPUShortTermPowerLimit = 0x0101FF00,
    CPULongTermPowerLimit = 0x0102FF00,
    CPUPeakPowerLimit = 0x0103FF00,
    CPUTemperatureLimit = 0x0104FF00,
    APUsPPTPowerLimit = 0x0105FF00,
    CPUCrossLoadingPowerLimit = 0x0106FF00,
    CPUPL1Tau = 0x0107FF00,
    CPUOverclockingEnable = 0x0108FF00,
    GPUPowerBoost = 0x0201FF00,
    GPUConfigurableTGP = 0x0202FF00,
    GPUTemperatureLimit = 0x0203FF00,
    GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = 0x0204FF00,
    GPUToCPUDynamicBoost = 0x020BFF00,
    GPUStatus = 0x02070000,
    GPUDidVid = 0x02090000,
    InstantBootAc = 0x03010001,
    InstantBootUsbPowerDelivery = 0x03010002,
    FanFullSpeed = 0x04020000,
    CpuCurrentFanSpeed = 0x04030001,
    GpuCurrentFanSpeed = 0x04030002,
    PchCurrentFanSpeed = 0x04030004,
    PchCurrentTemperature = 0x05010000,
    CpuCurrentTemperature = 0x05040000,
    GpuCurrentTemperature = 0x05050000
}

/// <summary>Identifies CPU overclocking parameters.</summary>
public enum CPUOverclockingID
{
    PrecisionBoostOverdriveScaler = 0x414D4401,
    PrecisionBoostOverdriveBoostFrequency = 0x414D4402,
    AllCoreCurveOptimizer = 0x414D4403,
}

/// <summary>Represents CPU profile modes for different workload types.</summary>
public enum CpuProfileMode
{
    Productivity,
    X3DGaming
}

/// <summary>Represents keyboard shortcut driver key flags.</summary>
[Flags]
public enum DriverKey
{
    FnF10 = 32,
    FnF4 = 256,
    FnF8 = 8192,
    FnSpace = 4096,
}

/// <summary>Represents the fan control mode (Auto or Manual).</summary>
public enum FanState
{
    Auto,
    Manual,
}

/// <summary>Identifies the physical fan type (CPU, GPU, or System).</summary>
public enum FanType
{
    [Display(ResourceType = typeof(Resource), Name = "CustomFanCurveControl_Fan_CPU")]
    Cpu = 0,
    [Display(ResourceType = typeof(Resource), Name = "CustomFanCurveControl_Fan_GPU")]
    Gpu = 1,
    [Display(ResourceType = typeof(Resource), Name = "CustomFanCurveControl_Fan_System")]
    System = 2,
}

/// <summary>Identifies fan table types for thermal management.</summary>
public enum FanTableType
{
    Unknown,
    CPU,
    CPUSensor,
    GPU,
    GPU2,
    PCH,
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

/// <summary>Represents physical keyboard layout types.</summary>
public enum KeyboardLayout
{
    Ansi,
    Iso,
    Jis,
    Keyboard24Zone,
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

/// <summary>Represents lamp/RGB lighting effect types.</summary>
public enum LampEffectType
{
    Static,
    Breathe,
    Wave,
    Rainbow,
    Meteor,
    Ripple,
    Sparkle,
    Gradient,
    CustomPattern,
    RainbowWave,
    SpiralRainbow,
    AuroraSync,
}

/// <summary>Identifies which lighting zone changed (Panel or Ports).</summary>
public enum LightingChangeState
{
    Panel = 0,
    Ports = 1,
}

/// <summary>Identifies the product series of the Lenovo/Legion device.</summary>
public enum LegionSeries
{
    Legion_5 = 0,
    Legion_Pro_5 = 1,
    Legion_Slim_5 = 2,
    Legion_7 = 3,
    Legion_Pro_7 = 4,
    Legion_9 = 5,
    Legion_Go = 6,
    Lenovo_Slim = 7,
    Legion_Legacy = 8,
    IdeaPad = 9,
    IdeaPad_Gaming = 10,
    LOQ = 11,
    YOGA = 12,
    ThinkBook = 13,
    Unknown = 255
}

/// <summary>Represents the initialization state of the Libre Hardware Monitor library.</summary>
public enum LibreHardwareMonitorInitialState
{
    Fail = 0,
    Initialized = 1,
    Success = 2,
    PawnIONotInstalled = 3
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

/// <summary>Represents the duration for on-screen notifications.</summary>
public enum NotificationDuration
{
    [Display(ResourceType = typeof(Resource), Name = "NotificationDuration_Short")]
    Short,
    [Display(ResourceType = typeof(Resource), Name = "NotificationDuration_Normal")]
    Normal,
    [Display(ResourceType = typeof(Resource), Name = "NotificationDuration_Long")]
    Long
}

/// <summary>Identifies the type of system notification to display.</summary>
public enum NotificationType
{
    ACAdapterConnected,
    ACAdapterConnectedLowWattage,
    ACAdapterDisconnected,
    AutomationNotification,
    CameraOn,
    CameraOff,
    CapsLockOn,
    CapsLockOff,
    FnLockOn,
    FnLockOff,
    MicrophoneOff,
    MicrophoneOn,
    NumLockOn,
    NumLockOff,
    PanelLogoLightingOn,
    PanelLogoLightingOff,
    PortLightingOn,
    PortLightingOff,
    PowerModeQuiet,
    PowerModeBalance,
    PowerModePerformance,
    PowerModeExtreme,
    PowerModeGodMode,
    RefreshRate,
    RGBKeyboardBacklightChanged,
    RGBKeyboardBacklightOff,
    SmartKeyDoublePress,
    SmartKeySinglePress,
    SpectrumBacklightChanged,
    SpectrumBacklightOff,
    SpectrumBacklightPresetChanged,
    TouchpadOn,
    TouchpadOff,
    UpdateAvailable,
    WhiteKeyboardBacklightChanged,
    WhiteKeyboardBacklightOff,
    ITSModeAuto,
    ITSModeCool,
    ITSModePerformance,
    ITSModeGeek
}

/// <summary>Represents the priority level of a notification.</summary>
public enum NotificationPriority
{
    Low,
    Normal,
    High
}

/// <summary>Represents the on-screen position for notifications.</summary>
public enum NotificationPosition
{
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_BottomRight")]
    BottomRight,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_BottomCenter")]
    BottomCenter,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_BottomLeft")]
    BottomLeft,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_CenterLeft")]
    CenterLeft,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_TopLeft")]
    TopLeft,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_TopCenter")]
    TopCenter,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_TopRight")]
    TopRight,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_CenterRight")]
    CenterRight,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_Center")]
    Center
}

/// <summary>Represents the single-level white keyboard backlight state.</summary>
public enum OneLevelWhiteKeyboardBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "OneLevelWhiteKeyboardBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "OneLevelWhiteKeyboardBacklightState_On")]
    On
}

/// <summary>Identifies the Windows operating system version.</summary>
public enum OS
{
    [Display(Name = "Windows 11")]
    Windows11,
    [Display(Name = "Windows 10")]
    Windows10,
    [Display(Name = "Windows 8")]
    Windows8,
    [Display(Name = "Windows 7")]
    Windows7
}

/// <summary>Represents the AMD OverDrive feature state.</summary>
public enum OverDriveState
{
    [Display(ResourceType = typeof(Resource), Name = "OverdriveState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "OverdriveState_On")]
    On
}

/// <summary>Represents the panel logo backlight state.</summary>
public enum PanelLogoBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "PanelLogoBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "PanelLogoBacklightState_On")]
    On
}

/// <summary>Represents the PawnIO driver installation state.</summary>
public enum PawnIOState
{
    NotInstalled,
    Installed,
}

/// <summary>Represents the USB port backlight state.</summary>
public enum PortsBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "PortsBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "PortsBacklightState_On")]
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

/// <summary>Placeholder enum for RGB keyboard backlight change tracking.</summary>
public enum RGBKeyboardBacklightChanged
{
    None
};

/// <summary>Represents the RGB keyboard backlight brightness level.</summary>
public enum RGBKeyboardBacklightBrightness
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightBrightness_Low")]
    Low,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightBrightness_High")]
    High
}

/// <summary>Represents the RGB keyboard backlight effect type.</summary>
public enum RGBKeyboardBacklightEffect
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_Static")]
    Static,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_Breath")]
    Breath,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_Smooth")]
    Smooth,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_WaveRTL")]
    WaveRTL,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_WaveLTR")]
    WaveLTR
}

/// <summary>Represents the RGB keyboard backlight color preset index.</summary>
public enum RGBKeyboardBacklightPreset
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Off")]
    Off = -1,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_One")]
    One = 0,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Two")]
    Two = 1,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Three")]
    Three = 2,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Four")]
    Four = 3
}

/// <summary>Represents the RGB keyboard backlight animation speed.</summary>
public enum RGBKeyboardBacklightSpeed
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Slowest")]
    Slowest,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Slow")]
    Slow,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Fast")]
    Fast,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Fastest")]
    Fastest
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

/// <summary>Represents the Spectrum RGB keyboard backlight brightness level.</summary>
public enum SpectrumKeyboardBacklightBrightness
{
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_Low")]
    Low,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_Medium")]
    Medium,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_High")]
    High
}

/// <summary>Represents the Spectrum keyboard backlight rotational direction.</summary>
public enum SpectrumKeyboardBacklightClockwiseDirection
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_Clockwise")]
    Clockwise,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_CounterClockwise")]
    CounterClockwise
}

/// <summary>Represents the Spectrum keyboard backlight animation direction.</summary>
public enum SpectrumKeyboardBacklightDirection
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_BottomToTop")]
    BottomToTop,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_TopToBottom")]
    TopToBottom,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_LeftToRight")]
    LeftToRight,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_RightToLeft")]
    RightToLeft
}

/// <summary>Represents the Spectrum keyboard backlight effect type.</summary>
public enum SpectrumKeyboardBacklightEffectType
{
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Always")]
    Always,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_RainbowScrew")]
    RainbowScrew,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_RainbowWave")]
    RainbowWave,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_ColorChange")]
    ColorChange,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_ColorWave")]
    ColorWave,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_ColorPulse")]
    ColorPulse,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Smooth")]
    Smooth,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Rain")]
    Rain,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Ripple")]
    Ripple,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Type")]
    Type,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_AudioBounce")]
    AudioBounce,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_AudioRipple")]
    AudioRipple,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_AuroraSync")]
    AuroraSync
}

/// <summary>Represents the Spectrum keyboard backlight animation speed.</summary>
public enum SpectrumKeyboardBacklightSpeed
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightSpeed_Speed1")]
    Speed1,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightSpeed_Speed2")]
    Speed2,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightSpeed_Speed3")]
    Speed3
}

/// <summary>Represents the Spectrum RGB lighting zone layout.</summary>
public enum SpectrumLayout
{
    KeyboardOnly,
    KeyboardAndFront,
    Full,
    FullAlternative
}

/// <summary>Represents the application color theme (System, Light, Dark).</summary>
public enum Theme
{
    [Display(ResourceType = typeof(Resource), Name = "Theme_System")]
    System,
    [Display(ResourceType = typeof(Resource), Name = "Theme_Light")]
    Light,
    [Display(ResourceType = typeof(Resource), Name = "Theme_Dark")]
    Dark
}

/// <summary>Represents the source for the UI accent color.</summary>
public enum AccentColorSource
{
    [Display(ResourceType = typeof(Resource), Name = "AccentColorSource_System")]
    System,
    [Display(ResourceType = typeof(Resource), Name = "AccentColorSource_Custom")]
    Custom
}

/// <summary>Represents predefined theme style presets.</summary>
public enum ThemeStylePreset
{
    [Display(ResourceType = typeof(Resource), Name = "ThemeStylePreset_Default")]
    Default,
    [Display(ResourceType = typeof(Resource), Name = "ThemeStylePreset_Official")]
    Official,
    [Display(ResourceType = typeof(Resource), Name = "ThemeStylePreset_Midnight")]
    Midnight,
    [Display(ResourceType = typeof(Resource), Name = "ThemeStylePreset_Forest")]
    Forest
}

/// <summary>Represents the window backdrop (title bar) visual style.</summary>
public enum WindowBackdropStyle
{
    [Display(ResourceType = typeof(Resource), Name = "WindowBackdropStyle_Windows")]
    Windows,
    [Display(ResourceType = typeof(Resource), Name = "WindowBackdropStyle_macOS")]
    macOS,
    [Display(ResourceType = typeof(Resource), Name = "WindowBackdropStyle_Off")]
    Off
}

/// <summary>Represents the application font family style.</summary>
// No Display attributes: option labels are font names (proper nouns) resolved
// WPF-side, like TemperatureUnit.
public enum AppFontStyle
{
    Default,
    FluentVariable,
    YaHeiUI,
    DengXian,
    NotoSans,
    SimHei,
    SimSun,
    KaiTi
}

/// <summary>Represents the application text size preset (Compact to ExtraLarge).</summary>
// No Display attributes: option labels are percentages (with a localized "Default"
// marker on Standard) resolved WPF-side, like AppFontStyle. The numeric scale
// mapping (90/100/110/125%) lives in the WPF layer.
public enum AppTextSize
{
    Compact,
    Standard,
    Large,
    ExtraLarge
}

/// <summary>Represents the application UI scale percentage.</summary>
public enum AppScale
{
    Compact = 80,
    Small = 90,
    Standard = 100,
    Large = 110,
    ExtraLarge = 125
}

/// <summary>Represents the temperature display unit (Celsius or Fahrenheit).</summary>
public enum TemperatureUnit
{
    C,
    F
}

/// <summary>Represents the thermal mode state reported by the embedded controller.</summary>
public enum ThermalModeState
{
    Unknown,
    Quiet,
    Balance,
    Performance,
    Extreme = 224,
    GodMode = 255
}

/// <summary>Represents the touchpad lock (disable) state.</summary>
public enum TouchpadLockState
{
    [Display(ResourceType = typeof(Resource), Name = "TouchpadLockState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "TouchpadLockState_On")]
    On
}

/// <summary>Represents the automatic update check frequency.</summary>
public enum UpdateCheckFrequency
{
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerHour")]
    PerHour,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerThreeHours")]
    PerThreeHours,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerTwelveHours")]
    PerTwelveHours,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerDay")]
    PerDay,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerWeek")]
    PerWeek,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerMonth")]
    PerMonth
}

/// <summary>Represents the result status of an update check operation.</summary>
public enum UpdateCheckStatus
{
    Success,
    RateLimitReached,
    Error
}

/// <summary>Represents the white keyboard backlight brightness level (multi-level).</summary>
public enum WhiteKeyboardBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "WhiteKeyboardBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "WhiteKeyboardBacklightState_Low")]
    Low,
    [Display(ResourceType = typeof(Resource), Name = "WhiteKeyboardBacklightState_High")]
    High
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

/// <summary>Identifies on-screen display (OSD) hardware sensor metrics.</summary>
public enum OsdItem
{
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Fps")]
    Fps,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_LowFps")]
    LowFps,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_FrameTime")]
    FrameTime,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Frequency")]
    CpuFrequency,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_P_Core_Frequency")]
    CpuPCoreFrequency,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_E_Core_Frequency")]
    CpuECoreFrequency,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Utilization")]
    CpuUtilization,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Temperature")]
    CpuTemperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Power")]
    CpuPower,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Fan")]
    CpuFan,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Frequency")]
    GpuFrequency,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Utilization")]
    GpuUtilization,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Temperature")]
    GpuTemperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_VramUtilization")]
    GpuVramUtilization,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_VramTemperature")]
    GpuVramTemperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Power")]
    GpuPower,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Fan")]
    GpuFan,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Utilization")]
    MemoryUtilization,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_MemoryTemperature")]
    MemoryTemperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Disk1Temperature")]
    Disk1Temperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Disk2Temperature")]
    Disk2Temperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_MotherboardTemperature")]
    PchTemperature,
    [Display(ResourceType = typeof(Resource), Name = "OsdItem_Fan")]
    PchFan,
}

/// <summary>Represents the on-screen display visibility state.</summary>
public enum OsdState
{
    [Display(ResourceType = typeof(Resource), Name = "OsdState_Hidden")]
    Hidden,
    [Display(ResourceType = typeof(Resource), Name = "OsdState_Show")]
    Show,
    [Display(ResourceType = typeof(Resource), Name = "OsdState_Toggle")]
    Toggle,
}

/// <summary>Represents the hardware sensors monitoring state.</summary>
public enum HardwareSensorsState
{
    [Display(ResourceType = typeof(Resource), Name = "HardwareSensorsState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "HardwareSensorsState_On")]
    On
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
};
