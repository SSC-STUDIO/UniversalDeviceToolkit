using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Lib.System;

// ---------------------------------------------------------------------------
// Status codes returned by NVAPI functions.
// ---------------------------------------------------------------------------
internal enum NvApiStatus : int
{
    Ok = 0,
    Error = -1,
    LibraryNotFound = -2,
    NoImplementation = -3,
    HandleInvalidated = -4,
    OpenGLContextNotCurrent = -5,
    NoGlExpert = -6,
    InstrumentationDisabled = -7,
    ExpectedLogicalGpuHandle = -100,
    ExpectedPhysicalGpuHandle = -101,
    ExpectedDisplayHandle = -102,
    InvalidCombination = -103,
    NotSupported = -104,
    PortIdNotFound = -105,
    ExpectedUnattachedDisplayHandle = -106,
    InvalidPerfLevel = -107,
    DeviceBusy = -108,
    NvPersistFileNotFound = -109,
    PersistDataNotFound = -110,
    ExpectedTvDisplay = -111,
    ExpectedTvDisplayOnConnector = -112,
    NoActiveSliTopology = -113,
    SliRenderingModeNotAllowed = -114,
    ExpectedDigitalFlatPanel = -115,
    ArgumentExceedMaxSize = -116,
    DeviceSwitchingNotSupported = -117,
    TestingClocksNotSupported = -118,
    UnknownUnderscanConfig = -119,
    ErrorReprogrammingTopology = -120,
    NoEdidCeaV3BlockPresent = -121,
    ExpectedAnalogDisplay = -122,
    ApiNotInitialized = -123,
    InsufficientBuffer = -124,
    StringTooSmall = -125,
    WrongFamily = -126,
    GpuNotFound = -127,
    GpuNotPowered = -128,
    InvalidArgument = -129,
    MosaicNotActive = -130,
    ShareResourceRelocated = -131,
    ExpectedPhysicalDisplayHandle = -132,
    NotAllowed = -133,
}

// ---------------------------------------------------------------------------
// System type: Laptop or Desktop
// ---------------------------------------------------------------------------
internal enum NvSystemType : uint
{
    Unknown = 0,
    Laptop = 1,
    Desktop = 2,
}

// ---------------------------------------------------------------------------
// GPU memory manufacturer
// ---------------------------------------------------------------------------
internal enum NvGpuMemoryMaker : uint
{
    Unknown = 0,
    Samsung = 1,
    Qimonda = 2,
    Elpida = 3,
    Etron = 4,
    Nanya = 5,
    Hynix = 6,
    Mosel = 7,
    Winbond = 8,
    Elite = 9,
    Micron = 10,
}

// ---------------------------------------------------------------------------
// Performance state identifiers (P0 = max perf, P8 = basic idle, etc.)
// ---------------------------------------------------------------------------
internal enum NvPerformanceStateId : uint
{
    P0_3DPerformance = 0,
    P1_HDVideoPlayback = 1,
    P2_Balanced = 2,
    P3_PowerSaving = 3,
    P4 = 4,
    P5 = 5,
    P6 = 6,
    P7 = 7,
    P8_BasicIdle = 8,
    P9 = 9,
    P10 = 10,
    P11 = 11,
    P12 = 12,
    P13 = 13,
    P14 = 14,
    P15 = 15,
    Undefined = 16,
}

// ---------------------------------------------------------------------------
// Public clock domains
// ---------------------------------------------------------------------------
internal enum NvPublicClockDomain : uint
{
    Graphics = 0,
    Memory = 4,
    Processor = 7,
    Video = 8,
    Undefined = 30,
}

// ---------------------------------------------------------------------------
// Thermal sensor target / controller
// ---------------------------------------------------------------------------
internal enum NvThermalTarget : int
{
    None = 0,
    Gpu = 1,
    Memory = 2,
    PowerSupply = 4,
    Board = 8,
    VisualComputingBoard = 9,
    VisualComputingInlet = 10,
    VisualComputingOutlet = 11,
    All = 15,
    Unknown = -1,
}

internal enum NvThermalController : int
{
    None = 0,
    GpuInternal = 1,
    Adm1032 = 2,
    Max6649 = 3,
    Max1617 = 4,
    Lm99 = 5,
    Lm89 = 6,
    Lm64 = 7,
    Ds1620 = 8,
    Adt7473 = 9,
    SBMax6649 = 10,
    VBiosEvt = 11,
    OS = 12,
    Unknown = -1,
}

