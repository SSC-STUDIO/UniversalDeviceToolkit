using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

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
