using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

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
