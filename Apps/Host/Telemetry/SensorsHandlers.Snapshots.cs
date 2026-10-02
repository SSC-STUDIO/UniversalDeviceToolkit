using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

public static partial class SensorsHandlers
{
    private static SensorsGroupController GetSensorsGroup()
        => IoCContainer.Resolve<SensorsGroupController>();

    private static SensorsController GetVendorSensors()
        => IoCContainer.Resolve<SensorsController>();

    private static async Task<object> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var group = GetSensorsGroup();
        var applicationSettings = IoCContainer.Resolve<ApplicationSettings>();

        if (applicationSettings.Store.EnableHardwareSensors)
        {
            try
            {
                await group.IsSupportedAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Initialization failed; fall back to the vendor path below.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (group.IsLibreHardwareMonitorInitialized())
        {
            var snapshot = await BuildLhmSnapshotAsync(group, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
                return snapshot;
            // LibreHardwareMonitor initialized but exposed no CPU/GPU sensors
            // (e.g. running without administrator rights) — fall back to the
            // vendor snapshot instead of rendering empty panels.
        }

        return await BuildVendorSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> BuildLhmSnapshotAsync(SensorsGroupController group, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cpuTempTask = group.GetCpuTemperatureAsync();
        var cpuUsageTask = group.GetCpuUsageAsync();
        var cpuFanTask = group.GetCpuFanSpeedAsync();
        var cpuPowerTask = group.GetCpuPowerAsync();
        var cpuComponentPowersTask = group.GetCpuComponentPowersAsync();
        var cpuVoltageTask = group.GetCpuVoltageAsync();
        var cpuClockTask = group.GetCpuCoreClockAsync();
        var cpuPClockTask = group.GetCpuPCoreClockAsync();
        var cpuEClockTask = group.GetCpuECoreClockAsync();
        var gpuUsageTask = group.GetGpuUsageAsync();
        var gpuTempTask = group.GetGpuTemperatureAsync();
        var gpuClockTask = group.GetGpuCoreClockAsync();
        var gpuMemoryClockTask = group.GetGpuMemoryClockAsync();
        var gpuPowerTask = group.GetGpuPowerAsync();
        var gpuVoltageTask = group.GetGpuVoltageAsync();
        var gpuVramTempTask = group.GetGpuVramTemperatureAsync();
        var gpuHotSpotTask = group.GetGpuHotSpotTemperatureAsync();
        var gpuVramUtilTask = group.GetGpuVramUtilizationAsync();
        var gpuVramUsedTask = group.GetGpuVramUsedAsync();
        var gpuVramTotalTask = group.GetGpuVramTotalAsync();
        var gpuPcieRxTask = group.GetGpuPcieRxThroughputAsync();
        var gpuPcieTxTask = group.GetGpuPcieTxThroughputAsync();
        var gpuFanTask = group.GetGpuFanSpeedAsync();
        var memUsageTask = group.GetMemoryUsageAsync();
        var memUsedTask = group.GetMemoryUsedAsync();
        var memTotalTask = group.GetMemoryTotalAsync();
        var memMaxTempTask = group.GetHighestMemoryTemperatureAsync();
        var motherboardMaxTempTask = group.GetHighestMotherboardTemperatureAsync();
        var ssdTempsTask = group.GetSsdTemperaturesAsync();
        var cpuNameTask = group.GetCpuNameAsync();
        var gpuNameTask = group.GetGpuNameAsync();
        var gpuIsIntegratedTask = group.IsCurrentGpuIntegratedAsync();

        await Task.WhenAll(
            cpuTempTask, cpuUsageTask, cpuFanTask, cpuPowerTask, cpuComponentPowersTask,
            cpuVoltageTask, cpuClockTask, cpuPClockTask, cpuEClockTask,
            gpuUsageTask, gpuTempTask, gpuClockTask, gpuMemoryClockTask, gpuPowerTask,
            gpuVoltageTask, gpuVramTempTask, gpuHotSpotTask, gpuVramUtilTask,
            gpuVramUsedTask, gpuVramTotalTask, gpuPcieRxTask, gpuPcieTxTask, gpuFanTask,
            memUsageTask, memUsedTask, memTotalTask, memMaxTempTask, motherboardMaxTempTask,
            ssdTempsTask, cpuNameTask, gpuNameTask, gpuIsIntegratedTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var cpuTemperature = cpuTempTask.Result;
        var cpuUsage = cpuUsageTask.Result;
        var cpuPower = cpuPowerTask.Result;
        var cpuVoltage = cpuVoltageTask.Result;
        var cpuClock = cpuClockTask.Result;
        var cpuFan = cpuFanTask.Result;
        var gpuFan = gpuFanTask.Result;
        var gpuUsage = gpuUsageTask.Result;
        var gpuTemp = gpuTempTask.Result;
        var gpuClock = gpuClockTask.Result;
        var gpuMemoryClock = gpuMemoryClockTask.Result;
        var gpuPower = gpuPowerTask.Result;
        var gpuVoltage = gpuVoltageTask.Result;

        // Complement missing CPU/GPU metrics via vendor controllers (EC WMI / NVAPI / Performance Counters).
        // LHM can expose CPU utilization while omitting package temperature, clock, power, or fan sensors.
        if (cpuTemperature <= 0 || cpuUsage < 0 || cpuPower <= 0 || cpuVoltage <= 0 || cpuClock < 0 ||
            cpuFan <= 0 || gpuFan <= 0 || gpuUsage < 0 || gpuTemp <= 0 || gpuClock < 0 || gpuPower <= 0 || gpuVoltage <= 0)
        {
            try
            {
                var vendor = GetVendorSensors();
                SensorsData? vendorData = null;
                try
                {
                    vendorData = await vendor.GetDataAsync(detailed: true).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                        UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"Vendor sensor data complement failed: {ex.Message}");
                }

                if (vendorData is { } vendorDataValue)
                {
                    if (cpuTemperature <= 0 && vendorDataValue.CPU.Temperature > 0)
                        cpuTemperature = vendorDataValue.CPU.Temperature;
                    if (cpuUsage < 0 && vendorDataValue.CPU.Utilization >= 0)
                        cpuUsage = vendorDataValue.CPU.Utilization;
                    if (cpuPower <= 0 && vendorDataValue.CPU.Wattage > 0)
                        cpuPower = vendorDataValue.CPU.Wattage;
                    if (cpuVoltage <= 0 && vendorDataValue.CPU.Voltage > 0)
                        cpuVoltage = (float)vendorDataValue.CPU.Voltage;
                    if (cpuClock < 0 && vendorDataValue.CPU.CoreClock >= 0)
                        cpuClock = vendorDataValue.CPU.CoreClock;
                    if (cpuFan <= 0 && vendorDataValue.CPU.FanSpeed > 0)
                        cpuFan = vendorDataValue.CPU.FanSpeed;
                }

                if (cpuFan <= 0 || gpuFan <= 0)
                {
                    var vendorFans = await vendor.GetFanSpeedsAsync().ConfigureAwait(false);
                    if (cpuFan <= 0 && vendorFans.cpuFanSpeed > 0)
                        cpuFan = vendorFans.cpuFanSpeed;
                    if (gpuFan <= 0 && vendorFans.gpuFanSpeed > 0)
                        gpuFan = vendorFans.gpuFanSpeed;
                }

                if (vendorData is { } gpuVendorData)
                {
                    if (gpuUsage < 0 && gpuVendorData.GPU.Utilization >= 0)
                        gpuUsage = gpuVendorData.GPU.Utilization;
                    if (gpuTemp <= 0 && gpuVendorData.GPU.Temperature > 0)
                        gpuTemp = gpuVendorData.GPU.Temperature;
                    if (gpuClock < 0 && gpuVendorData.GPU.CoreClock >= 0)
                        gpuClock = gpuVendorData.GPU.CoreClock;
                    if (gpuMemoryClock < 0 && gpuVendorData.GPU.MemoryClock >= 0)
                        gpuMemoryClock = gpuVendorData.GPU.MemoryClock;
                    if (gpuPower <= 0 && gpuVendorData.GPU.Wattage > 0)
                        gpuPower = gpuVendorData.GPU.Wattage;
                    if (gpuVoltage <= 0 && gpuVendorData.GPU.Voltage > 0)
                        gpuVoltage = (float)gpuVendorData.GPU.Voltage;
                    if (gpuFan <= 0 && gpuVendorData.GPU.FanSpeed > 0)
                        gpuFan = gpuVendorData.GPU.FanSpeed;
                }
            }
            catch (Exception ex)
            {
                if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                    UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"Vendor sensor complement failed: {ex.Message}");
            }
        }

        // On Hybrid / Optimus laptops when discrete GPU is sleeping in low-power idle state
        if (gpuUsage < 0 && (group.IsHybrid || gpuIsIntegratedTask.Result))
        {
            gpuUsage = 0f;
            if (gpuClock < 0) gpuClock = 0f;
            if (gpuPower < 0) gpuPower = 0f;
        }

        var ssdTemps = ssdTempsTask.Result;
        var battery = await BuildBatteryAsync(cancellationToken).ConfigureAwait(false);
        var (cpuName, gpuName) = await MergeHardwareNamesAsync(
            NullIf(cpuNameTask.Result, "UNKNOWN"),
            NullIf(gpuNameTask.Result, "UNKNOWN"),
            cancellationToken).ConfigureAwait(false);

        // LibreHardwareMonitor may initialize without exposing any CPU/GPU
        // sensors (e.g. without administrator rights). Treat that as "no data"
        // so the caller falls back to the vendor snapshot.
        var hasCpuData = HasValue(cpuTemperature) || HasValue(cpuUsage) ||
                         HasValue(cpuClock) || HasValue(cpuFan);
        var hasGpuData = HasValue(gpuTemp) || HasValue(gpuUsage) ||
                         HasValue(gpuClock) || HasValue(gpuFan);
        if (!hasCpuData && !hasGpuData)
            return null;

        return CreateSnapshot(
            source: "LibreHardwareMonitor",
            initialized: true,
            isHybrid: group.IsHybrid,
            cpuName: cpuName,
            gpuName: gpuName,
            gpuIsIntegrated: gpuIsIntegratedTask.Result,
            cpu: new
            {
                temperature = NullIf(cpuTemperature),
                usage = NullIf(cpuUsage),
                fanSpeed = NullIf(cpuFan),
                power = NullIf(cpuPower),
                powerCores = NullIf(cpuComponentPowersTask.Result.cores),
                powerMemory = NullIf(cpuComponentPowersTask.Result.memory),
                powerPlatform = NullIf(cpuComponentPowersTask.Result.platform),
                voltage = NullIf(cpuVoltage),
                coreClockMax = NullIf(cpuClock),
                coreClockAvg = (float?)null,
                pCoreClock = NullIf(cpuPClockTask.Result),
                eCoreClock = NullIf(cpuEClockTask.Result),
            },
            gpu: new
            {
                usage = NullIf(gpuUsage),
                temperature = NullIf(gpuTemp),
                coreClock = NullIf(gpuClock),
                memoryClock = NullIf(gpuMemoryClock),
                power = NullIf(gpuPower),
                voltage = NullIf(gpuVoltage),
                vramTemperature = NullIf(gpuVramTempTask.Result),
                hotSpotTemperature = NullIf(gpuHotSpotTask.Result),
                vramUtilization = NullIf(gpuVramUtilTask.Result),
                // GetGpuVramUsed/TotalAsync return GiB; bridge fields are MiB.
                vramUsedMb = GigabytesToMegabytes(gpuVramUsedTask.Result),
                vramTotalMb = GigabytesToMegabytes(gpuVramTotalTask.Result),
                pcieRxThroughput = NullIf(gpuPcieRxTask.Result),
                pcieTxThroughput = NullIf(gpuPcieTxTask.Result),
                fanSpeed = NullIf(gpuFan),
            },
            memory: MapMemory(memUsageTask.Result, memUsedTask.Result, memTotalTask.Result, memMaxTempTask.Result),
            battery: battery,
            motherboard: new
            {
                highestTemperature = NullIfTemperature(motherboardMaxTempTask.Result),
            },
            storage: new
            {
                temperatures = new float?[] { NullIf(ssdTemps.Item1), NullIf(ssdTemps.Item2) },
            });
    }

    private static async Task<object> BuildVendorSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SensorsData data;
        try
        {
            data = await GetVendorSensors().GetDataAsync(detailed: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            var batteryOnly = await BuildBatteryAsync(cancellationToken).ConfigureAwait(false);
            return CreateSnapshot(
                source: "vendor",
                initialized: false,
                isHybrid: false,
                cpuName: null,
                gpuName: null,
                gpuIsIntegrated: false,
                cpu: CreateEmptyCpu(),
                gpu: CreateEmptyGpu(),
                memory: CreateEmptyMemory(),
                battery: batteryOnly);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var battery = await BuildBatteryAsync(cancellationToken).ConfigureAwait(false);
        var group = GetSensorsGroup();
        var (cpuName, gpuName) = await MergeHardwareNamesAsync(null, null, cancellationToken).ConfigureAwait(false);
        var gpuIsIntegrated = false;
        object memory = CreateEmptyMemory();
        object? motherboard = null;
        object? storage = null;
        try
        {
            if (group.IsLibreHardwareMonitorInitialized())
            {
                var gpuIntegratedTask = group.IsCurrentGpuIntegratedAsync();
                var memUsageTask = group.GetMemoryUsageAsync();
                var memUsedTask = group.GetMemoryUsedAsync();
                var memTotalTask = group.GetMemoryTotalAsync();
                var memMaxTempTask = group.GetHighestMemoryTemperatureAsync();
                var motherboardMaxTempTask = group.GetHighestMotherboardTemperatureAsync();
                var ssdTempsTask = group.GetSsdTemperaturesAsync();

                await Task.WhenAll(
                    gpuIntegratedTask, memUsageTask, memUsedTask, memTotalTask,
                    memMaxTempTask, motherboardMaxTempTask, ssdTempsTask).ConfigureAwait(false);

                gpuIsIntegrated = gpuIntegratedTask.Result;
                memory = MapMemory(memUsageTask.Result, memUsedTask.Result, memTotalTask.Result, memMaxTempTask.Result);
                motherboard = new
                {
                    highestTemperature = NullIfTemperature(motherboardMaxTempTask.Result),
                };
                var ssdTemps = ssdTempsTask.Result;
                storage = new
                {
                    temperatures = new float?[] { NullIf(ssdTemps.Item1), NullIf(ssdTemps.Item2) },
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Best-effort; vendor snapshot still returns sensor readings.
        }

        return CreateSnapshot(
            source: "vendor",
            initialized: true,
            isHybrid: false,
            cpuName,
            gpuName,
            gpuIsIntegrated,
            cpu: new
            {
                temperature = NullIf(data.CPU.Temperature),
                usage = NullIf(data.CPU.Utilization),
                fanSpeed = NullIf(data.CPU.FanSpeed),
                power = NullIf(data.CPU.Wattage),
                powerCores = (float?)null,
                powerMemory = (float?)null,
                powerPlatform = (float?)null,
                voltage = NullIfVoltage(data.CPU.Voltage),
                coreClockMax = NullIf(data.CPU.CoreClock),
                coreClockAvg = (float?)null,
                pCoreClock = (int?)null,
                eCoreClock = (int?)null,
            },
            gpu: new
            {
                usage = NullIf(data.GPU.Utilization),
                temperature = NullIf(data.GPU.Temperature),
                coreClock = NullIf(data.GPU.CoreClock),
                memoryClock = NullIf(data.GPU.MemoryClock),
                power = NullIf(data.GPU.Wattage),
                voltage = NullIfVoltage(data.GPU.Voltage),
                vramTemperature = (float?)null,
                hotSpotTemperature = (float?)null,
                vramUtilization = (float?)null,
                vramUsedMb = (float?)null,
                vramTotalMb = (float?)null,
                pcieRxThroughput = (float?)null,
                pcieTxThroughput = (float?)null,
                fanSpeed = NullIf(data.GPU.FanSpeed),
            },
            memory: memory,
            battery: battery,
            motherboard: motherboard,
            storage: storage);
    }

    /// <summary>
    /// Avalonia SensorsControl.RefreshBattery parity: Battery.GetBatteryInformation +
    /// Power.IsPowerAdapterConnectedAsync for low-wattage adapter warning.
    /// Health is 0..1 (BatteryHealth is already 0..100 percent).
    /// Charge/discharge rates stay in milliwatts to match Electron formatRate.
    /// </summary>

    private static async Task<object?> BuildBatteryAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = Battery.GetBatteryInformation();
            var adapter = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
            return new
            {
                chargeLevel = info.BatteryPercentage >= 0 ? (int?)info.BatteryPercentage : null,
                health = info.DesignCapacity > 0 ? (double?)(info.BatteryHealth / 100.0) : null,
                temperature = info.BatteryTemperatureC,
                avgTemperature = info.AvgTemperatureC,
                chargeRate = info.DischargeRate,
                minDischargeRate = info.MinDischargeRate == int.MaxValue ? null : (int?)info.MinDischargeRate,
                maxDischargeRate = (int?)info.MaxDischargeRate,
                voltage = (double?)null,
                designCapacity = info.DesignCapacity > 0 ? (int?)info.DesignCapacity : null,
                fullChargeCapacity = info.FullChargeCapacity > 0 ? (int?)info.FullChargeCapacity : null,
                cycleCount = info.CycleCount >= 0 ? (int?)info.CycleCount : null,
                manufactureDate = info.ManufactureDate?.ToString("yyyy-MM-dd"),
                firstUseDate = info.FirstUseDate?.ToString("yyyy-MM-dd"),
                isCharging = info.IsCharging,
                isLowBattery = info.IsLowBattery,
                isLowPowerAdapter = adapter == PowerAdapterStatus.ConnectedLowWattage,
                modelName = string.IsNullOrWhiteSpace(info.ModelName) ? null : info.ModelName,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    // ── handlers ────────────────────────────────────────────────────────────

    private static async Task<BridgeResult> HandleGetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = GetSensorsGroup();
            var initialized = group.IsLibreHardwareMonitorInitialized();
            string? cpuName = null;
            string? gpuName = null;
            var gpuIsIntegrated = false;
            if (initialized)
            {
                cpuName = NullIf(await group.GetCpuNameAsync().ConfigureAwait(false), "UNKNOWN");
                gpuName = NullIf(await group.GetGpuNameAsync().ConfigureAwait(false), "UNKNOWN");
                gpuIsIntegrated = await group.IsCurrentGpuIntegratedAsync().ConfigureAwait(false);
            }

            (cpuName, gpuName) = await MergeHardwareNamesAsync(cpuName, gpuName, cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new
            {
                initialized,
                isHybrid = group.IsHybrid,
                cpuName,
                gpuName,
                gpuIsIntegrated,
                initialState = group.InitialState.ToString(),
                elevated = IsProcessElevated(),
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleGetSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await BuildSnapshotAsync(cancellationToken).ConfigureAwait(false);
            return BridgeResult.Ok(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleGetDetailedAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Electron types this as SensorSnapshot; return the vendor snapshot
            // with the same field names (usage/power) rather than utilization/wattage.
            var snapshot = await BuildVendorSnapshotAsync(cancellationToken).ConfigureAwait(false);
            return BridgeResult.Ok(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool IsProcessElevated()
    {
        if (!OperatingSystem.IsWindows())
            return true;
        using var identity = global::System.Security.Principal.WindowsIdentity.GetCurrent();
        return new global::System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(global::System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static bool HasValue(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>
    /// Cheap probe for readable LHM sensors (CPU/GPU temperature or usage).
    /// Returns false when LHM is initialized but has no readable data, so the
    /// caller can fall back to the stable vendor snapshot path.
    /// </summary>

    private static async Task<bool> LhmHasSensorDataAsync(SensorsGroupController group, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cpuTemp = await group.GetCpuTemperatureAsync().ConfigureAwait(false);
            var cpuUsage = await group.GetCpuUsageAsync().ConfigureAwait(false);
            var gpuTemp = await group.GetGpuTemperatureAsync().ConfigureAwait(false);
            var gpuUsage = await group.GetGpuUsageAsync().ConfigureAwait(false);
            return HasValue(cpuTemp) || HasValue(cpuUsage) || HasValue(gpuTemp) || HasValue(gpuUsage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(string? CpuName, string? GpuName)> MergeHardwareNamesAsync(
        string? cpuName,
        string? gpuName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cpuName) && !string.IsNullOrWhiteSpace(gpuName))
            return (cpuName, gpuName);

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var inventory = await HardwareInventoryProvider.ReadAsync().ConfigureAwait(false);
            cpuName ??= NullIfBlank(inventory.PrimaryProcessorName);
            gpuName ??= NullIfBlank(inventory.PrimaryVideoControllerName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // WMI inventory is optional; keep any names already resolved.
        }

        return (cpuName, gpuName);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Converts LibreHardwareMonitor Data / GetGpuVram* gigabyte readings into
    /// the MiB values expected by Electron <c>*Mb</c> snapshot fields.
    /// </summary>

    internal static float? GigabytesToMegabytes(float gigabytes)
        => HasValue(gigabytes) ? gigabytes * 1024f : null;

    private static object CreateSnapshot(
        string source,
        bool initialized,
        bool isHybrid,
        string? cpuName,
        string? gpuName,
        bool gpuIsIntegrated,
        object cpu,
        object gpu,
        object memory,
        object? battery,
        object? motherboard = null,
        object? storage = null) => new
    {
        ts = DateTime.UtcNow,
        source,
        initialized,
        isHybrid,
        info = new { cpuName, gpuName, gpuIsIntegrated },
        cpu,
        gpu,
        memory,
        battery,
        motherboard = motherboard ?? new { highestTemperature = (double?)null },
        storage = storage ?? new { temperatures = new float?[] { null, null } },
    };

    private static object CreateEmptyCpu() => new
    {
        temperature = (float?)null,
        usage = (float?)null,
        fanSpeed = (float?)null,
        power = (float?)null,
        powerCores = (float?)null,
        powerMemory = (float?)null,
        powerPlatform = (float?)null,
        voltage = (float?)null,
        coreClockMax = (float?)null,
        coreClockAvg = (float?)null,
        pCoreClock = (int?)null,
        eCoreClock = (int?)null,
    };

    private static object CreateEmptyGpu() => new
    {
        usage = (float?)null,
        temperature = (float?)null,
        coreClock = (float?)null,
        memoryClock = (float?)null,
        power = (float?)null,
        voltage = (float?)null,
        vramTemperature = (float?)null,
        hotSpotTemperature = (float?)null,
        vramUtilization = (float?)null,
        vramUsedMb = (float?)null,
        vramTotalMb = (float?)null,
        pcieRxThroughput = (float?)null,
        pcieTxThroughput = (float?)null,
        fanSpeed = (float?)null,
    };

    private static object CreateEmptyMemory() => new
    {
        usage = (int?)null,
        usedMb = (int?)null,
        totalMb = (int?)null,
        highestTemperature = (double?)null,
    };

    private static float? NullIf(float value) => HasValue(value) ? value : null;
    private static double? NullIfVoltage(double value) => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? null : value;
    private static double? NullIfTemperature(double value) => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? null : value;
    private static int? NullIf(int value) => value < 0 ? null : value;
    private static string? NullIf(string value, string sentinel) => value == sentinel ? null : value;

    internal static object MapMemory(float usage, float usedGb, float totalGb, double temperature) => new
    {
        usage = NullIf(usage),
        usedMb = GigabytesToMegabytes(usedGb),
        totalMb = GigabytesToMegabytes(totalGb),
        highestTemperature = NullIfTemperature(temperature),
    };
}
