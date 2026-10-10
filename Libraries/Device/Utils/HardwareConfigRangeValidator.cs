using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.Utils;

/// <summary>
/// Validates God Mode / fan-curve / GPU overclock settings against device-style
/// ranges before any hardware write. Illegal configurations are backed up and
/// the caller must skip applying them to hardware.
/// </summary>
public static class HardwareConfigRangeValidator
{
    public const int DefaultMaxGpuCoreDeltaMhz = 500;
    public const int DefaultMaxGpuMemoryDeltaMhz = 1500;
    public const int MinFanTemperatureC = 0;
    public const int MaxFanTemperatureC = 120;
    public const int MinFanTargetPercent = 0;
    public const int MaxFanTargetPercent = 100;
    public const int MinCriticalTempC = 50;
    public const int MaxCriticalTempC = 120;

    /// <summary>
    /// Returns true when the stepper value is within its embedded device range
    /// (Min/Max or discrete Steps). Null values and steppers with no range
    /// metadata are treated as valid (device limits unknown yet).
    /// </summary>
    public static bool IsStepperInDeviceRange(StepperValue? value)
    {
        if (value is null)
            return true;

        var v = value.Value;
        if (v.Steps is { Length: > 0 })
            return v.Steps.Contains(v.Value);

        // Min=Max=0 with no steps usually means "range not yet loaded from device".
        if (v.Min == 0 && v.Max == 0)
            return true;

        return v.Value >= v.Min && v.Value <= v.Max;
    }

    public static bool IsGpuOverclockInRange(
        GPUOverclockInfo info,
        int maxCoreDeltaMhz = DefaultMaxGpuCoreDeltaMhz,
        int maxMemoryDeltaMhz = DefaultMaxGpuMemoryDeltaMhz)
    {
        if (maxCoreDeltaMhz < 0 || maxMemoryDeltaMhz < 0)
            throw new ArgumentOutOfRangeException(nameof(maxCoreDeltaMhz));

        return info.CoreDeltaMhz >= 0
               && info.CoreDeltaMhz <= maxCoreDeltaMhz
               && info.MemoryDeltaMhz >= 0
               && info.MemoryDeltaMhz <= maxMemoryDeltaMhz;
    }

    public static bool IsFanCurveEntryInRange(FanCurveEntry? entry)
    {
        if (entry is null)
            return false;

        if (entry.CriticalTemp < MinCriticalTempC || entry.CriticalTemp > MaxCriticalTempC)
            return false;

        if (entry.CurveNodes is null || entry.CurveNodes.Count == 0)
            return false;

        foreach (var node in entry.CurveNodes)
        {
            if (node.Temperature < MinFanTemperatureC || node.Temperature > MaxFanTemperatureC)
                return false;
            if (node.TargetPercent < MinFanTargetPercent || node.TargetPercent > MaxFanTargetPercent)
                return false;
        }

        return true;
    }

    public static bool IsGodModePresetInRange(GodModeSettings.GodModeSettingsStore.Preset? preset)
    {
        if (preset is null)
            return false;

        StepperValue?[] steppers =
        [
            preset.CPULongTermPowerLimit,
            preset.CPUShortTermPowerLimit,
            preset.CPUPeakPowerLimit,
            preset.CPUCrossLoadingPowerLimit,
            preset.CPUPL1Tau,
            preset.APUsPPTPowerLimit,
            preset.CPUTemperatureLimit,
            preset.GPUPowerBoost,
            preset.GPUConfigurableTGP,
            preset.GPUTemperatureLimit,
            preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
            preset.GPUToCPUDynamicBoost,
        ];

        foreach (var stepper in steppers)
        {
            if (!IsStepperInDeviceRange(stepper))
                return false;
        }

        if (preset.MinValueOffset is < -100 or > 100)
            return false;
        if (preset.MaxValueOffset is < -100 or > 500)
            return false;

        return true;
    }

    public static bool IsGodModeStoreInRange(GodModeSettings.GodModeSettingsStore? store)
    {
        if (store?.Presets is null || store.Presets.Count == 0)
            return true;

        return store.Presets.Values.All(IsGodModePresetInRange);
    }