// ---------------------------------------------------------------------------
// GPU / Display handles (opaque pointer wrappers)
// ---------------------------------------------------------------------------
internal readonly record struct NvPhysicalGpuHandle(nint Value)
{
    public bool IsValid => Value != nint.Zero;
}

internal readonly record struct NvDisplayHandle(nint Value)
{
    public bool IsValid => Value != nint.Zero;
}

// ---------------------------------------------------------------------------
// PCI identifiers
// ---------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct NvPciIdentifiers
{
    public uint DeviceId;
    public uint SubSystemId;
    public uint RevisionId;
    public uint VendorId;

    public override readonly string ToString() =>
        $"PCI\\VEN_{VendorId:X4}&DEV_{DeviceId:X4}&SUBSYS_{SubSystemId:X8}&REV_{RevisionId:X2}";
}

// ---------------------------------------------------------------------------
// Active application info (NVAPI v1 = simple struct, v2 = with process name)
// ---------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct NvActiveAppV1
{
    public uint Version;
    public uint ProcessId;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct NvActiveAppV2
{
    public uint Version;
    public uint ProcessId;
    public uint DeviceId;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string ProcessName;
}

// ---------------------------------------------------------------------------
// Dynamic P-state domain utilization
// ---------------------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
internal struct NvDynamicPstateInfo
{
    public uint Version;
    public uint Flags;
    public uint GpuUtilization;
    public uint FbUtilization;
    public uint VidUtilization;
    public uint BusUtilization;
}

// ---------------------------------------------------------------------------
// Clock frequency info (returned by NvAPI_GPU_GetAllClockFrequencies)
// ---------------------------------------------------------------------------
internal readonly record struct NvClockDomainInfo(
    bool IsPresent,
    uint FrequencyKHz);

internal readonly record struct NvClockFrequencies(
    NvClockDomainInfo Graphics,
    NvClockDomainInfo Memory,
    NvClockDomainInfo Video);

// ---------------------------------------------------------------------------
// Thermal sensor reading
// ---------------------------------------------------------------------------
internal readonly record struct NvThermalSensor(
    NvThermalController Controller,
    NvThermalTarget Target,
    int CurrentTemperature,
    int DefaultMaximumTemperature,
    int DefaultMinimumTemperature);

// ---------------------------------------------------------------------------
// Memory info
// ---------------------------------------------------------------------------
internal readonly record struct NvMemoryInfo(
    uint DedicatedVideoMemoryKb,
    uint AvailableDedicatedVideoMemoryKb,
    uint SystemVideoMemoryKb,
    uint SharedSystemMemoryKb,
    uint CurrentAvailableDedicatedVideoMemoryKb,
    NvGpuMemoryMaker RamMaker);

// ---------------------------------------------------------------------------
// Current performance state
// ---------------------------------------------------------------------------
internal readonly record struct NvCurrentPerformanceState(
    NvPerformanceStateId StateId);

// ---------------------------------------------------------------------------
// Performance States 2.0 (overclock read/write)
// ---------------------------------------------------------------------------
internal readonly record struct NvPstate20ParameterDelta(int DeltaValue);

internal readonly record struct NvPstate20ClockEntry(
    NvPerformanceStateId StateId,
    NvPublicClockDomain DomainId,
    NvPstate20ParameterDelta FrequencyDeltaInKHz);

internal readonly record struct NvPstate20Info(
    NvPstate20ClockEntry[] Clocks);

// ---------------------------------------------------------------------------
// Helper: NVAPI version macro
// ---------------------------------------------------------------------------
internal static class NvApiVersion
{
    /// <summary>
    /// MAKE_NVAPI_VERSION: encodes struct size and version into a uint32.
    /// Bits [15:0] = size in bytes, bits [31:16] = version number.
    /// </summary>
    public static uint Make(int structSize, int version) =>
        (uint)((structSize & 0xFFFF) | ((version & 0xFFFF) << 16));

    public static uint Make<T>(int version) where T : struct =>
        Make(Marshal.SizeOf<T>(), version);
}
