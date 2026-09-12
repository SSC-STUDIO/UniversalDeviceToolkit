using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

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
