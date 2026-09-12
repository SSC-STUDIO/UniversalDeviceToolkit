using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

internal static class SensorReadingHelper
{
    /// <summary>
    /// LibreHardwareMonitor <c>SensorType.Data</c> is GiB; <c>SensorType.SmallData</c> is MiB.
    /// Normalize both to GiB so VRAM used/total/free can be combined without a 1024x error.
    /// </summary>
    public static float NormalizeLibreHardwareMonitorDataToGigabytes(float value, bool isMegabyteScale)
    {
        if (value < 0)
            return value;

        return isMegabyteScale ? value / 1024f : value;
    }

    public static int NormalizePowerReadingToWatts(object? value)
    {
        if (!TryConvertToDouble(value, out var raw) || raw <= 0)
            return -1;

        // Windows power providers most commonly expose mW. A few counters expose W.
        var watts = raw > 1000 ? raw / 1000d : raw;
        if (watts <= 0 || watts > 1000)
            return -1;

        return (int)Math.Round(watts, MidpointRounding.AwayFromZero);
    }

    public static int ConvertAcpiTenthsKelvinToCelsius(object? value)
    {
        if (!TryConvertToDouble(value, out var tenthsKelvin) || tenthsKelvin <= 0)
            return -1;

        var celsius = tenthsKelvin / 10d - 273.15d;
        if (celsius < -50 || celsius > 150)
            return -1;

        return (int)Math.Round(celsius, MidpointRounding.AwayFromZero);
    }

    public static async Task<int> GetCpuWattageFromWmiAsync()
    {
        var result = await TryReadPowerMeterAsync($"SELECT * FROM Win32_PowerMeter WHERE Name LIKE '%CPU%' OR Name LIKE '%Processor%'").ConfigureAwait(false);
        if (result >= 0)
            return result;

        return await TryReadPowerMeterAsync($"SELECT * FROM Win32_PowerMeter").ConfigureAwait(false);
    }

    public static async Task<int> GetCpuTemperatureFromAcpiAsync()
    {
        try
        {
            var temperatures = await WMI.ReadAsync("root\\WMI",
                $"SELECT * FROM MSAcpi_ThermalZoneTemperature",
                pdc => ConvertAcpiTenthsKelvinToCelsius(pdc["CurrentTemperature"]?.Value)).ConfigureAwait(false);

            return temperatures.Where(t => t > 0).DefaultIfEmpty(-1).Max();
        }
        catch (ManagementException ex)
        {
            Log.Instance.TraceOnce("sensors-acpi-temp", "MSAcpi_ThermalZoneTemperature WMI probe failed.", ex);
            return -1;
        }
        catch (InvalidOperationException ex)
        {
            Log.Instance.TraceOnce("sensors-acpi-temp-invalid", "ACPI thermal zone read invalid operation.", ex);
            return -1;
        }
    }

    private static DateTime _gpuUtilizationCooldownUntil = DateTime.MinValue;
    private static readonly TimeSpan GpuUtilizationCooldown = TimeSpan.FromSeconds(30);

    public static async Task<int> GetGpuUtilizationFromPerformanceCountersAsync()
    {
        if (DateTime.UtcNow < _gpuUtilizationCooldownUntil)
            return -1;

        try
        {
            const string categoryName = "GPU Engine";
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                _gpuUtilizationCooldownUntil = DateTime.UtcNow.Add(GpuUtilizationCooldown);
                return -1;
            }

            var category = new PerformanceCounterCategory(categoryName);
            return category
                .GetInstanceNames()
                .Where(instanceName => instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .Select(ReadGpuEngineUtilization)
                .Where(value => value >= 0)
                .DefaultIfEmpty(-1)
                .Max();
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-gpu-util-category", "GPU Engine performance counter category probe failed.", ex);
            _gpuUtilizationCooldownUntil = DateTime.UtcNow.Add(GpuUtilizationCooldown);
            return -1;
        }
    }

    private static async Task<int> TryReadPowerMeterAsync(FormattableString query)
    {
        try
        {
            var readings = await WMI.ReadAsync("root\\CIMV2\\power",
                query,
                pdc => NormalizePowerReadingToWatts(
                    pdc["Power"]?.Value
                    ?? pdc["CurrentPower"]?.Value
                    ?? pdc["PowerReading"]?.Value)).ConfigureAwait(false);

            return readings.Where(w => w > 0).DefaultIfEmpty(-1).Max();
        }
        catch (ManagementException ex)
        {
            Log.Instance.TraceOnce("sensors-power-meter", "Win32_PowerMeter WMI probe failed.", ex);
            return -1;
        }
        catch (InvalidOperationException ex)
        {
            Log.Instance.TraceOnce("sensors-power-meter-invalid", "Win32_PowerMeter read invalid operation.", ex);
            return -1;
        }
    }

    private static int ReadGpuEngineUtilization(string instanceName)
    {
        if (DateTime.UtcNow < _gpuUtilizationCooldownUntil)
            return -1;

        try
        {
            using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instanceName, true);
            var value = counter.NextValue();
            if (value < 0)
                return -1;

            return Math.Min(100, (int)Math.Round(value, MidpointRounding.AwayFromZero));
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-gpu-util-instance", "GPU Engine utilization counter instance read failed.", ex);
            _gpuUtilizationCooldownUntil = DateTime.UtcNow.Add(GpuUtilizationCooldown);
            return -1;
        }
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        if (value is null)
            return false;

        try
        {
            result = value switch
            {
                string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
                IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
                _ => 0
            };

            return result > 0;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
