using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.System.Management;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors;

public abstract partial class AbstractSensorsController(GPUController gpuController) : ISensorsController
{
    protected readonly record struct LibreHardwareMonitorReadings(
        int CpuUtilization,
        int CpuTemperature,
        int CpuCoreClock,
        int CpuWattage,
        double CpuVoltage,
        int CpuFanSpeed,
        int GpuUtilization,
        int GpuTemperature,
        int GpuCoreClock,
        int GpuMemoryClock,
        int GpuWattage,
        double GpuVoltage,
        int GpuFanSpeed);

    protected readonly struct GPUInfo(
        int utilization,
        int coreClock,
        int maxCoreClock,
        int memoryClock,
        int maxMemoryClock,
        int temperature,
        int maxTemperature,
        int wattage,
        double voltage)
    {
        public static readonly GPUInfo Empty = new(-1, -1, -1, -1, -1, -1, -1, -1, 0);

        public int Utilization { get; } = utilization;
        public int CoreClock { get; } = coreClock;
        public int MaxCoreClock { get; } = maxCoreClock;
        public int MemoryClock { get; } = memoryClock;
        public int MaxMemoryClock { get; } = maxMemoryClock;
        public int Temperature { get; } = temperature;
        public int MaxTemperature { get; } = maxTemperature;
        public int Wattage { get; } = wattage;
        public double Voltage { get; } = voltage;
    }

    private readonly SafePerformanceCounter _percentProcessorPerformanceCounter = new("Processor Information", "% Processor Performance", "_Total");
    private readonly SafePerformanceCounter _percentProcessorUtilityCounter = new("Processor Information", "% Processor Utility", "_Total");
    private readonly SafePerformanceCounter? _cpuPowerCounter = TryCreatePowerCounter();

    private int? _cpuBaseClockCache;
    private int? _cpuMaxCoreClockCache;
    private int? _cpuMaxFanSpeedCache;
    private int? _gpuMaxFanSpeedCache;

    // Sensor data cache — short TTL collapses concurrent callers (UI + HWiNFO + OSD).
    private readonly object _cacheLock = new();
    private SensorsData? _cachedSensorsData;
    private bool _cachedSensorsDetailed;
    private bool _sensorReadFailureLogged;
    private DateTime _lastCacheUpdateTime = DateTime.MinValue;
    private const int CACHE_EXPIRATION_MS = 180;
    private const int DefaultMaxFanSpeedRpm = 5500;
    // Fan WMI + LHM must finish inside this window; overruns return stale cache and freeze gauges/charts.
    private const int SENSOR_READ_TIMEOUT_SECONDS = 3;
    protected virtual int SensorReadTimeoutSeconds => SENSOR_READ_TIMEOUT_SECONDS;

    private int _disposed;
    private Task<SensorsData>? _inFlightRead;
    private bool _inFlightDetailed;

    protected async Task<bool> CanReadSensorSnapshotAsync()
    {
        try
        {
            await GetSensorSnapshotAsync(detailed: false).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensor snapshot probe failed. [type={GetType().Name}]", ex);

            return false;
        }
    }

    public abstract Task<bool> IsSupportedAsync();

    protected virtual bool ShouldGateGpuInfoByLenovoController => true;

    public Task PrepareAsync()
    {
        _percentProcessorPerformanceCounter.Reset();
        _percentProcessorUtilityCounter.Reset();
        
        try { NVAPI.Initialize(); } catch
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to initialize NVAPI");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _percentProcessorPerformanceCounter.Dispose();
        _percentProcessorUtilityCounter.Dispose();
        _cpuPowerCounter?.Dispose();

        try { NVAPI.Unload(); } catch
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Failed to unload NVAPI");
        }

