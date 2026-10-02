using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Sensor bridge: LibreHardwareMonitor snapshot + subscription, vendor fallback,
/// FPS monitoring and sensor-related settings.
/// </summary>
public static class SensorsHandlers
{
    private sealed class SensorSubscriber
    {
        public static object Named(string id) => id;
    }

    private static readonly object SubscribeLock = new();
    private static readonly HashSet<string> VendorSubscriberIds = new(StringComparer.Ordinal);
    private static readonly HashSet<string> LhmSubscriberIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> VendorIntervals = new(StringComparer.Ordinal);
    private static readonly object FpsLock = new();
    private static int _fpsSubscriberCount;
    private static SensorsGroupController? _subscribedGroup;
    private static BridgeRpcServer? _sensorsRpc;
    private static System.Threading.Timer? _vendorPollTimer;
    private static CancellationTokenSource? _vendorPollCts;
    private static double _vendorPollIntervalSec = 1.0;
    private static double _lhmIntervalSec = 1.0;
    private static int _snapshotPublishInFlight;
    private static bool _uiActivityHooked;
    private static FpsSensorController? _subscribedFpsController;
    private static BridgeRpcServer? _fpsRpc;

    public static void Register(BridgeRpcServer rpc)
    {
        EnsureUiActivityHook();
        rpc.RegisterHandler("sensors.getStatus", (_, ct) => HandleGetStatusAsync(ct));
        rpc.RegisterHandler("sensors.getSnapshot", (_, ct) => HandleGetSnapshotAsync(ct));
        rpc.RegisterHandler("sensors.getDetailed", (_, ct) => HandleGetDetailedAsync(ct));
        rpc.RegisterHandler("sensors.subscribe", (request, ct) => HandleSubscribeAsync(request, rpc, ct));
        rpc.RegisterHandler("sensors.unsubscribe", (request, ct) => HandleUnsubscribeAsync(request, ct));
        rpc.RegisterHandler("sensors.getSettings", (_, ct) => HandleGetSettingsAsync(ct));
        rpc.RegisterHandler("sensors.setSettings", (request, ct) => HandleSetSettingsAsync(request, rpc, ct));
        rpc.RegisterHandler("sensors.getFps", (_, ct) => HandleGetFpsAsync(ct));
        rpc.RegisterHandler("sensors.subscribeFps", (request, ct) => HandleSubscribeFpsAsync(request, rpc, ct));
        rpc.RegisterHandler("sensors.unsubscribeFps", (_, ct) => HandleUnsubscribeFpsAsync(ct));
    }

    private static void EnsureUiActivityHook()
    {
        if (_uiActivityHooked)
            return;
        _uiActivityHooked = true;
        HostUiActivity.Changed += OnUiActivityChanged;
    }

