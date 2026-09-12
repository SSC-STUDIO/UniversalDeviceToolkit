using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UniversalDeviceToolkit.Lib.System;

/// <summary>
/// Low-level P/Invoke layer for NVIDIA NVAPI.
/// NVAPI functions are resolved at runtime via nvapi_QueryInterface(functionId),
/// then cached as delegates. This avoids static DllImport linkage which would
/// fail on systems without nvapi64.dll.
/// </summary>
internal static class NvApiInterop
{
    private const string NvApiDll = "nvapi64.dll";
    private const int NVAPI_MAX_PHYSICAL_GPUS = 64;
    private const int NVAPI_MAX_THERMAL_SENSORS_PER_GPU = 3;

    // -----------------------------------------------------------------------
    // NVAPI function IDs (from nvapi.h / NvAPIWrapper.Net decompilation)
    // -----------------------------------------------------------------------
    private static class Ids
    {
        public const uint Initialize                   = 0x0150E828;
        public const uint Unload                       = 0xD22BDD7E;
        public const uint EnumPhysicalGPUs             = 0xE5AC921F;
        public const uint GPU_GetSystemType            = 0xBAAABFCC;
        public const uint GetGPUIDfromPhysicalGPU      = 0x6533EA3E;
        public const uint GPU_GetPCIIdentifiers        = 0x2DDFB66E;
        public const uint EnumNvidiaDisplayHandle      = 0x9ABDD40D;
        public const uint GetPhysicalGPUsFromDisplay   = 0x34EF9506;
        public const uint GPU_GetCurrentPstate         = 0x927DA4F6;
        public const uint GPU_QueryActiveApps          = 0x65B1C5F5;
        public const uint GPU_GetDynamicPstatesInfoEx  = 0x60DED2ED;
        public const uint GPU_GetAllClockFrequencies   = 0xDCB616C3;
        public const uint GPU_GetThermalSettings       = 0xE3640A56;
        public const uint GPU_GetMemoryInfo            = 0x07F9B368;
        public const uint GPU_GetPstates20             = 0x6FF81213;
        public const uint GPU_SetPstates20             = 0x0F4DAE6B;
        public const uint GPU_ClientPowerPoliciesGetStatus  = 0x70916171;
        public const uint GPU_ClientVoltageSensorsGetStatus = 0x6C4E1284;
        public const uint GPU_ClientPowerTopologyGetStatus  = 0xEC1D4E2D;
    }

