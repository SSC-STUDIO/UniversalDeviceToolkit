using System;
using System.Collections.Generic;
using System.Linq;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System;

/// <summary>
/// NVAPI facade providing high-level access to NVIDIA GPU information.
/// Replaces the previous NvAPIWrapper.Net dependency with hand-written P/Invoke
/// over nvapi64.dll via <see cref="NvApiInterop"/>.
/// </summary>
internal static class NVAPI
{
    public static void Initialize() => NvApiInterop.Initialize();

    public static void Unload() => NvApiInterop.Unload();

    /// <summary>
    /// Returns the first laptop (discrete) GPU, or null if none is found.
    /// </summary>
    public static NvPhysicalGpuHandle? GetGPU()
    {
        try
        {
            var gpus = NvApiInterop.EnumPhysicalGPUs();
            foreach (var gpu in gpus)
            {
                try
                {
                    if (NvApiInterop.GetSystemType(gpu) == NvSystemType.Laptop)
                        return gpu;
                }
                catch
                {
                    // Skip GPUs whose system type cannot be read
                }
            }
            return gpus.Length > 0 ? gpus[0] : null;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-get-gpu", "NVAPI GetPhysicalGPUs failed (driver unloaded or unsupported).", ex);
            return null;
        }
    }

    /// <summary>
    /// Returns true if any NVIDIA display is driven by the given GPU.
    /// </summary>
    public static bool IsDisplayConnected(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var displayHandles = NvApiInterop.EnumNvidiaDisplayHandles();
            foreach (var display in displayHandles)
            {
                try
                {
                    var gpusForDisplay = NvApiInterop.GetPhysicalGPUsFromDisplay(display);
                    if (gpusForDisplay.Any(g => g.Value == gpu.Value))
                        return true;
                }
                catch
                {
                    // Skip displays whose GPU mapping cannot be resolved
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-display-connected", "NVAPI display connection probe failed.", ex);
            return false;
        }
    }

    /// <summary>
    /// Returns the PCI identifier string for the GPU, suitable for WMI PnP device ID lookup.
    /// </summary>
    public static string? GetGPUId(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NvApiInterop.GetPCIIdentifiers(gpu).ToString();
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-gpu-id", "NVAPI PCIIdentifiers read failed.", ex);
            return null;
        }
    }

