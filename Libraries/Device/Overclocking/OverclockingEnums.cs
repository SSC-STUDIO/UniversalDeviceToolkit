using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

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
