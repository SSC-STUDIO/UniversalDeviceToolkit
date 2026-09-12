using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System;

public static class HardwareInventoryProvider
{
    public static async Task<HardwareInventory> ReadAsync()
    {
        var computerSystem = await ReadOrDefaultAsync(
            WMI.Win32.ComputerSystem.ReadAsync,
            ComputerSystemHardware.Empty,
            "Win32_ComputerSystem").ConfigureAwait(false);

        var baseBoard = await ReadOrDefaultAsync(
            WMI.Win32.BaseBoard.ReadAsync,
            BaseBoardHardware.Empty,
            "Win32_BaseBoard").ConfigureAwait(false);

        var chassis = await ReadOrDefaultAsync(
            WMI.Win32.SystemEnclosure.ReadAsync,
            ChassisHardware.Empty,
            "Win32_SystemEnclosure").ConfigureAwait(false);

        var processors = await ReadCollectionOrEmptyAsync(
            WMI.Win32.Processor.ReadAsync,
            "Win32_Processor").ConfigureAwait(false);

        var videoControllers = await ReadCollectionOrEmptyAsync(
            WMI.Win32.VideoController.ReadAsync,
            "Win32_VideoController").ConfigureAwait(false);

        var memoryModules = await ReadCollectionOrEmptyAsync(
            WMI.Win32.PhysicalMemory.ReadAsync,
            "Win32_PhysicalMemory").ConfigureAwait(false);

        var batteries = await ReadCollectionOrEmptyAsync(
            WMI.Win32.Battery.ReadAsync,
            "Win32_Battery").ConfigureAwait(false);

        return new HardwareInventory
        {
            ComputerSystem = computerSystem,
            BaseBoard = baseBoard,
            Chassis = chassis,
            Processors = processors,
            VideoControllers = videoControllers,
            Memory = CreateMemoryInformation(memoryModules),
            Batteries = batteries
        };
    }

    private static MemoryHardware CreateMemoryInformation(IReadOnlyCollection<MemoryModuleHardware> modules)
    {
        if (modules.Count == 0)
            return MemoryHardware.Empty;

        var speeds = modules.Select(module => module.SpeedMHz).Where(speed => speed.HasValue).Select(speed => speed!.Value).ToArray();
        var configuredSpeeds = modules.Select(module => module.ConfiguredClockSpeedMHz).Where(speed => speed.HasValue).Select(speed => speed!.Value).ToArray();

        return new MemoryHardware
        {
            TotalCapacityBytes = modules.Aggregate(0UL, (total, module) => total + module.CapacityBytes),
            ModuleCount = modules.Count,
            SpeedMHz = speeds.Length > 0 ? speeds.Max() : null,
            ConfiguredClockSpeedMHz = configuredSpeeds.Length > 0 ? configuredSpeeds.Max() : null
        };
    }

    private static async Task<T> ReadOrDefaultAsync<T>(Func<Task<T>> read, T fallback, string source)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Generic hardware inventory read failed for {source}.", ex);

            return fallback;
        }
    }

    private static async Task<IReadOnlyCollection<T>> ReadCollectionOrEmptyAsync<T>(Func<Task<IEnumerable<T>>> read, string source)
    {
        try
        {
            return (await read().ConfigureAwait(false)).ToArray();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Generic hardware inventory read failed for {source}.", ex);

            return [];
        }
    }
}
