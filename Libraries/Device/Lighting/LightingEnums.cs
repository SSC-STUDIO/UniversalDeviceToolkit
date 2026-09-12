using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

/// <summary>Represents physical keyboard layout types.</summary>
public enum KeyboardLayout
{
    Ansi,
    Iso,
    Jis,
    Keyboard24Zone,
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

/// <summary>Represents the single-level white keyboard backlight state.</summary>
public enum OneLevelWhiteKeyboardBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "OneLevelWhiteKeyboardBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "OneLevelWhiteKeyboardBacklightState_On")]
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

/// <summary>Represents the USB port backlight state.</summary>
public enum PortsBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "PortsBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "PortsBacklightState_On")]
    On
}

/// <summary>Placeholder enum for RGB keyboard backlight change tracking.</summary>
public enum RGBKeyboardBacklightChanged
{
    None
}

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