    private static void OnUiActivityChanged(bool active)
    {
        if (active)
        {
            _ = ResumeAfterBackgroundAsync();
            return;
        }

        PauseFpsForBackground();

        if (AutomationNeedsHardwareSensors())
        {
            // Keep the LHM producer so automation reads fresh cached snapshots.
            // sensors.updated is already suppressed while HostUiActivity is false.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
            return;
        }

        lock (SubscribeLock)
            PauseSensorProductionLocked();

        try
        {
            IoCContainer.TryResolve<SensorsGroupController>()?.ReleaseHardwareForBackground();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"background hardware release failed: {ex.Message}", ex);
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, blocking: false);
    }

    private static bool AutomationNeedsHardwareSensors()
    {
        try
        {
            var processor = IoCContainer.TryResolve<AutomationProcessor>();
            return processor?.HasHardwareSensorTriggers() == true;
        }
        catch
        {
            return true;
        }
    }

    private static async Task ResumeAfterBackgroundAsync()
    {
        try
        {
            var group = IoCContainer.TryResolve<SensorsGroupController>();
            if (group is not null && !group.IsLibreHardwareMonitorInitialized())
                _ = await group.EnsureHardwareAfterBackgroundAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"background hardware restore failed: {ex.Message}", ex);
        }

        lock (SubscribeLock)
            ResumeSensorProductionLocked();

        ResumeFpsAfterBackground();
    }

    private static void PauseFpsForBackground()
    {
        lock (FpsLock)
        {
            if (_subscribedFpsController is { } controller)
            {
                controller.FpsDataUpdated -= OnFpsDataUpdated;
                controller.StopMonitoring();
            }
        }
    }

    private static void ResumeFpsAfterBackground()
    {
        FpsSensorController? controller;
        lock (FpsLock)
        {
            if (_fpsSubscriberCount <= 0 || _subscribedFpsController is null)
                return;
            controller = _subscribedFpsController;
            controller.FpsDataUpdated -= OnFpsDataUpdated;
            controller.FpsDataUpdated += OnFpsDataUpdated;
        }

        _ = controller.StartMonitoringAsync();
    }

    private static void PauseSensorProductionLocked()
    {
        if (_subscribedGroup is not null)
        {
            foreach (var id in LhmSubscriberIds)
                _subscribedGroup.Stop(SensorSubscriber.Named(id));
            _subscribedGroup.SensorsUpdated -= OnSensorsUpdated;
        }

        CancelVendorTimer();
    }

    private static void ResumeSensorProductionLocked()
    {
        if (!HostUiActivity.IsActive)
            return;

        if (LhmSubscriberIds.Count > 0)
        {
            var group = _subscribedGroup ?? GetSensorsGroup();
            _subscribedGroup = group;
            group.SensorsUpdated -= OnSensorsUpdated;
            group.SensorsUpdated += OnSensorsUpdated;
            var interval = TimeSpan.FromSeconds(_lhmIntervalSec);
            foreach (var id in LhmSubscriberIds)
                group.Start(SensorSubscriber.Named(id), interval);
        }

        if (VendorSubscriberIds.Count > 0 && _sensorsRpc is not null)
            StartOrUpdateVendorTimer(_sensorsRpc);
    }

    private static SensorsGroupController GetSensorsGroup()
        => IoCContainer.Resolve<SensorsGroupController>();

    private static SensorsController GetVendorSensors()
        => IoCContainer.Resolve<SensorsController>();

    private static FpsSensorController GetFpsController()
        => IoCContainer.Resolve<FpsSensorController>();

    // ── snapshot assembly ───────────────────────────────────────────────────

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

        return new
        {
            ts = DateTime.UtcNow,
            source = "LibreHardwareMonitor",
            initialized = true,
            isHybrid = group.IsHybrid,
            info = new
            {
                cpuName,
                gpuName,
                gpuIsIntegrated = gpuIsIntegratedTask.Result,
            },
            cpu = new
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
            gpu = new
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
            memory = new
            {
                usage = NullIf(memUsageTask.Result),
                // LHM SensorType.Data is GiB; bridge fields are MiB.
                usedMb = GigabytesToMegabytes(memUsedTask.Result),
                totalMb = GigabytesToMegabytes(memTotalTask.Result),
                highestTemperature = NullIfTemperature(memMaxTempTask.Result),
            },
            battery,
            motherboard = new
            {
                highestTemperature = NullIfTemperature(motherboardMaxTempTask.Result),
            },
            storage = new
            {
                temperatures = new float?[] { NullIf(ssdTemps.Item1), NullIf(ssdTemps.Item2) },
            },
        };
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
                memory = new
                {
                    usage = NullIf(memUsageTask.Result),
                    usedMb = GigabytesToMegabytes(memUsedTask.Result),
                    totalMb = GigabytesToMegabytes(memTotalTask.Result),
                    highestTemperature = NullIfTemperature(memMaxTempTask.Result),
                };
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

    private static string ReadSubscriberId(BridgeRequest request)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Object &&
            request.Parameters.TryGetProperty("subscriberId", out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            var id = prop.GetString();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        return "ui";
    }

    private static async Task EnsureLibreHardwareMonitorAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
        if (!applicationSettings.Store.EnableHardwareSensors)
            return;

        var group = GetSensorsGroup();
        if (group.IsLibreHardwareMonitorInitialized())
            return;

        try
        {
            _ = await group.IsSupportedAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Vendor fallback in HandleSubscribeAsync.
        }
    }

    private static void StopVendorTimerIfIdle()
    {
        lock (SubscribeLock)
        {
            if (VendorSubscriberIds.Count > 0)
                return;
            CancelVendorTimer();
        }
    }

    private static void CancelVendorTimer()
    {
        _vendorPollCts?.Cancel();
        _vendorPollCts?.Dispose();
        _vendorPollCts = null;
        _vendorPollTimer?.Dispose();
        _vendorPollTimer = null;
    }

    private static void StartOrUpdateVendorTimer(BridgeRpcServer rpc)
    {
        lock (SubscribeLock)
        {
            if (VendorSubscriberIds.Count == 0)
            {
                CancelVendorTimer();
                return;
            }

            _vendorPollIntervalSec = VendorIntervals.Values.Min();
            _sensorsRpc = rpc;
            CancelVendorTimer();
            _vendorPollCts = new CancellationTokenSource();
            var pollToken = _vendorPollCts.Token;
            _vendorPollTimer = new System.Threading.Timer(
                async _ =>
                {
                    var rpcRef = _sensorsRpc;
                    if (rpcRef is null || pollToken.IsCancellationRequested || !HostUiActivity.IsActive)
                        return;
                    try
                    {
                        var snapshot = await BuildSnapshotAsync(pollToken).ConfigureAwait(false);
                        if (pollToken.IsCancellationRequested)
                            return;
                        rpcRef.Publish("sensors.updated", snapshot);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                            UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"sensors.updated vendor publish failed: {ex.Message}", ex);
                    }
                },
                null,
                TimeSpan.FromSeconds(_vendorPollIntervalSec),
                TimeSpan.FromSeconds(_vendorPollIntervalSec));
        }
    }

    private static async Task<BridgeResult> HandleSubscribeAsync(BridgeRequest request, BridgeRpcServer rpc, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var intervalSec = 1.0;
            if (request.Parameters.TryGetProperty("intervalSec", out var intervalProp) &&
                intervalProp.ValueKind == JsonValueKind.Number)
            {
                intervalSec = intervalProp.GetDouble();
            }

            if (!double.IsFinite(intervalSec))
                intervalSec = 1.0;
            intervalSec = Math.Clamp(intervalSec, 0.5, 30.0);
            _lhmIntervalSec = intervalSec;
            var subscriberId = ReadSubscriberId(request);
            var subscriber = SensorSubscriber.Named(subscriberId);

            await EnsureLibreHardwareMonitorAsync(cancellationToken).ConfigureAwait(false);

            var group = GetSensorsGroup();

            // LibreHardwareMonitor path: the group's producer loop raises
            // SensorsUpdated only after LHM initialization succeeds.
            // LHM may be marked initialized while exposing no readable sensors
            // (e.g. NVAPI/performance counters unavailable) — in that case the
            // vendor fallback below is stable, whereas the LHM loop would
            // publish nothing but null frames.
            if (group.IsLibreHardwareMonitorInitialized() && await LhmHasSensorDataAsync(group, cancellationToken).ConfigureAwait(false))
            {
                lock (SubscribeLock)
                {
                    LhmSubscriberIds.Add(subscriberId);
                    VendorSubscriberIds.Remove(subscriberId);
                    VendorIntervals.Remove(subscriberId);
                }
                StopVendorTimerIfIdle();

                _subscribedGroup = group;
                _sensorsRpc = rpc;
                group.SensorsUpdated -= OnSensorsUpdated;
                group.SensorsUpdated += OnSensorsUpdated;
                if (HostUiActivity.IsActive)
                    group.Start(subscriber, TimeSpan.FromSeconds(intervalSec));

                return BridgeResult.Ok(new { subscribed = true, effectiveIntervalSec = intervalSec });
            }

            group.Stop(subscriber);
            lock (SubscribeLock)
            {
                LhmSubscriberIds.Remove(subscriberId);
                VendorSubscriberIds.Add(subscriberId);
                VendorIntervals[subscriberId] = intervalSec;
                if (LhmSubscriberIds.Count == 0)
                {
                    group.SensorsUpdated -= OnSensorsUpdated;
                    _subscribedGroup = null;
                }
            }
            if (HostUiActivity.IsActive)
                StartOrUpdateVendorTimer(rpc);

            return BridgeResult.Ok(new { subscribed = true, effectiveIntervalSec = intervalSec });
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

    private static Task<BridgeResult> HandleUnsubscribeAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var subscriberId = ReadSubscriberId(request);
            var subscriber = SensorSubscriber.Named(subscriberId);
            var group = GetSensorsGroup();
            group.Stop(subscriber);
            lock (SubscribeLock)
            {
                VendorSubscriberIds.Remove(subscriberId);
                VendorIntervals.Remove(subscriberId);
                LhmSubscriberIds.Remove(subscriberId);
                if (LhmSubscriberIds.Count == 0)
                {
                    group.SensorsUpdated -= OnSensorsUpdated;
                    _subscribedGroup = null;
                }

                if (VendorSubscriberIds.Count == 0 && LhmSubscriberIds.Count == 0)
                    _sensorsRpc = null;
            }
            StopVendorTimerIfIdle();
            if (VendorSubscriberIds.Count > 0 && _sensorsRpc is not null)
                StartOrUpdateVendorTimer(_sensorsRpc);
            return Task.FromResult(BridgeResult.Ok(new { unsubscribed = true }));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static async void OnSensorsUpdated(object? sender, EventArgs args)
    {
        var group = _subscribedGroup;
        var rpc = _sensorsRpc;
        if (!HostUiActivity.IsActive || group is null || rpc is null)
            return;

        if (Interlocked.CompareExchange(ref _snapshotPublishInFlight, 1, 0) != 0)
            return;

        try
        {
            var snapshot = await BuildLhmSnapshotAsync(group, CancellationToken.None).ConfigureAwait(false)
                           ?? await BuildVendorSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            if (snapshot is null)
                return;
            rpc.Publish("sensors.updated", snapshot);
        }
        catch (Exception ex)
        {
            if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"sensors.updated publish failed: {ex.Message}", ex);
        }
        finally
        {
            Volatile.Write(ref _snapshotPublishInFlight, 0);
        }
    }

    private static Task<BridgeResult> HandleGetSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
            var osdSettings = IoCContainer.Resolve<OsdSettings>();
            var hardwareSensorSettings = IoCContainer.Resolve<HardwareSensorSettings>();

            return Task.FromResult(BridgeResult.Ok(new
            {
                enableHardwareSensors = applicationSettings.Store.EnableHardwareSensors,
                osdRefreshIntervalSec = osdSettings.Store.OsdRefreshInterval,
                selectedGpuIsIgpu = hardwareSensorSettings.Store.SelectedGpuIsIgpu,
                showCpuAverageFrequency = hardwareSensorSettings.Store.ShowCpuAverageFrequency,
                displayMemoryInGigabytes = hardwareSensorSettings.Store.DisplayMemoryInGigabytes,
                visibleSections = hardwareSensorSettings.Store.VisibleSections,
                sectionOrder = hardwareSensorSettings.Store.SectionOrder,
            }));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static async Task<BridgeResult> HandleSetSettingsAsync(BridgeRequest request, BridgeRpcServer rpc, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hardwareSensorSettings = IoCContainer.Resolve<HardwareSensorSettings>();
            var osdSettings = IoCContainer.Resolve<OsdSettings>();

            var applicationChanged = false;
            var hardwareChanged = false;
            var osdChanged = false;

            if (TryGetBoolean(request, "enableHardwareSensors", out var enabled))
            {
                var feature = IoCContainer.Resolve<HardwareSensorsFeature>();
                await feature.SetStateAsync(enabled ? HardwareSensorsState.On : HardwareSensorsState.Off, cancellationToken).ConfigureAwait(false);
                applicationChanged = true;
            }

            if (TryGetBoolean(request, "selectedGpuIsIgpu", out var selectedGpuIsIgpu))
            {
                hardwareSensorSettings.Store.SelectedGpuIsIgpu = selectedGpuIsIgpu;
                GetSensorsGroup().SelectedGpuIsIgpu = selectedGpuIsIgpu;
                hardwareChanged = true;
            }

            if (TryGetBoolean(request, "showCpuAverageFrequency", out var showCpuAverageFrequency))
            {
                hardwareSensorSettings.Store.ShowCpuAverageFrequency = showCpuAverageFrequency;
                GetSensorsGroup().ShowAverageCpuFrequency = showCpuAverageFrequency;
                hardwareChanged = true;
            }

            if (TryGetBoolean(request, "displayMemoryInGigabytes", out var displayMemoryInGigabytes))
            {
                hardwareSensorSettings.Store.DisplayMemoryInGigabytes = displayMemoryInGigabytes;
                hardwareChanged = true;
            }

            if (TryGetDouble(request, "osdRefreshIntervalSec", out var osdRefreshIntervalSec))
            {
                osdSettings.Store.OsdRefreshInterval = Math.Clamp(osdRefreshIntervalSec, 0.1, 10);
                osdChanged = true;
            }

            if (TryGetStringArray(request, "visibleSections", out var visibleSections))
            {
                hardwareSensorSettings.Store.VisibleSections = visibleSections.Length > 0
                    ? visibleSections
                    : ["CPU", "Battery", "GPU"];
                hardwareChanged = true;
            }

            if (TryGetStringArray(request, "sectionOrder", out var sectionOrder))
            {
                hardwareSensorSettings.Store.SectionOrder = sectionOrder.Length > 0
                    ? sectionOrder
                    : ["CPU", "Battery", "GPU"];
                hardwareChanged = true;
            }

            if (hardwareChanged)
            {
                hardwareSensorSettings.SynchronizeStore();
                hardwareSensorSettings.NotifySectionsChanged();
                rpc.Publish("settings.changed", new { scope = "hardwareSensors", reason = "set" });
            }

            if (osdChanged)
            {
                osdSettings.SynchronizeStore();
                rpc.Publish("settings.changed", new { scope = "osd", reason = "set" });
            }

            if (applicationChanged)
                rpc.Publish("settings.changed", new { scope = "application", reason = "set" });

            var saved = applicationChanged || hardwareChanged || osdChanged;
            return BridgeResult.Ok(new { saved });
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

    private static Task<BridgeResult> HandleGetFpsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = GetFpsController().GetCurrentFpsData();
            return Task.FromResult(BridgeResult.Ok(MapFpsData(data)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static async Task<BridgeResult> HandleSubscribeFpsAsync(BridgeRequest request, BridgeRpcServer rpc, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var controller = GetFpsController();
            var blacklist = ParseFpsBlacklist(request.Parameters);
            var shouldStart = false;

            lock (FpsLock)
            {
                if (_fpsSubscriberCount == 0)
                {
                    _subscribedFpsController = controller;
                    _fpsRpc = rpc;
                    TryApplyFpsBlacklist(controller, blacklist);
                    controller.FpsDataUpdated -= OnFpsDataUpdated;
                    controller.FpsDataUpdated += OnFpsDataUpdated;
                    shouldStart = true;
                }
            }

            if (shouldStart)
            {
                try
                {
                    await controller.StartMonitoringAsync().ConfigureAwait(false);
                }
                catch
                {
                    lock (FpsLock)
                    {
                        controller.FpsDataUpdated -= OnFpsDataUpdated;
                        _subscribedFpsController = null;
                        _fpsRpc = null;
                    }

                    throw;
                }
            }

            lock (FpsLock)
                _fpsSubscriberCount++;

            return BridgeResult.Ok(new { monitoring = true });
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

    private static Task<BridgeResult> HandleUnsubscribeFpsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var monitoring = false;

            lock (FpsLock)
            {
                _fpsSubscriberCount = Math.Max(0, _fpsSubscriberCount - 1);
                monitoring = _fpsSubscriberCount > 0;
                if (!monitoring && _subscribedFpsController is { } controller)
                {
                    controller.FpsDataUpdated -= OnFpsDataUpdated;
                    controller.StopMonitoring();
                    _subscribedFpsController = null;
                    _fpsRpc = null;
                }
            }

            return Task.FromResult(BridgeResult.Ok(new { monitoring }));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static void OnFpsDataUpdated(object? sender, FpsSensorController.FpsData data)
    {
        if (!HostUiActivity.IsActive || _fpsRpc is null)
            return;

        try
        {
            _fpsRpc.Publish("sensors.fpsUpdated", MapFpsData(data));
        }
        catch (Exception ex)
        {
            if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"sensors.fpsUpdated publish failed: {ex.Message}", ex);
        }
    }

    private static object MapFpsData(FpsSensorController.FpsData data)
    {
        return new
        {
            process = (string?)null,
            fps = ParseFps(data.Fps),
            lowFps = ParseFps(data.LowFps),
            frameTimeMs = ParseFps(data.FrameTime),
        };
    }

    private static double? ParseFps(string value)
        => double.TryParse(value, out var parsed) && parsed >= 0 ? parsed : null;

    // ── helpers ─────────────────────────────────────────────────────────────

    private static string[]? ParseFpsBlacklist(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("blacklist", out var blacklistProp) || blacklistProp.ValueKind != JsonValueKind.Array)
            return null;

        var entries = blacklistProp.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return entries.Length > 0 ? entries : null;
    }

    /// <summary>
    /// Applies the subscription-time blacklist to the FPS controller. Lib's
    /// FpsSensorController only exposes the blacklist read-only (Blacklist),
    /// so the backing field is written directly; if its shape ever changes the
    /// application is skipped and monitoring keeps the previous behavior.
    /// </summary>
    private static void TryApplyFpsBlacklist(FpsSensorController controller, string[]? blacklist)
    {
        if (blacklist is null)
            return;

        try
        {
            var field = typeof(FpsSensorController).GetField("_blacklist", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field is null || field.FieldType != typeof(List<string>))
                return;

            field.SetValue(controller, new List<string>(blacklist));
        }
        catch (Exception ex)
        {
            if (UniversalDeviceToolkit.Lib.Utils.Log.Instance.IsTraceEnabled)
                UniversalDeviceToolkit.Lib.Utils.Log.Instance.Trace($"Failed to apply FPS blacklist: {ex.Message}", ex);
        }
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

    private static bool TryGetBoolean(BridgeRequest request, string name, out bool value)
    {
        value = false;
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetDouble(BridgeRequest request, string name, out double value)
    {
        value = 0;
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDouble(out value) ||
            !double.IsFinite(value))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetStringArray(BridgeRequest request, string name, out string[] value)
    {
        value = [];
        if (!request.Parameters.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        value = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToArray();
        return true;
    }

    private static float? NullIf(float value) => HasValue(value) ? value : null;
    private static double? NullIfVoltage(double value) => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? null : value;
    private static double? NullIfTemperature(double value) => value <= 0 || double.IsNaN(value) || double.IsInfinity(value) ? null : value;
    private static int? NullIf(int value) => value < 0 ? null : value;
    private static string? NullIf(string value, string sentinel) => value == sentinel ? null : value;
}