    // -----------------------------------------------------------------------
    // QueryInterface — the single entry point used to resolve all other functions
    // -----------------------------------------------------------------------
    [DllImport(NvApiDll, EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint QueryInterface(uint functionId);

    // -----------------------------------------------------------------------
    // Delegate cache
    // -----------------------------------------------------------------------
    private static readonly ConcurrentDictionary<uint, Delegate> _cache = new();

    private static T GetFunction<T>(uint id) where T : Delegate
    {
        var d = _cache.GetOrAdd(id, static key =>
        {
            var ptr = QueryInterface(key);
            if (ptr == nint.Zero)
                throw new InvalidOperationException($"NVAPI function 0x{key:X8} not available on this system.");
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        });
        return (T)d;
    }

    // -----------------------------------------------------------------------
    // Native delegate signatures
    // -----------------------------------------------------------------------

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UnloadDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate int EnumPhysicalGPUsDelegate(nint* gpuHandles, ref uint gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetSystemTypeDelegate(nint gpuHandle, out NvSystemType systemType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetGPUIDDelegate(nint gpuHandle, out uint gpuId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPCIIdentifiersDelegate(
        nint gpuHandle, out uint deviceId, out uint subSystemId, out uint revisionId, out uint vendorId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumNvidiaDisplayHandleDelegate(uint index, out nint displayHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate int GetPhysicalGPUsFromDisplayDelegate(
        nint displayHandle, nint* gpuHandles, ref uint gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetCurrentPstateDelegate(nint gpuHandle, out uint performanceStateId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate int QueryActiveAppsDelegate(nint gpuHandle, nint apps, ref uint appCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetDynamicPstatesDelegate(nint gpuHandle, ref NvDynamicPstateInfoNative info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetAllClockFrequenciesDelegate(nint gpuHandle, ref NvClockFrequenciesNative info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetThermalSettingsDelegate(
        nint gpuHandle, uint sensorIndex, ref NvThermalSettingsNative info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetMemoryInfoDelegate(nint gpuHandle, ref NvMemoryInfoNative info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetPstates20Delegate(nint gpuHandle, ref NvPstates20Native info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetPstates20Delegate(nint gpuHandle, ref NvPstates20Native info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ClientPowerPoliciesGetStatusDelegate(
        nint gpuHandle, ref NvClientPowerPoliciesStatusNative info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ClientVoltageSensorsGetStatusDelegate(
        nint gpuHandle, ref NvClientVoltageSensorsStatusNative info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ClientPowerTopologyGetStatusDelegate(
        nint gpuHandle, ref NvClientPowerTopologyStatusNative info);

    // -----------------------------------------------------------------------
    // Native struct layouts (must match NVAPI binary ABI exactly)
    // -----------------------------------------------------------------------

    // NvDynamicPstateInfo: version 0x10000 + utilization[NVAPI_MAX_GPU_UTILIZATIONS=8].
    // Each utilization entry is a (bIsPresent, percentage) PAIR — the percentage ([0-100]) is the
    // busy time for that domain. The previous layout declared 8 consecutive percentage fields,
    // which read each domain's bIsPresent flag (0/1) instead of its real utilization, and had the
    // wrong struct size (40 bytes instead of 72), so NVAPI either erred or returned garbage.
    [StructLayout(LayoutKind.Sequential)]
    private struct NvDynamicPstateInfoNative
    {
        public uint Version;
        public uint Flags;
        public uint GpuPresent;         // domain 0: GPU — bIsPresent
        public uint GpuUtilization;     // domain 0: GPU — percentage
        public uint FbPresent;          // domain 1: FrameBuffer — bIsPresent
        public uint FbUtilization;      // domain 1: FrameBuffer — percentage
        public uint VidPresent;         // domain 2: Video Engine — bIsPresent
        public uint VidUtilization;     // domain 2: Video Engine — percentage
        public uint BusPresent;         // domain 3: Bus — bIsPresent
        public uint BusUtilization;     // domain 3: Bus — percentage
        public uint Reserved4Present;   // domain 4
        public uint Reserved4;
        public uint Reserved5Present;   // domain 5
        public uint Reserved5;
        public uint Reserved6Present;   // domain 6
        public uint Reserved6;
        public uint Reserved7Present;   // domain 7
        public uint Reserved7;
    }

    // NvClockFrequencies: version 0x30000 + uint clockType + 32 clock entries
    // clockType: 0=CurrentFreq, 1=BaseClock, 2=BoostClock (NV_GPU_CLOCK_FREQUENCIES_v3)
    // Each clock entry: uint bIsPresent(1) + uint frequency(31 bits MHz)
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NvClockFrequenciesNative
    {
        public uint Version;
        public uint ClockType; // NV_GPU_CLOCK_FREQUENCIES_CLOCK_TYPE_*
        public fixed uint Clocks[64]; // 32 entries × 2 uints each (isPresent+freq)
    }

    // Thermal sensor entry within NvThermalSettings
    [StructLayout(LayoutKind.Sequential)]
    private struct NvThermalSensorNative
    {
        public uint Controller;
        public uint DefaultMinTemp;
        public uint DefaultMaxTemp;
        public uint CurrentTemp;
        public uint Target;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
    }

    // NvThermalSettings: version + count + array of sensor entries
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NvThermalSettingsNative
    {
        public uint Version;
        public uint Count;
        public fixed byte Sensors[NVAPI_MAX_THERMAL_SENSORS_PER_GPU * 32]; // 3 sensors × 32 bytes each
    }

    // NvMemoryInfo v2 (0x20000)
    [StructLayout(LayoutKind.Sequential)]
    private struct NvMemoryInfoNative
    {
        public uint Version;
        public uint DedicatedVideoMemory;       // KB
        public uint AvailableDedicatedVideoMemory; // KB
        public uint SystemVideoMemory;          // KB
        public uint SharedSystemMemory;         // KB
        public uint CurrentAvailableDedicatedVideoMemory; // KB
    }

    // NvPstates20Info v1 — variable-length struct with clock/voltage entries
    // Layout: version(4) + numPstates(4) + numClocks(4) + reserved(4)
    //         + pstateInfo[numPstates × (4+4+4)] (stateId + numClocks + flags per state)
    //         + clockEntries[numClocks × (4+4+4+4)] (stateId+domainId+deltaValue+flags per clock)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private unsafe struct NvPstates20Native
    {
        public uint Version;
        public uint NumPstates;
        public uint NumClocks;
        public uint Reserved;
        // Followed by variable-length data; we use a fixed buffer for the common case
        // Max 4 pstates × 3 entries + max 8 clock entries × 4 uints
        public fixed uint Data[64];
    }

    // Client power policies status
    [StructLayout(LayoutKind.Sequential)]
    private struct NvClientPowerPoliciesStatusNative
    {
        public uint Version;
        public uint PowerInMilliwatts;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
    }

    // Client voltage sensors status
    [StructLayout(LayoutKind.Sequential)]
    private struct NvClientVoltageSensorsStatusNative
    {
        public uint Version;
        public uint CurrentVoltageMillivolts;
        public uint Reserved0;
        public uint Reserved1;
    }

    // Client power topology status — up to 4 entries
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct NvClientPowerTopologyStatusNative
    {
        public uint Version;
        public uint Count;
        // Each entry: domain(4) + powerUsageInPCM(4)
        public fixed uint Entries[16]; // 4 entries × 4 uints each (domain, usage, reserved0, reserved1)
    }

    // -----------------------------------------------------------------------
    // Status check helper
    // -----------------------------------------------------------------------
    private static void CheckStatus(int status, string functionName)
    {
        if (status != (int)NvApiStatus.Ok)
        {
            var statusEnum = (NvApiStatus)status;
            throw new InvalidOperationException(
                $"NVAPI {functionName} failed with status {statusEnum} (0x{status:X8}).");
        }
    }

    /// <summary>
    /// Soft status check: returns true on success, false for expected/soft errors
    /// (e.g. GPU_NOT_POWERED), and throws for unexpected failures.
    /// </summary>
    private static bool CheckStatusSoft(int status, string functionName)
    {
        if (status == (int)NvApiStatus.Ok)
            return true;

        var statusEnum = (NvApiStatus)status;
        // Expected soft failures — return false instead of throwing
        if (statusEnum is NvApiStatus.GpuNotPowered
            or NvApiStatus.NotSupported
            or NvApiStatus.GpuNotFound)
            return false;

        throw new InvalidOperationException(
            $"NVAPI {functionName}: {statusEnum} (0x{status:X8}).");
    }

    // =======================================================================
    // Public API surface
    // =======================================================================

    public static void Initialize()
    {
        var fn = GetFunction<InitializeDelegate>(Ids.Initialize);
        CheckStatus(fn(), nameof(Initialize));
    }

    public static void Unload()
    {
        try
        {
            var fn = GetFunction<UnloadDelegate>(Ids.Unload);
            fn();
        }
        catch (DllNotFoundException)
        {
            // nvapi64.dll not present — silently ignore
        }
    }

    public static NvPhysicalGpuHandle[] EnumPhysicalGPUs()
    {
        var fn = GetFunction<EnumPhysicalGPUsDelegate>(Ids.EnumPhysicalGPUs);
        unsafe
        {
            var handles = stackalloc nint[NVAPI_MAX_PHYSICAL_GPUS];
            uint count = NVAPI_MAX_PHYSICAL_GPUS;
            CheckStatus(fn(handles, ref count), nameof(EnumPhysicalGPUs));

            var result = new NvPhysicalGpuHandle[count];
            for (int i = 0; i < count; i++)
                result[i] = new NvPhysicalGpuHandle(handles[i]);
            return result;
        }
    }

    public static NvSystemType GetSystemType(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetSystemTypeDelegate>(Ids.GPU_GetSystemType);
        CheckStatus(fn(gpu.Value, out var systemType), nameof(GetSystemType));
        return systemType;
    }

    public static uint GetGPUID(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetGPUIDDelegate>(Ids.GetGPUIDfromPhysicalGPU);
        CheckStatus(fn(gpu.Value, out var gpuId), nameof(GetGPUID));
        return gpuId;
    }

    public static NvPciIdentifiers GetPCIIdentifiers(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetPCIIdentifiersDelegate>(Ids.GPU_GetPCIIdentifiers);
        CheckStatus(fn(gpu.Value, out var deviceId, out var subSystemId, out var revisionId, out var vendorId),
            nameof(GetPCIIdentifiers));
        return new NvPciIdentifiers
        {
            DeviceId = deviceId,
            SubSystemId = subSystemId,
            RevisionId = revisionId,
            VendorId = vendorId,
        };
    }

    public static NvDisplayHandle[] EnumNvidiaDisplayHandles()
    {
        var fn = GetFunction<EnumNvidiaDisplayHandleDelegate>(Ids.EnumNvidiaDisplayHandle);
        var handles = new List<NvDisplayHandle>();
        for (uint i = 0; i < 64; i++)
        {
            var status = fn(i, out var handle);
            if (status != (int)NvApiStatus.Ok)
                break;
            if (handle != nint.Zero)
                handles.Add(new NvDisplayHandle(handle));
        }
        return handles.ToArray();
    }

    public static NvPhysicalGpuHandle[] GetPhysicalGPUsFromDisplay(NvDisplayHandle display)
    {
        var fn = GetFunction<GetPhysicalGPUsFromDisplayDelegate>(Ids.GetPhysicalGPUsFromDisplay);
        unsafe
        {
            var handles = stackalloc nint[NVAPI_MAX_PHYSICAL_GPUS];
            uint count = NVAPI_MAX_PHYSICAL_GPUS;
            CheckStatus(fn(display.Value, handles, ref count), nameof(GetPhysicalGPUsFromDisplay));

            var result = new NvPhysicalGpuHandle[count];
            for (int i = 0; i < count; i++)
                result[i] = new NvPhysicalGpuHandle(handles[i]);
            return result;
        }
    }

    public static NvPerformanceStateId GetCurrentPstate(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetCurrentPstateDelegate>(Ids.GPU_GetCurrentPstate);
        if (!CheckStatusSoft(fn(gpu.Value, out var stateId), nameof(GetCurrentPstate)))
            return NvPerformanceStateId.Undefined;
        return (NvPerformanceStateId)stateId;
    }

    public static NvActiveAppV2[] QueryActiveApps(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<QueryActiveAppsDelegate>(Ids.GPU_QueryActiveApps);

        const int maxApps = 32;
        // NvActiveAppV2 layout: version(4) + processId(4) + deviceId(4) + processName(64) = 76 bytes
        const int entrySize = 76;
        var bufferSize = entrySize * maxApps;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            // Zero the buffer and set version for each slot
            unsafe
            {
                var p = (byte*)buffer;
                for (int i = 0; i < bufferSize; i++)
                    p[i] = 0;
            }

            var version = NvApiVersion.Make(entrySize, 2);
            for (int i = 0; i < maxApps; i++)
            {
                Marshal.WriteInt32(buffer, i * entrySize, (int)version);
            }

            uint count = (uint)maxApps;
            var status = fn(gpu.Value, buffer, ref count);
            if (status != (int)NvApiStatus.Ok)
                return [];

            var result = new NvActiveAppV2[count];
            for (int i = 0; i < count; i++)
            {
                var offset = i * entrySize;
                var processId = (uint)Marshal.ReadInt32(buffer, offset + 4);
                var deviceId = (uint)Marshal.ReadInt32(buffer, offset + 8);
                // Process name is at offset+12, 64 ANSI bytes
                var namePtr = buffer + offset + 12;
                var processName = Marshal.PtrToStringAnsi(namePtr, 64)?.TrimEnd('\0') ?? "";
                result[i] = new NvActiveAppV2
                {
                    Version = version,
                    ProcessId = processId,
                    DeviceId = deviceId,
                    ProcessName = processName,
                };
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static NvDynamicPstateInfo GetDynamicPstatesInfo(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetDynamicPstatesDelegate>(Ids.GPU_GetDynamicPstatesInfoEx);
        var native = new NvDynamicPstateInfoNative
        {
            Version = NvApiVersion.Make<NvDynamicPstateInfoNative>(1),
        };
        CheckStatus(fn(gpu.Value, ref native), nameof(GetDynamicPstatesInfo));
        return new NvDynamicPstateInfo
        {
            Version = native.Version,
            Flags = native.Flags,
            GpuUtilization = native.GpuPresent != 0 ? native.GpuUtilization : 0,
            FbUtilization = native.FbPresent != 0 ? native.FbUtilization : 0,
            VidUtilization = native.VidPresent != 0 ? native.VidUtilization : 0,
            BusUtilization = native.BusPresent != 0 ? native.BusUtilization : 0,
        };
    }

    // NV_GPU_CLOCK_FREQUENCIES clock type constants
    private const uint NV_GPU_CLOCK_FREQUENCIES_CURRENT_FREQ = 0;
    private const uint NV_GPU_CLOCK_FREQUENCIES_BASE_CLOCK  = 1;
    private const uint NV_GPU_CLOCK_FREQUENCIES_BOOST_CLOCK  = 2;

    public static NvClockFrequencies GetAllClockFrequencies(NvPhysicalGpuHandle gpu, uint clockType = NV_GPU_CLOCK_FREQUENCIES_CURRENT_FREQ)
    {
        var fn = GetFunction<GetAllClockFrequenciesDelegate>(Ids.GPU_GetAllClockFrequencies);
        unsafe
        {
            var native = new NvClockFrequenciesNative
            {
                Version = NvApiVersion.Make<NvClockFrequenciesNative>(3),
                ClockType = clockType,
            };
            CheckStatus(fn(gpu.Value, ref native), nameof(GetAllClockFrequencies));

            static NvClockDomainInfo ReadDomain(uint* clocks, int index)
            {
                var isPresent = clocks[index * 2] != 0;
                // NV_GPU_CLOCK_FREQUENCIES v3 stores the raw frequency directly in kHz —
                // no conversion needed.  Earlier code mistakenly multiplied by 1000,
                // inflating every clock reading by 1000× (765 MHz displayed as 765 GHz).
                var freqKHz = clocks[index * 2 + 1] & 0x7FFFFFFFu;
                return new NvClockDomainInfo(isPresent, freqKHz);
            }

            return new NvClockFrequencies(
                ReadDomain(native.Clocks, (int)NvPublicClockDomain.Graphics),
                ReadDomain(native.Clocks, (int)NvPublicClockDomain.Memory),
                ReadDomain(native.Clocks, (int)NvPublicClockDomain.Video));
        }
    }

    public static NvThermalSensor[] GetThermalSettings(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetThermalSettingsDelegate>(Ids.GPU_GetThermalSettings);
        unsafe
        {
            var native = new NvThermalSettingsNative
            {
                Version = NvApiVersion.Make(sizeof(NvThermalSettingsNative), 2),
            };
            // Query all sensors (index = 15 means "all")
            CheckStatus(fn(gpu.Value, 15, ref native), nameof(GetThermalSettings));

            var sensors = new NvThermalSensor[native.Count];
            for (int i = 0; i < native.Count; i++)
            {
                var s = (NvThermalSensorNative*)(native.Sensors + i * sizeof(NvThermalSensorNative));
                sensors[i] = new NvThermalSensor(
                    (NvThermalController)s->Controller,
                    (NvThermalTarget)s->Target,
                    (int)s->CurrentTemp,
                    (int)s->DefaultMaxTemp,
                    (int)s->DefaultMinTemp);
            }
            return sensors;
        }
    }

    public static NvMemoryInfo GetMemoryInfo(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetMemoryInfoDelegate>(Ids.GPU_GetMemoryInfo);
        var native = new NvMemoryInfoNative
        {
            Version = NvApiVersion.Make<NvMemoryInfoNative>(2),
        };
        CheckStatus(fn(gpu.Value, ref native), nameof(GetMemoryInfo));
        return new NvMemoryInfo(
            native.DedicatedVideoMemory,
            native.AvailableDedicatedVideoMemory,
            native.SystemVideoMemory,
            native.SharedSystemMemory,
            native.CurrentAvailableDedicatedVideoMemory,
            NvGpuMemoryMaker.Unknown); // RamMaker requires separate NVAPI_PRIVATE call
    }

    public static NvPstate20Info GetPstates20(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<GetPstates20Delegate>(Ids.GPU_GetPstates20);
        unsafe
        {
            var native = new NvPstates20Native
            {
                Version = NvApiVersion.Make(sizeof(NvPstates20Native), 1),
            };
            CheckStatus(fn(gpu.Value, ref native), nameof(GetPstates20));
            return ParsePstates20(ref native);
        }
    }

    public static void SetPstates20(NvPhysicalGpuHandle gpu, NvPstate20Info info)
    {
        var fn = GetFunction<SetPstates20Delegate>(Ids.GPU_SetPstates20);
        unsafe
        {
            var native = BuildPstates20Native(info);
            CheckStatus(fn(gpu.Value, ref native), nameof(SetPstates20));
        }
    }

    public static int GetClientPowerInWatts(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<ClientPowerPoliciesGetStatusDelegate>(Ids.GPU_ClientPowerPoliciesGetStatus);
        var native = new NvClientPowerPoliciesStatusNative
        {
            Version = NvApiVersion.Make<NvClientPowerPoliciesStatusNative>(1),
        };
        CheckStatus(fn(gpu.Value, ref native), nameof(GetClientPowerInWatts));
        return (int)(native.PowerInMilliwatts / 1000);
    }

    public static double GetClientVoltageInVolts(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<ClientVoltageSensorsGetStatusDelegate>(Ids.GPU_ClientVoltageSensorsGetStatus);
        var native = new NvClientVoltageSensorsStatusNative
        {
            Version = NvApiVersion.Make<NvClientVoltageSensorsStatusNative>(1),
        };
        CheckStatus(fn(gpu.Value, ref native), nameof(GetClientVoltageInVolts));
        return native.CurrentVoltageMillivolts / 1000.0;
    }

    public static (int wattage, bool found) GetWattageFromPowerTopology(NvPhysicalGpuHandle gpu)
    {
        var fn = GetFunction<ClientPowerTopologyGetStatusDelegate>(Ids.GPU_ClientPowerTopologyGetStatus);
        unsafe
        {
            var native = new NvClientPowerTopologyStatusNative
            {
                Version = NvApiVersion.Make<NvClientPowerTopologyStatusNative>(1),
            };
            CheckStatus(fn(gpu.Value, ref native), nameof(GetWattageFromPowerTopology));

            // Each entry: domain(4 bytes) + usageInPCM(4 bytes) + reserved(4) + reserved(4)
            for (int i = 0; i < native.Count && i < 4; i++)
            {
                var domain = native.Entries[i * 4];
                var usagePCM = native.Entries[i * 4 + 1];
                // domain 0 = GPU, domain 1 = Board (NVAPI_PRIVATE enum)
                if ((domain == 0 || domain == 1) && usagePCM > 0)
                    return ((int)(usagePCM / 1000), true);
            }
            return (-1, false);
        }
    }

    // -----------------------------------------------------------------------
    // P-states 2.0 parsing helpers
    // -----------------------------------------------------------------------

    private static unsafe NvPstate20Info ParsePstates20(ref NvPstates20Native native)
    {
        // Data layout after the 4 header uints:
        //   pstateEntries[numPstates]: each = (stateId: uint, numClocks: uint, flags: uint)  → 3 uints
        //   clockEntries[numClocks]:   each = (stateId: uint, domainId: uint, freqDeltaKHz: int, flags: uint) → 4 uints
        var clocks = new List<NvPstate20ClockEntry>();

        int offset = 0;
        // Skip pstate info entries (3 uints each: stateId + numClocks + flags)
        var numPstates = (int)native.NumPstates;
        if (numPstates < 0 || numPstates > 16)
            numPstates = 0;
        offset += numPstates * 3;

        // Read clock entries
        for (int i = 0; i < native.NumClocks && offset + 2 < 64; i++)
        {
            var stateId = (NvPerformanceStateId)native.Data[offset];
            var domainId = (NvPublicClockDomain)native.Data[offset + 1];
            var deltaKHz = (int)native.Data[offset + 2];
            clocks.Add(new NvPstate20ClockEntry(stateId, domainId, new NvPstate20ParameterDelta(deltaKHz)));
            offset += 4; // Each clock entry is 4 uints (stateId, domainId, deltaKHz, reserved/flags)
        }

        return new NvPstate20Info(clocks.ToArray());
    }

    private static unsafe NvPstates20Native BuildPstates20Native(NvPstate20Info info)
    {
        var native = new NvPstates20Native
        {
            Version = NvApiVersion.Make(sizeof(NvPstates20Native), 1),
            NumPstates = 1,
            NumClocks = (uint)info.Clocks.Length,
        };

        int offset = 0;
        // Write one pstate info entry (P0_3DPerformance): stateId + numClocks + flags
        native.Data[offset++] = (uint)NvPerformanceStateId.P0_3DPerformance; // stateId
        native.Data[offset++] = (uint)info.Clocks.Length;                    // numClocks
        native.Data[offset++] = 0;                                           // flags/editable

        // Write clock entries
        for (int i = 0; i < info.Clocks.Length && offset + 3 < 64; i++)
        {
            native.Data[offset++] = (uint)info.Clocks[i].StateId;
            native.Data[offset++] = (uint)info.Clocks[i].DomainId;
            native.Data[offset++] = (uint)info.Clocks[i].FrequencyDeltaInKHz.DeltaValue;
            native.Data[offset++] = 0; // reserved/flags
        }

        return native;
    }
}