        GC.SuppressFinalize(this);
    }

    private double _cpuMinVoltage = double.MaxValue;
    private double _cpuMaxVoltage = double.MinValue;
    private int _cpuMinTemp = int.MaxValue;
    private int _cpuMaxTemp = int.MinValue;
    
    private double _gpuMinVoltage = double.MaxValue;
    private double _gpuMaxVoltage = double.MinValue;
    private int _gpuMinTemp = int.MaxValue;
    private int _gpuMaxTemp = int.MinValue;

    public async Task<SensorsData> GetDataAsync(bool detailed = false)
    {
        Task<SensorsData> readTask;
        lock (_cacheLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return _cachedSensorsData ?? new SensorsData(new SensorData(), new SensorData());

            // Detailed snapshots can satisfy summary callers; summary cannot satisfy detailed.
            var now = DateTime.UtcNow;
            if (_cachedSensorsData.HasValue
                && (now - _lastCacheUpdateTime).TotalMilliseconds < CACHE_EXPIRATION_MS
                && (!detailed || _cachedSensorsDetailed))
            {
                return _cachedSensorsData.Value;
            }

            if (_inFlightRead is { IsCompleted: false } && (!detailed || _inFlightDetailed))
            {
                readTask = _inFlightRead;
            }
            else
            {
                readTask = ReadSensorsAndUpdateCacheAsync(detailed);
                _inFlightRead = readTask;
                _inFlightDetailed = detailed;
            }
        }

        try
        {
            return await readTask.ConfigureAwait(false);
        }
        finally
        {
            lock (_cacheLock)
            {
                if (ReferenceEquals(_inFlightRead, readTask))
                    _inFlightRead = null;
            }
        }
    }

    private async Task<SensorsData> ReadSensorsAndUpdateCacheAsync(bool detailed)
    {
        // Timeout must cancel the wait itself. Passing a token into GetSensorSnapshotAsync
        // is not enough: the parallel WMI/NVAPI probes do not observe it, so WhenAll
        // would otherwise run past SENSOR_READ_TIMEOUT_SECONDS.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(SensorReadTimeoutSeconds));

        try
        {
            var (cpu, gpu) = await GetSensorSnapshotAsync(detailed, timeoutCts.Token).ConfigureAwait(false);

            var result = new SensorsData(cpu, gpu);
            _sensorReadFailureLogged = false;

            lock (_cacheLock)
            {
                _cachedSensorsData = result;
                _cachedSensorsDetailed = detailed;
                _lastCacheUpdateTime = DateTime.UtcNow;
            }

            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            if (Log.Instance.IsTraceEnabled && !_sensorReadFailureLogged)
            {
                Log.Instance.Trace($"Sensor read timed out after {SensorReadTimeoutSeconds}s, falling back to cache. [type={GetType().Name}]");
                _sensorReadFailureLogged = true;
            }

            lock (_cacheLock)
            {
                if (_cachedSensorsData.HasValue)
                    return _cachedSensorsData.Value;
            }

            return new SensorsData(SensorData.Empty, SensorData.Empty);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled && !_sensorReadFailureLogged)
            {
                Log.Instance.Trace($"Sensor read failed. [type={GetType().Name}]", ex);
                _sensorReadFailureLogged = true;
            }

            lock (_cacheLock)
            {
                if (_cachedSensorsData.HasValue)
                    return _cachedSensorsData.Value;
            }

            return new SensorsData(SensorData.Empty, SensorData.Empty);
        }
    }

    private async Task<(SensorData cpu, SensorData gpu)> GetSensorSnapshotAsync(bool detailed, CancellationToken cancellationToken = default)
    {
        const int GENERIC_MAX_UTILIZATION = 100;
        const int GENERIC_MAX_TEMPERATURE = 100;
        Task<LibreHardwareMonitorReadings?>? libreHardwareMonitorReadingsTask = null;
        Task<LibreHardwareMonitorReadings?> GetLibreHardwareMonitorReadingsOnceAsync() =>
            libreHardwareMonitorReadingsTask ??= GetLibreHardwareMonitorReadingsAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Cheap counters stay sync; independent WMI/NVAPI/fan probes run in parallel.
        var cpuUtilization = SafeRead(() => GetCpuUtilization(GENERIC_MAX_UTILIZATION), -1, "CPU utilization");
        var cpuCoreClock = SafeRead(GetCpuCoreClock, -1, "CPU core clock");

        var cpuMaxCoreClockTask = SafeReadAsync(async () => _cpuMaxCoreClockCache ??= await GetCpuMaxCoreClockAsync().ConfigureAwait(false), -1, "CPU max core clock");
        var cpuTempTask = SafeReadAsync(GetCpuCurrentTemperatureAsync, -1, "CPU temperature");
        var cpuFanTask = SafeReadAsync(GetCpuCurrentFanSpeedAsync, -1, "CPU fan speed");
        var cpuMaxFanTask = SafeReadAsync(async () => _cpuMaxFanSpeedCache ??= await GetCpuMaxFanSpeedAsync().ConfigureAwait(false), -1, "CPU max fan speed");
        var gpuInfoTask = SafeReadAsync(GetGPUInfoAsync, GPUInfo.Empty, "GPU info");
        var gpuTempTask = SafeReadAsync(GetGpuCurrentTemperatureAsync, -1, "GPU temperature");
        var gpuFanTask = SafeReadAsync(GetGpuCurrentFanSpeedAsync, -1, "GPU fan speed");
        var gpuMaxFanTask = SafeReadAsync(async () => _gpuMaxFanSpeedCache ??= await GetGpuMaxFanSpeedAsync().ConfigureAwait(false), -1, "GPU max fan speed");
        Task<double>? cpuVoltageTask = detailed
            ? SafeReadAsync(WMI.Win32.Processor.GetVoltageAsync, 0d, "CPU voltage")
            : null;
        Task<int>? cpuWattageTask = detailed
            ? SafeReadAsync(GetCpuWattageAsync, -1, "CPU wattage")
            : null;

        await Task.WhenAll(
            cpuMaxCoreClockTask,
            cpuTempTask,
            cpuFanTask,
            cpuMaxFanTask,
            gpuInfoTask,
            gpuTempTask,
            gpuFanTask,
            gpuMaxFanTask,
            cpuVoltageTask ?? Task.CompletedTask,
            cpuWattageTask ?? Task.CompletedTask).WaitAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var cpuMaxCoreClock = await cpuMaxCoreClockTask.ConfigureAwait(false);
        var cpuCurrentTemperature = NormalizeTemperatureReading(await cpuTempTask.ConfigureAwait(false));
        var cpuCurrentFanSpeed = await cpuFanTask.ConfigureAwait(false);
        var cpuMaxFanSpeed = await cpuMaxFanTask.ConfigureAwait(false);
        var cpuVoltage = cpuVoltageTask is null ? 0d : await cpuVoltageTask.ConfigureAwait(false);
        var cpuWattage = cpuWattageTask is null ? -1 : await cpuWattageTask.ConfigureAwait(false);

        var gpuInfo = await gpuInfoTask.ConfigureAwait(false);
        var gpuUtilization = gpuInfo.Utilization;
        var gpuCoreClock = gpuInfo.CoreClock;
        var gpuMaxCoreClock = gpuInfo.MaxCoreClock;
        var gpuMemoryClock = gpuInfo.MemoryClock;
        var gpuMaxMemoryClock = gpuInfo.MaxMemoryClock;
        var gpuWattage = gpuInfo.Wattage;
        var gpuVoltage = gpuInfo.Voltage;
        var gpuCurrentTemperature = gpuInfo.Temperature > 0
            ? gpuInfo.Temperature
            : NormalizeTemperatureReading(await gpuTempTask.ConfigureAwait(false));
        var gpuMaxTemperature = gpuInfo.MaxTemperature >= 0 ? gpuInfo.MaxTemperature : GENERIC_MAX_TEMPERATURE;
        var gpuCurrentFanSpeed = await gpuFanTask.ConfigureAwait(false);
        var gpuMaxFanSpeed = await gpuMaxFanTask.ConfigureAwait(false);

        // Single LHM pass fills any missing CPU/GPU fields (fans are the common gap on IRX9+).
        // Fan uses <= 0 so a false WMI "0 RPM" can still be replaced by a positive LHM reading.
        var needLhm = cpuUtilization < 0 || cpuCoreClock < 0 || cpuCurrentTemperature < 0 || cpuCurrentFanSpeed <= 0
                      || gpuUtilization < 0 || gpuCoreClock < 0 || gpuCurrentTemperature < 0 || gpuCurrentFanSpeed <= 0
                      || (detailed && (cpuVoltage <= 0 || cpuWattage < 0 || gpuVoltage <= 0 || gpuWattage < 0));
        if (needLhm)
        {
            var libreHardwareMonitorReadings = await GetLibreHardwareMonitorReadingsOnceAsync().ConfigureAwait(false);
            if (libreHardwareMonitorReadings is { } readings)
            {
                if (cpuUtilization < 0 && readings.CpuUtilization >= 0)
                    cpuUtilization = readings.CpuUtilization;
                if (cpuCoreClock < 0 && readings.CpuCoreClock >= 0)
                    cpuCoreClock = readings.CpuCoreClock;
                if (cpuCurrentTemperature < 0 && readings.CpuTemperature > 0)
                    cpuCurrentTemperature = readings.CpuTemperature;
                if (cpuCurrentFanSpeed <= 0 && readings.CpuFanSpeed > 0)
                    cpuCurrentFanSpeed = readings.CpuFanSpeed;
                if (detailed && cpuVoltage <= 0 && readings.CpuVoltage > 0)
                    cpuVoltage = readings.CpuVoltage;
                if (detailed && cpuWattage < 0 && readings.CpuWattage > 0)
                    cpuWattage = readings.CpuWattage;

                if (gpuUtilization < 0 && readings.GpuUtilization >= 0)
                    gpuUtilization = readings.GpuUtilization;
                if (gpuCoreClock < 0 && readings.GpuCoreClock >= 0)
                    gpuCoreClock = readings.GpuCoreClock;
                if (gpuMemoryClock < 0 && readings.GpuMemoryClock >= 0)
                    gpuMemoryClock = readings.GpuMemoryClock;
                if (gpuCurrentTemperature < 0 && readings.GpuTemperature > 0)
                    gpuCurrentTemperature = readings.GpuTemperature;
                if (gpuCurrentFanSpeed <= 0 && readings.GpuFanSpeed > 0)
                    gpuCurrentFanSpeed = readings.GpuFanSpeed;
                if (detailed && gpuWattage < 0 && readings.GpuWattage > 0)
                    gpuWattage = readings.GpuWattage;
                if (detailed && gpuVoltage <= 0 && readings.GpuVoltage > 0)
                    gpuVoltage = readings.GpuVoltage;
            }
        }

        if (cpuCurrentTemperature < 0)
        {
            var fallback = await SensorReadingHelper.GetCpuTemperatureFromAcpiAsync().ConfigureAwait(false);
            cpuCurrentTemperature = fallback > 0 ? fallback : -1;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (cpuMaxFanSpeed <= 0 && cpuCurrentFanSpeed > 0)
            cpuMaxFanSpeed = Math.Max(cpuCurrentFanSpeed, DefaultMaxFanSpeedRpm);
        if (gpuMaxFanSpeed <= 0 && gpuCurrentFanSpeed > 0)
            gpuMaxFanSpeed = Math.Max(gpuCurrentFanSpeed, DefaultMaxFanSpeedRpm);

        if (gpuMaxCoreClock < 0 && gpuCoreClock >= 0)
            gpuMaxCoreClock = gpuCoreClock;
        if (gpuMaxMemoryClock < 0 && gpuMemoryClock >= 0)
            gpuMaxMemoryClock = gpuMemoryClock;

        lock (_cacheLock)
        {
            if (cpuVoltage > 0)
            {
                if (cpuVoltage < _cpuMinVoltage) _cpuMinVoltage = cpuVoltage;
                if (cpuVoltage > _cpuMaxVoltage) _cpuMaxVoltage = cpuVoltage;
            }
            if (cpuCurrentTemperature > 0)
            {
                if (cpuCurrentTemperature < _cpuMinTemp) _cpuMinTemp = cpuCurrentTemperature;
                if (cpuCurrentTemperature > _cpuMaxTemp) _cpuMaxTemp = cpuCurrentTemperature;
            }

            if (gpuVoltage > 0)
            {
                if (gpuVoltage < _gpuMinVoltage) _gpuMinVoltage = gpuVoltage;
                if (gpuVoltage > _gpuMaxVoltage) _gpuMaxVoltage = gpuVoltage;
            }
            if (gpuCurrentTemperature > 0)
            {
                if (gpuCurrentTemperature < _gpuMinTemp) _gpuMinTemp = gpuCurrentTemperature;
                if (gpuCurrentTemperature > _gpuMaxTemp) _gpuMaxTemp = gpuCurrentTemperature;
            }
        }

        var cpu = new SensorData(cpuUtilization,
            GENERIC_MAX_UTILIZATION,
            cpuCoreClock,
            cpuMaxCoreClock,
            -1,
            -1,
            cpuCurrentTemperature,
            GENERIC_MAX_TEMPERATURE,
            cpuWattage,
            cpuVoltage,
            cpuCurrentFanSpeed,
            cpuMaxFanSpeed).WithMinMax(_cpuMinVoltage, _cpuMaxVoltage, _cpuMinTemp, _cpuMaxTemp);

        var gpu = new SensorData(gpuUtilization,
            GENERIC_MAX_UTILIZATION,
            gpuCoreClock,
            gpuMaxCoreClock,
            gpuMemoryClock,
            gpuMaxMemoryClock,
            gpuCurrentTemperature,
            gpuMaxTemperature,
            gpuWattage,
            gpuVoltage,
            gpuCurrentFanSpeed,
            gpuMaxFanSpeed).WithMinMax(_gpuMinVoltage, _gpuMaxVoltage, _gpuMinTemp, _gpuMaxTemp);

        return (cpu, gpu);
    }

    public async Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync()
    {
        var now = DateTime.UtcNow;
        lock (_cacheLock)
        {
            if (_cachedSensorsData.HasValue && (now - _lastCacheUpdateTime).TotalMilliseconds < CACHE_EXPIRATION_MS)
            {
                return (_cachedSensorsData.Value.CPU.FanSpeed, _cachedSensorsData.Value.GPU.FanSpeed);
            }
        }

        var data = await GetDataAsync().ConfigureAwait(false);
        return (data.CPU.FanSpeed, data.GPU.FanSpeed);
    }

    protected abstract Task<int> GetCpuCurrentTemperatureAsync();

    protected abstract Task<int> GetGpuCurrentTemperatureAsync();

    protected abstract Task<int> GetCpuCurrentFanSpeedAsync();

    protected abstract Task<int> GetGpuCurrentFanSpeedAsync();

    protected virtual Task<int> GetPchCurrentTemperatureAsync() => Task.FromResult(-1);

    protected virtual Task<int> GetPchCurrentFanSpeedAsync() => Task.FromResult(-1);

    protected abstract Task<int> GetCpuMaxFanSpeedAsync();

    protected abstract Task<int> GetGpuMaxFanSpeedAsync();

    protected virtual Task<int> GetPchMaxFanSpeedAsync() => Task.FromResult(-1);

    protected virtual async Task<LibreHardwareMonitorReadings?> GetLibreHardwareMonitorReadingsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IoCContainer.TryResolve<SensorsGroupController>() is not { } sensorsGroupController)
                return null;

            if (!sensorsGroupController.IsLibreHardwareMonitorInitialized())
            {
                var fullSensorsEnabled = IoCContainer.TryResolve<ApplicationSettings>()?.Store.EnableHardwareSensors == true;
                _ = fullSensorsEnabled
                    ? await sensorsGroupController.IsSupportedAsync().ConfigureAwait(false)
                    : await sensorsGroupController.EnsureFanSensorsAvailableAsync(cancellationToken).ConfigureAwait(false)
                        ? LibreHardwareMonitorInitialState.Success
                        : LibreHardwareMonitorInitialState.Fail;
            }

            if (!sensorsGroupController.IsLibreHardwareMonitorInitialized())
                return null;

            await sensorsGroupController.UpdateAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return new LibreHardwareMonitorReadings(
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetCpuUsageAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetCpuTemperatureAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetCpuCoreClockAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorPositiveMetric(await sensorsGroupController.GetCpuPowerAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorVoltage(await sensorsGroupController.GetCpuVoltageAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetCpuFanSpeedAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetGpuUsageAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetGpuTemperatureAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetGpuCoreClockAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetGpuMemoryClockAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorPositiveMetric(await sensorsGroupController.GetGpuPowerAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorVoltage(await sensorsGroupController.GetGpuVoltageAsync().ConfigureAwait(false)),
                NormalizeLibreHardwareMonitorMetric(await sensorsGroupController.GetGpuFanSpeedAsync().ConfigureAwait(false)));
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-lhm-snapshot", "LibreHardwareMonitor sensor snapshot failed.", ex);
            return null;
        }
    }

    private static int NormalizeLibreHardwareMonitorMetric(float value) =>
        value >= 0 ? (int)Math.Round(value) : -1;

    private static int NormalizeLibreHardwareMonitorPositiveMetric(float value) =>
        value > 0 ? (int)Math.Round(value) : -1;

    private static double NormalizeLibreHardwareMonitorVoltage(float value) =>
        value > 0 ? Math.Round(value, 3) : 0;

    private static int NormalizeTemperatureReading(int value) =>
        value > 0 ? value : -1;

    protected virtual int GetCpuUtilization(int maxUtilization)
    {
        var result = (int)_percentProcessorUtilityCounter.NextValue();
        if (result < 0)
            return -1;
        return Math.Min(result, maxUtilization);
    }

    protected virtual int GetCpuCoreClock()
    {
        var baseClock = _cpuBaseClockCache ??= GetCpuBaseClock();
        var clock = (int)(baseClock * (_percentProcessorPerformanceCounter.NextValue() / 100f));
        if (clock < 1)
            return -1;
        return clock;
    }

    private static unsafe int GetCpuBaseClock()
    {
        var ptr = IntPtr.Zero;
        try
        {
            PInvoke.GetSystemInfo(out var systemInfo);

            var numberOfProcessors = Math.Min(32, (int)systemInfo.dwNumberOfProcessors);
            var infoSize = Marshal.SizeOf<PROCESSOR_POWER_INFORMATION>();
            var infosSize = numberOfProcessors * infoSize;

            ptr = Marshal.AllocHGlobal(infosSize);

            var result = PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.ProcessorInformation,
                null,
                0,
                ptr.ToPointer(),
                (uint)infosSize);
            if (result != 0)
                return 0;

            var infos = new PROCESSOR_POWER_INFORMATION[numberOfProcessors];

            for (var i = 0; i < infos.Length; i++)
                infos[i] = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(IntPtr.Add(ptr, i * infoSize));

            return (int)infos.Select(p => p.MaxMhz).Max();
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Asynchronously gets the CPU maximum core clock frequency.
    /// </summary>
    /// <returns>CPU maximum core clock frequency in MHz.</returns>
    /// <remarks>
    /// This method can be overridden in unit tests to avoid actual WMI calls.
    /// </remarks>
    protected virtual Task<int> GetCpuMaxCoreClockAsync() => WMI.LenovoGameZoneData.GetCPUFrequencyAsync();

    private static SafePerformanceCounter? TryCreatePowerCounter()
    {
        try
        {
            // Try to create a performance counter for CPU power consumption
            // Windows 10/11 may provide "Processor Information" category with "Power" counter
            // Note: This may not be available on all systems
            return new SafePerformanceCounter("Processor Information", "Power", "_Total");
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "sensors-power-counter",
                "Processor Information/Power performance counter unavailable.",
                ex);
            return null;
        }
    }

    protected virtual async Task<int> GetCpuWattageAsync()
    {
        await Task.Delay(0, CancellationToken.None).ConfigureAwait(false);

        // Try method 1: Performance counter (if available)
        var performanceCounterWattage = GetCpuWattageFromPerformanceCounter();
        if (performanceCounterWattage > 0)
            return performanceCounterWattage;

        // Try method 2: WMI query for power meter (if available)
        try
        {
            var wattage = await GetCpuWattageFromWmiAsync().ConfigureAwait(false);
            if (wattage > 0)
                return wattage;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-cpu-watt-wmi", "CPU wattage WMI probe failed.", ex);
        }

        // Try method 3: reuse LibreHardwareMonitor package power if that path is already available
        var libreHardwareMonitorWattage = await GetCpuWattageFromLibreHardwareMonitorAsync().ConfigureAwait(false);
        if (libreHardwareMonitorWattage > 0)
            return libreHardwareMonitorWattage;

        // Method not available, return -1
        return -1;
    }

    protected virtual int GetCpuWattageFromPerformanceCounter()
    {
        if (_cpuPowerCounter == null)
            return -1;

        try
        {
            var powerValue = _cpuPowerCounter.NextValue();
            return SensorReadingHelper.NormalizePowerReadingToWatts(powerValue);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-cpu-watt-perfctr", "CPU wattage performance counter read failed.", ex);
            return -1;
        }
    }

    protected virtual Task<int> GetCpuWattageFromWmiAsync() =>
        SensorReadingHelper.GetCpuWattageFromWmiAsync();

    protected virtual async Task<int> GetCpuWattageFromLibreHardwareMonitorAsync()
    {
        try
        {
            if (IoCContainer.TryResolve<SensorsGroupController>() is { } sensorsGroupController)
            {
                if (!sensorsGroupController.IsLibreHardwareMonitorInitialized())
                    _ = await sensorsGroupController.IsSupportedAsync().ConfigureAwait(false);

                if (sensorsGroupController.IsLibreHardwareMonitorInitialized())
                {
                    await sensorsGroupController.UpdateAsync().ConfigureAwait(false);

                    var cpuPower = await sensorsGroupController.GetCpuPowerAsync().ConfigureAwait(false);
                    if (cpuPower > 0)
                        return (int)Math.Round(cpuPower);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "sensors-cpu-watt-lhm",
                "CPU wattage LibreHardwareMonitor path failed.",
                ex);
        }

        return -1;
    }

    private static async Task<(int wattage, double voltage)> GetGpuInfoFromNvidiaSmiAsync()
    {
        try
        {
            var executablePath = FindNvidiaSmiPath();
            if (executablePath is null)
                return (-1, 0);

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "-q",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return (-1, 0);

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            int wattage = -1;
            double voltage = 0;

            var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool inPowerReadings = false;
            bool inVoltageReadings = false;

            foreach (var line in lines)
            {
                if (line.Contains("GPU Power Readings"))
                {
                    inPowerReadings = true;
                    inVoltageReadings = false;
                    continue;
                }
                if (line.Contains("Voltage"))
                {
                    inVoltageReadings = true;
                    inPowerReadings = false;
                    continue;
                }

                var trimmed = line.Trim();
                if (inPowerReadings && trimmed.StartsWith("Power Draw"))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length > 1)
                    {
                        var val = parts[1].Trim().Split(' ')[0];
                        if (double.TryParse(val, global::System.Globalization.CultureInfo.InvariantCulture, out var w))
                            wattage = (int)w;
                    }
                    inPowerReadings = false; 
                }
                else if (inPowerReadings && (trimmed.StartsWith("Instantaneous Power Draw") || trimmed.StartsWith("Average Power Draw")))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length > 1)
                    {
                        var val = parts[1].Trim().Split(' ')[0];
                        if (double.TryParse(val, global::System.Globalization.CultureInfo.InvariantCulture, out var w))
                            wattage = (int)w;
                    }
                }
                else if (inVoltageReadings && trimmed.StartsWith("Graphics"))
                {
                    var parts = trimmed.Split(':');
                    if (parts.Length > 1)
                    {
                        var val = parts[1].Trim().Split(' ')[0];
                        if (double.TryParse(val, global::System.Globalization.CultureInfo.InvariantCulture, out var v))
                            voltage = v / 1000.0;
                    }
                    inVoltageReadings = false;
                }
            }

            return (wattage, voltage);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-nvidia-smi-power", "nvidia-smi power/voltage parse failed.", ex);
            return (-1, 0);
        }
    }

    protected internal static string? FindNvidiaSmiPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("ProgramW6432") ?? programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    protected virtual async Task<GPUInfo> GetGPUInfoAsync()
    {
        if (ShouldGateGpuInfoByLenovoController)
        {
            if (await gpuController.IsSupportedAsync().ConfigureAwait(false))
                await gpuController.StartAsync().ConfigureAwait(false);

            // Only skip NVAPI when we know the dGPU is off. Unknown (pre-first-refresh)
            // must not block sensor reads forever.
            if (await gpuController.GetLastKnownStateAsync().ConfigureAwait(false) is GPUState.PoweredOff)
                return GPUInfo.Empty;
        }

        try
        {
            var gpuNullable = NVAPI.GetGPU();
            if (gpuNullable is not { } gpu)
                return GPUInfo.Empty;

            var utilization = NVAPI.GetUsage(gpu);

            var (currentGfxKHz, currentMemKHz) = NVAPI.GetCurrentClockFrequencies(gpu);
            var currentCoreClock = currentGfxKHz / 1000;
            var currentMemoryClock = currentMemKHz / 1000;

            var (boostGfxKHz, boostMemKHz) = NVAPI.GetBoostClockFrequencies(gpu);
            var maxCoreClock = boostGfxKHz / 1000;
            var maxMemoryClock = boostMemKHz / 1000;

            // Get current performance state
            var currentPerformanceState = NvPerformanceStateId.P0_3DPerformance;
            try
            {
                currentPerformanceState = NVAPI.GetCurrentPstate(gpu);
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    "sensors-gpu-pstate",
                    "Failed to parse GPU performance state id; defaulting to P0.",
                    ex);
            }

            // Try to get overclock offsets for current performance state, fall back to P0 if not available
            int maxCoreClockOffset = 0;
            int maxMemoryClockOffset = 0;
            try
            {
                (maxCoreClockOffset, maxMemoryClockOffset) = NVAPI.GetOverclockDelta(gpu, currentPerformanceState);
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    "sensors-gpu-oc-current",
                    "Failed to read GPU OC offsets for current p-state; trying P0.",
                    ex);
                try
                {
                    (maxCoreClockOffset, maxMemoryClockOffset) = NVAPI.GetOverclockDelta(gpu, NvPerformanceStateId.P0_3DPerformance);
                }
                catch (Exception ex2)
                {
                    Log.Instance.TraceOnce(
                        "sensors-gpu-oc-p0",
                        "No GPU overclock offsets available from NVAPI.",
                        ex2);
                }
            }

            var thermalSensors = NVAPI.GetThermalSensors(gpu);
            var currentTemperature = thermalSensors.Length > 0 ? thermalSensors[0].CurrentTemperature : -1;
            var maxTemperature = thermalSensors.Length > 0 ? thermalSensors[0].DefaultMaximumTemperature : -1;

            // Get GPU Power and Voltage via helper methods
            var currentWattage = GPUInfoHelper.GetWattage(gpu);
            var currentVoltage = GPUInfoHelper.GetVoltage(gpu);

            if (currentWattage < 0)
                currentWattage = GPUInfoHelper.GetWattageFromPowerTopology(gpu);

            // Final fallback: nvidia-smi
            if (currentWattage < 0 || currentVoltage == 0)
            {
                var (smiWattage, smiVoltage) = await GetGpuInfoFromNvidiaSmiAsync().ConfigureAwait(false);
                if (currentWattage < 0 && smiWattage >= 0)
                    currentWattage = smiWattage;
                if (currentVoltage == 0 && smiVoltage > 0)
                    currentVoltage = smiVoltage;
            }

            return new(utilization,
                currentCoreClock,
                maxCoreClock + maxCoreClockOffset,
                currentMemoryClock,
                maxMemoryClock + maxMemoryClockOffset,
                currentTemperature,
                maxTemperature,
                currentWattage,
                currentVoltage);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce("sensors-nvapi-gpuinfo", "NVAPI GPUInfo snapshot failed.", ex);
            return GPUInfo.Empty;
        }
    }

    private static T SafeRead<T>(Func<T> operation, T fallback, string operationName)
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"sensors-safe-read-{operationName}",
                $"Safe sensor read failed: {operationName}",
                ex);
            return fallback;
        }
    }

    private static async Task<T> SafeReadAsync<T>(Func<Task<T>> operation, T fallback, string operationName)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"sensors-safe-read-async-{operationName}",
                $"Safe async sensor read failed: {operationName}",
                ex);
            return fallback;
        }
    }

    /// <summary>
    /// Awaits a fallback task with a timeout to prevent indefinite blocking.
    /// Used by vendor-specific sensor controllers when a hardware read fails and a
    /// software fallback is needed.
    /// </summary>
    protected static async Task<int> AwaitWithTimeoutAsync(Task<int> fallback, int timeoutSeconds = 30)
    {
        try
        {
            return await fallback.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Log.Instance.Warning($"Sensor fallback task timed out after {timeoutSeconds}s.");
            return -1;
        }
    }
}
