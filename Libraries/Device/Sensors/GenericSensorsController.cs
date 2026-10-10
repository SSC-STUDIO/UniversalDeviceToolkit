using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public class GenericSensorsController(GPUController gpuController, IDelayProvider? delayProvider = null) : AbstractSensorsController(gpuController)
{
    private static readonly TimeSpan SupportProbeRetryDelay = TimeSpan.FromMilliseconds(250);
    private readonly IDelayProvider _delayProvider = delayProvider ?? new DefaultDelayProvider();

    protected override bool ShouldGateGpuInfoByLenovoController => false;

    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (await CanReadGenericSnapshotAsyncCore().ConfigureAwait(false))
                return true;

            await _delayProvider.Delay(SupportProbeRetryDelay, CancellationToken.None).ConfigureAwait(false);
            return await CanReadGenericSnapshotAsyncCore().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error checking generic sensors support. [type={GetType().Name}]", ex);

            return false;
        }
    }

    protected virtual Task<bool> CanReadGenericSnapshotAsyncCore() => CanReadGenericSnapshotAsync();

    protected override Task<int> GetCpuCurrentTemperatureAsync() =>
        SensorReadingHelper.GetCpuTemperatureFromAcpiAsync();

    protected override Task<int> GetGpuCurrentTemperatureAsync() =>
        Task.FromResult(-1);

    protected override Task<int> GetCpuCurrentFanSpeedAsync() =>
        ReadLenovoCpuFanSpeedAsync();

    protected override Task<int> GetGpuCurrentFanSpeedAsync() =>
        ReadLenovoGpuFanSpeedAsync();

    protected virtual Task<int> ReadLenovoCpuFanSpeedAsync() =>
        ReadFanSpeedAsync("CPU", CapabilityFanSource(CapabilityID.CpuCurrentFanSpeed), GamezoneCpuFanSource());

    protected virtual Task<int> ReadLenovoGpuFanSpeedAsync() =>
        ReadFanSpeedAsync("GPU", CapabilityFanSource(CapabilityID.GpuCurrentFanSpeed), GamezoneGpuFanSource());
    protected override Task<int> GetCpuMaxFanSpeedAsync() =>
        Task.FromResult(-1);

    protected override Task<int> GetGpuMaxFanSpeedAsync() =>
        Task.FromResult(-1);

    protected override Task<int> GetCpuMaxCoreClockAsync() =>
        GetGenericCpuMaxCoreClockAsync();

    protected override async Task<GPUInfo> GetGPUInfoAsync()
    {
        var nvapiInfo = await base.GetGPUInfoAsync().ConfigureAwait(false);
        if (HasUsableGpuData(nvapiInfo))
            return nvapiInfo;

        var nvidiaSmiInfo = await GetGpuInfoFromNvidiaSmiAsync().ConfigureAwait(false);
        if (HasUsableGpuData(nvidiaSmiInfo))
            return nvidiaSmiInfo;

        var utilization = await SensorReadingHelper.GetGpuUtilizationFromPerformanceCountersAsync().ConfigureAwait(false);
        return utilization >= 0
            ? new GPUInfo(utilization, -1, -1, -1, -1, -1, -1, -1, 0)
            : GPUInfo.Empty;
    }

    private async Task<bool> CanReadGenericSnapshotAsync()
    {
        var data = await GetDataAsync().ConfigureAwait(false);
        return HasUsableData(data.CPU) || HasUsableData(data.GPU);
    }

    private static bool HasUsableData(SensorData data) =>
        data.Utilization >= 0 ||
        data.CoreClock >= 0 ||
        data.Temperature >= 0 ||
        data.Wattage >= 0 ||
        data.Voltage > 0;

    private static bool HasUsableGpuData(GPUInfo data) =>
        data.Utilization >= 0 ||
        data.CoreClock >= 0 ||
        data.Temperature >= 0 ||
        data.Wattage >= 0 ||
        data.Voltage > 0;

    private static async Task<int> GetGenericCpuMaxCoreClockAsync()
    {
        try
        {
            var processors = await WMI.Win32.Processor.ReadAsync().ConfigureAwait(false);
            return processors
                .Select(processor => processor.MaxClockSpeedMHz ?? -1)
                .Where(clock => clock > 0)
                .DefaultIfEmpty(-1)
                .Max();
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("generic-sensors-cpu-max-clock", "Failed to read CPU MaxClockSpeed via WMI.", ex);
            return -1;
        }
    }

    private static async Task<GPUInfo> GetGpuInfoFromNvidiaSmiAsync()
    {
        try
        {
            var nvidiaSmiPath = FindNvidiaSmiPath();
            if (nvidiaSmiPath is null)
                return GPUInfo.Empty;

            var startInfo = new ProcessStartInfo
            {
                FileName = nvidiaSmiPath,
                Arguments = "--query-gpu=utilization.gpu,clocks.current.graphics,clocks.max.graphics,clocks.current.memory,clocks.max.memory,temperature.gpu,power.draw,voltage.graphics --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return GPUInfo.Empty;

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            process.Kill(true);

            if (process.ExitCode != 0)
                return GPUInfo.Empty;

            var line = output
                .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(line))
                return GPUInfo.Empty;

            var values = line.Split(',').Select(value => value.Trim()).ToArray();
            if (values.Length < 8)
                return GPUInfo.Empty;

            return new GPUInfo(
                ParseInt(values[0]),
                ParseInt(values[1]),
                ParseInt(values[2]),
                ParseInt(values[3]),
                ParseInt(values[4]),
                ParseInt(values[5]),
                100,
                ParseInt(values[6]),
                ParseVoltage(values[7]));
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("generic-sensors-nvidia-smi", "nvidia-smi GPU info probe failed.", ex);
            return GPUInfo.Empty;
        }
    }

    private static int ParseInt(string value)
    {
        if (value.Equals("[Not Supported]", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? (int)Math.Round(parsed, MidpointRounding.AwayFromZero)
            : -1;
    }

    private static double ParseVoltage(string value)
    {
        var voltage = ParseInt(value);
        if (voltage <= 0)
            return 0;

        return voltage > 10 ? voltage / 1000d : voltage;
    }
}
