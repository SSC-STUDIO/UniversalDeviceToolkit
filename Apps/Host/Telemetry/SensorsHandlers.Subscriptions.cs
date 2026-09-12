using System.Text.Json;
using UniversalDeviceToolkit.Host.Rpc;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

public static partial class SensorsHandlers
{
    private static readonly object SubscribeLock = new();
    private static readonly HashSet<string> VendorSubscriberIds = new(StringComparer.Ordinal);
    private static readonly HashSet<string> LhmSubscriberIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, double> VendorIntervals = new(StringComparer.Ordinal);
    private static SensorsGroupController? _subscribedGroup;
    private static BridgeRpcServer? _sensorsRpc;
    private static System.Threading.Timer? _vendorPollTimer;
    private static CancellationTokenSource? _vendorPollCts;
    private static double _vendorPollIntervalSec = 1.0;
    private static double _lhmIntervalSec = 1.0;
    private static int _snapshotPublishInFlight;
    private static bool _uiActivityHooked;

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

    private static void PauseSensorProductionLocked()
    {
        if (_subscribedGroup is not null)
        {
            foreach (var id in LhmSubscriberIds)
                _subscribedGroup.Stop(id);
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
                group.Start(id, interval);
        }

        if (VendorSubscriberIds.Count > 0 && _sensorsRpc is not null)
            StartOrUpdateVendorTimer(_sensorsRpc);
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
            var subscriber = subscriberId;

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
            var subscriber = subscriberId;
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
}