    public static bool AreFanCurveEntriesInRange(IEnumerable<FanCurveEntry>? entries)
    {
        if (entries is null)
            return true;

        return entries.All(IsFanCurveEntryInRange);
    }

    /// <summary>
    /// Validates the live God Mode store. On failure, moves <c>godmode.json</c>
    /// to a timestamped backup and returns false so callers skip hardware writes.
    /// </summary>
    public static bool TryValidateOrBackupGodMode(
        GodModeSettings? settings,
        Func<string, bool>? tryBackupFile = null,
        Action<string>? log = null)
    {
        if (settings?.Store is null)
            return true;

        if (IsGodModeStoreInRange(settings.Store))
            return true;

        log?.Invoke("God Mode preset values are outside device range; backing up and refusing hardware apply.");
        BackupSettingsFile("godmode.json", tryBackupFile, log);
        return false;
    }

    /// <summary>
    /// Validates GPU OC deltas against controller limits. On failure, backs up
    /// <c>gpu_oc.json</c>, disables OC in the store, and returns false.
    /// </summary>
    public static bool TryValidateOrBackupGpuOverclock(
        GPUOverclockController? controller,
        GPUOverclockSettings? settings,
        int maxCoreDeltaMhz = DefaultMaxGpuCoreDeltaMhz,
        int maxMemoryDeltaMhz = DefaultMaxGpuMemoryDeltaMhz,
        Func<string, bool>? tryBackupFile = null,
        Action<string>? log = null)
    {
        if (controller is null && settings is null)
            return true;

        var enabled = settings?.Store.Enabled ?? false;
        var info = settings?.Store.Info ?? GPUOverclockInfo.Zero;

        if (controller is not null)
        {
            var state = controller.GetState();
            enabled = state.Item1;
            info = state.Item2;
        }

        if (!enabled)
            return true;

        if (IsGpuOverclockInRange(info, maxCoreDeltaMhz, maxMemoryDeltaMhz))
            return true;

        log?.Invoke(
            $"GPU overclock out of range (core={info.CoreDeltaMhz}, mem={info.MemoryDeltaMhz}); " +
            "backing up and refusing hardware apply.");
        BackupSettingsFile("gpu_oc.json", tryBackupFile, log);

        try
        {
            controller?.SaveState(false, GPUOverclockInfo.Zero);
            if (settings is not null)
            {
                settings.Store.Enabled = false;
                settings.Store.Info = GPUOverclockInfo.Zero;
                settings.SynchronizeStore();
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to disable invalid GPU OC settings: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Validates fan curve entries. On failure, backs up <c>fan_curves.json</c>
    /// and clears entries so no illegal curve is written to the EC.
    /// </summary>
    public static bool TryValidateOrBackupFanCurves(
        FanCurveSettings? settings,
        Func<string, bool>? tryBackupFile = null,
        Action<string>? log = null)
    {
        if (settings?.Store?.Entries is null || settings.Store.Entries.Count == 0)
            return true;

        if (AreFanCurveEntriesInRange(settings.Store.Entries))
            return true;

        log?.Invoke("Fan curve entries are outside device range; backing up and refusing hardware apply.");
        BackupSettingsFile("fan_curves.json", tryBackupFile, log);

        try
        {
            settings.Store.Entries.Clear();
            settings.SynchronizeStore();
        }
        catch (Exception ex)
        {
            log?.Invoke($"Failed to clear invalid fan curves: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    private static void BackupSettingsFile(string filename, Func<string, bool>? tryBackupFile, Action<string>? log)
    {
        try
        {
            if (tryBackupFile is not null)
            {
                tryBackupFile(filename);
                return;
            }

            var source = Path.Combine(Folders.AppData, filename);
            if (!File.Exists(source))
                return;

            var backup = Path.Combine(
                Folders.AppData,
                $"{filename}.bak.invalid.{DateTime.UtcNow:yyyyMMddHHmmss}");
            File.Copy(source, backup, overwrite: false);
            log?.Invoke($"Backed up invalid config to {backup}");
        }
        catch (Exception ex)
        {
            log?.Invoke($"Backup of '{filename}' failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