    /// <summary>
    /// Returns active application processes running on the given GPU.
    /// </summary>
    public static NvActiveAppV2[] GetActiveApps(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NvApiInterop.QueryActiveApps(gpu);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-active-apps", "NVAPI QueryActiveApps failed.", ex);
            return [];
        }
    }

    /// <summary>
    /// Reads the current performance state of the GPU.
    /// Throws InvalidOperationException with "NVAPI_GPU_NOT_POWERED" message when the GPU is powered off.
    /// </summary>
    public static NvPerformanceStateId GetCurrentPstate(NvPhysicalGpuHandle gpu)
    {
        return NvApiInterop.GetCurrentPstate(gpu);
    }

    /// <summary>
    /// Returns GPU utilization as a percentage [0–100], combining GPU and video engine usage.
    /// </summary>
    public static int GetUsage(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var info = NvApiInterop.GetDynamicPstatesInfo(gpu);
            return Math.Min(100, Math.Max((int)info.GpuUtilization, (int)info.VidUtilization));
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-usage", "NVAPI usage read failed.", ex);
            return -1;
        }
    }

    /// <summary>
    /// Returns current clock frequencies (graphics and memory) in kHz.
    /// </summary>
    public static (int graphicsKHz, int memoryKHz) GetCurrentClockFrequencies(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var clocks = NvApiInterop.GetAllClockFrequencies(gpu, clockType: 0); // CURRENT_FREQ
            return ((int)clocks.Graphics.FrequencyKHz, (int)clocks.Memory.FrequencyKHz);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-current-clocks", "NVAPI current clock read failed.", ex);
            return (-1, -1);
        }
    }

    /// <summary>
    /// Returns boost (maximum) clock frequencies (graphics and memory) in kHz.
    /// Uses NV_GPU_CLOCK_FREQUENCIES_BOOST_CLOCK (2) to query boost clocks rather than current freq.
    /// </summary>
    public static (int graphicsKHz, int memoryKHz) GetBoostClockFrequencies(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var clocks = NvApiInterop.GetAllClockFrequencies(gpu, clockType: 2); // BOOST_CLOCK
            return ((int)clocks.Graphics.FrequencyKHz, (int)clocks.Memory.FrequencyKHz);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-boost-clocks", "NVAPI boost clock read failed.", ex);
            return (-1, -1);
        }
    }

    /// <summary>
    /// Returns thermal sensor readings for the GPU.
    /// </summary>
    public static NvThermalSensor[] GetThermalSensors(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NvApiInterop.GetThermalSettings(gpu);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-thermal", "NVAPI thermal read failed.", ex);
            return [];
        }
    }

    /// <summary>
    /// Returns memory information for the GPU.
    /// </summary>
    public static NvMemoryInfo GetMemoryInfo(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NvApiInterop.GetMemoryInfo(gpu);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-memory", "NVAPI memory info read failed.", ex);
            return default;
        }
    }

    /// <summary>
    /// Reads overclock P-states data (frequencies and deltas).
    /// Returns an array of clock entries; use <see cref="GetOverclockDelta"/> for a simplified view.
    /// </summary>
    public static NvPstate20Info GetPstates20(NvPhysicalGpuHandle gpu)
    {
        return NvApiInterop.GetPstates20(gpu);
    }

    /// <summary>
    /// Reads the overclock delta (in kHz) for graphics and memory clocks from P-states 2.0.
    /// </summary>
    public static (int graphicsDeltaKHz, int memoryDeltaKHz) GetOverclockDelta(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var info = NvApiInterop.GetPstates20(gpu);
            return ExtractOverclockDelta(info);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("nvapi-oc-read", "NVAPI overclock read failed.", ex);
            return (0, 0);
        }
    }

    /// <summary>
    /// Reads the overclock delta for a specific performance state (used by sensors for offset-adjusted clocks).
    /// Falls back to the first available entry if no clock entry matches the given stateId.
    /// </summary>
    public static (int graphicsDeltaKHz, int memoryDeltaKHz) GetOverclockDelta(NvPhysicalGpuHandle gpu, NvPerformanceStateId stateId)
    {
        try
        {
            var info = NvApiInterop.GetPstates20(gpu);
            return ExtractOverclockDelta(info, stateId);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// Applies overclock settings: core delta and memory delta in MHz.
    /// </summary>
    public static void SetOverclock(NvPhysicalGpuHandle gpu, int coreDeltaMhz, int memoryDeltaMhz)
    {
        var clockEntries = new[]
        {
            new NvPstate20ClockEntry(NvPerformanceStateId.P0_3DPerformance, NvPublicClockDomain.Graphics, new NvPstate20ParameterDelta(coreDeltaMhz * 1000)),
            new NvPstate20ClockEntry(NvPerformanceStateId.P0_3DPerformance, NvPublicClockDomain.Memory, new NvPstate20ParameterDelta(memoryDeltaMhz * 1000)),
        };
        var info = new NvPstate20Info(clockEntries);
        NvApiInterop.SetPstates20(gpu, info);
    }

    /// <summary>
    /// Reads current overclock core and memory deltas in MHz.
    /// </summary>
    public static (int coreDeltaMhz, int memoryDeltaMhz) GetOverclockInfo(NvPhysicalGpuHandle gpu)
    {
        var (gfxKHz, memKHz) = GetOverclockDelta(gpu);
        return (gfxKHz / 1000, memKHz / 1000);
    }

    /// <summary>
    /// Returns the GPU power consumption in watts, or -1 if unavailable.
    /// </summary>
    public static int GetWattage(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NvApiInterop.GetClientPowerInWatts(gpu);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Returns the GPU voltage in volts, or 0 if unavailable.
    /// </summary>
    public static double GetVoltage(NvPhysicalGpuHandle gpu)
    {
        try
        {
            return NvApiInterop.GetClientVoltageInVolts(gpu);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Returns GPU wattage from power topology as a fallback.
    /// </summary>
    public static int GetWattageFromPowerTopology(NvPhysicalGpuHandle gpu)
    {
        try
        {
            var (wattage, found) = NvApiInterop.GetWattageFromPowerTopology(gpu);
            return found ? wattage : -1;
        }
        catch
        {
            return -1;
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static (int graphicsKHz, int memoryKHz) ExtractOverclockDelta(NvPstate20Info info, NvPerformanceStateId? stateId = null)
    {
        // Filter by stateId if provided; fall back to first matching domain entry otherwise.
        var filtered = stateId.HasValue
            ? info.Clocks.Where(e => e.StateId == stateId.Value).ToArray()
            : info.Clocks;

        // If stateId filter yielded no results, fall back to all entries.
        if (filtered.Length == 0)
            filtered = info.Clocks;

        int gfxDelta = 0;
        int memDelta = 0;
        foreach (var entry in filtered)
        {
            if (entry.DomainId == NvPublicClockDomain.Graphics)
                gfxDelta = entry.FrequencyDeltaInKHz.DeltaValue;
            else if (entry.DomainId == NvPublicClockDomain.Memory)
                memDelta = entry.FrequencyDeltaInKHz.DeltaValue;
        }
        return (gfxDelta, memDelta);
    }
}
