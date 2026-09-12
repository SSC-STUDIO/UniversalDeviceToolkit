using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public enum FanSpeedSource
{
    Unavailable,
    LenovoCapability,
    LenovoGamezone,
    LenovoFanMethod,
    LibreHardwareMonitor
}

public readonly record struct FanSpeedReading(
    int Rpm,
    FanSpeedSource Source,
    bool IsExplicitlyStopped,
    string? FailureReason = null)
{
    public bool IsAvailable => Rpm >= 0;

    public static FanSpeedReading Available(int rpm, FanSpeedSource source) =>
        new(rpm, source, rpm == 0);

    public static FanSpeedReading Unavailable(FanSpeedSource source, string? reason = null) =>
        new(-1, source, false, reason);
}

public readonly record struct FanSpeedSourceReader(
    FanSpeedSource Source,
    Func<Task<(bool Success, int Rpm)>> ReadAsync);

internal static class FanSpeedReadCoordinator
{
    public static async Task<FanSpeedReading> ReadAsync(string fanName, params FanSpeedSourceReader[] readers)
    {
        var failures = new List<string>();
        foreach (var reader in readers)
        {
            try
            {
                var (success, rpm) = await reader.ReadAsync().ConfigureAwait(false);
                if (!success || rpm < 0)
                {
                    failures.Add($"{reader.Source}: unavailable");
                    continue;
                }

                var result = FanSpeedReading.Available(rpm, reader.Source);
                Log.Instance.TraceOnce(
                    $"fan-source-{fanName}-{reader.Source}",
                    $"{fanName} fan RPM source selected: {reader.Source}.");
                return result;
            }
            catch (Exception ex)
            {
                failures.Add($"{reader.Source}: {ex.GetType().Name}");
                Log.Instance.TraceOnce(
                    $"fan-source-failed-{fanName}-{reader.Source}",
                    $"{fanName} fan RPM source {reader.Source} failed; trying the next source.",
                    ex);
            }
        }

        return FanSpeedReading.Unavailable(
            FanSpeedSource.Unavailable,
            failures.Count == 0 ? "No fan speed sources configured." : string.Join("; ", failures));
    }
}
