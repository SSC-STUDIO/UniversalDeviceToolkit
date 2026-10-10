using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

/// <summary>Represents the initialization state of the Libre Hardware Monitor library.</summary>
public enum LibreHardwareMonitorInitialState
{
    Fail = 0,
    Initialized = 1,
    Success = 2,
    PawnIONotInstalled = 3
}

/// <summary>Represents the PawnIO driver installation state.</summary>
public enum PawnIOState
{
    NotInstalled,
    Installed,
}

/// <summary>Represents the temperature display unit (Celsius or Fahrenheit).</summary>
public enum TemperatureUnit
{
    C,
    F
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
