using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Utils;
using UniversalDeviceToolkit.Lib.Controllers.GodMode;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace UniversalDeviceToolkit.Lib.Automation;

public class AutomationProcessor(
    AutomationSettings settings,
    DisplayConfigurationListener displayConfigurationListener,
    NativeWindowsMessageListener nativeWindowsMessageListener,
    PowerStateListener powerStateListener,
    PowerModeListener powerModeListener,
    GodModeController godModeController,
    GameAutoListener gameAutoListener,
    ProcessAutoListener processAutoListener,
    SessionLockUnlockListener sessionLockUnlockListener,
    TimeAutoListener timeAutoListener,
    UserInactivityAutoListener userInactivityAutoListener,
    WiFiAutoListener wifiAutoListener) : IDisposable
{
    private readonly AsyncLock _ioLock = new();
    private readonly AsyncLock _runLock = new();
    private readonly object _ctsLock = new();

    private List<AutomationPipeline> _pipelines = [];
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public bool IsEnabled => settings.Store.IsEnabled;

    public bool HasHardwareSensorTriggers()
    {
        if (!IsEnabled)
            return false;

        foreach (var pipeline in _pipelines)
        {
            foreach (var trigger in pipeline.AllTriggers)
            {
                if (trigger is HardwareSensorAutomationPipelineTrigger)
                    return true;
            }
        }

        return false;
    }

    public event EventHandler<List<AutomationPipeline>>? PipelinesChanged;

    #region Initialization / pipeline reloading

    public async Task InitializeAsync()
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            displayConfigurationListener.Changed += DisplayConfigurationListener_Changed;
            nativeWindowsMessageListener.Changed += NativeWindowsMessageListener_Changed;
            powerStateListener.Changed += PowerStateListener_Changed;
            powerModeListener.Changed += PowerModeListener_Changed;
            godModeController.PresetChanged += GodModeController_PresetChanged;
            sessionLockUnlockListener.Changed += SessionLockUnlockListener_Changed;

            _pipelines = [.. settings.Store.Pipelines];

            RaisePipelinesChanged();

            await UpdateListenersAsync().ConfigureAwait(false);
        }
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            settings.Store.IsEnabled = enabled;
            settings.SynchronizeStore();

            await UpdateListenersAsync().ConfigureAwait(false);
        }
    }

    public async Task ReloadPipelinesAsync(List<AutomationPipeline> pipelines)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Pipelines reload pending...");

        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipelines reloading...");

            _pipelines = pipelines.Select(p => p.DeepCopy()).ToList();

            settings.Store.Pipelines = pipelines;
            settings.SynchronizeStore();

            RaisePipelinesChanged();

            await UpdateListenersAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipelines reloaded.");
        }
    }

    public async Task<List<AutomationPipeline>> GetPipelinesAsync()
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
            return _pipelines.Select(p => p.DeepCopy()).ToList();
    }

    #endregion

    #region Run

    public void RunOnStartup()
    {
        if (!IsEnabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not enabled. Pipeline run on startup ignored.");

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Pipeline run on startup pending...");

        _ = RunOnStartupAsync();
    }

    private async Task RunOnStartupAsync()
    {
        try
        {
            await ProcessEvent(new StartupAutomationEvent()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error processing startup automation event: {ex.Message}", ex);
        }
    }

    public async Task RunNowAsync(AutomationPipeline pipeline)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Pipeline run now pending...");

        using (await _runLock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipeline run starting...");

            try
            {
                List<AutomationPipeline> pipelines;
                using (await _ioLock.LockAsync().ConfigureAwait(false))
                    pipelines = _pipelines.ToList();

                var otherPipelines = pipelines.Where(p => p.Id != pipeline.Id).ToList();
                await pipeline.DeepCopy().RunAsync(otherPipelines).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Pipeline run finished successfully.");
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Pipeline run failed.", ex);

                throw;
            }
        }
    }

    public async Task RunNowAsync(Guid pipelineId)
    {
        AutomationPipeline? pipeline;
        using (await _ioLock.LockAsync().ConfigureAwait(false))
            pipeline = _pipelines.FirstOrDefault(p => p.Trigger is null && p.Id == pipelineId);

        if (pipeline is null)
            return;

        await RunNowAsync(pipeline).ConfigureAwait(false);
    }

    private async Task RunAsync(IAutomationEvent automationEvent)
    {
        if (_disposed)
            return;

        // Cancel the previous run before waiting for the lock so in-flight delays/steps
        // can observe cancellation instead of only being replaced after they finish.
        CancellationTokenSource? oldCts;
        lock (_ctsLock)
        {
            if (_disposed)
                return;
            oldCts = _cts;
            _cts = new CancellationTokenSource();
        }

        if (oldCts is not null)
        {
            try
            {
                await oldCts.CancelAsync().ConfigureAwait(false);
                oldCts.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("CTS already disposed during cancellation", ex);
            }
        }

        using (await _runLock.LockAsync().ConfigureAwait(false))
        {
            if (_disposed || !IsEnabled)
                return;

            List<AutomationPipeline> pipelines;
            using (await _ioLock.LockAsync().ConfigureAwait(false))
                pipelines = _pipelines.ToList();

            CancellationToken ct;
            lock (_ctsLock)
            {
                ct = _cts!.Token;
            }

            foreach (var pipeline in pipelines)
            {
                if (ct.IsCancellationRequested)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Run interrupted.");
                    break;
                }

                try
                {
                    if (pipeline.Trigger is null || !await pipeline.Trigger.IsMatchingEvent(automationEvent).ConfigureAwait(false))
                        continue;

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Running pipeline... [name={pipeline.Name}, trigger={pipeline.Trigger}, steps.Count={pipeline.Steps.Count}]");

                    var otherPipelines = pipelines.Where(p => p.Id != pipeline.Id).ToList();
                    await pipeline.RunAsync(otherPipelines, ct).ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Pipeline completed successfully. [name={pipeline.Name}, trigger={pipeline.Trigger}]");
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Pipeline run failed. [name={pipeline.Name}, trigger={pipeline.Trigger}]", ex);
                }

                if (pipeline.IsExclusive)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Pipeline is exclusive. Breaking. [name={pipeline.Name}, trigger={pipeline.Trigger}, steps.Count={pipeline.Steps.Count}]");
                    break;
                }
            }
        }
    }

    #endregion

    #region Listeners

    private async Task DisplayConfigurationListener_ChangedAsync(object? sender, DisplayConfigurationListener.ChangedEventArgs args)
    {
        try
        {
            var e = new HDRAutomationEvent(args.HDR);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in DisplayConfigurationListener_Changed: {ex.Message}", ex);
        }
    }

    private void DisplayConfigurationListener_Changed(object? sender, DisplayConfigurationListener.ChangedEventArgs args)
    {
        _ = DisplayConfigurationListener_ChangedAsync(sender, args);
    }

    private async Task NativeWindowsMessageListener_ChangedAsync(object? sender, NativeWindowsMessageListener.ChangedEventArgs args)
    {
        try
        {
            var e = new NativeWindowsMessageEvent(args.Message, args.Data);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in NativeWindowsMessageListener_Changed: {ex.Message}", ex);
        }
    }

    private void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs args)
    {
        _ = NativeWindowsMessageListener_ChangedAsync(sender, args);
    }

    private async Task PowerStateListener_ChangedAsync(object? sender, PowerStateListener.ChangedEventArgs args)
    {
        try
        {
            var e = new PowerStateAutomationEvent(args.PowerStateEvent, args.PowerAdapterStateChanged);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in PowerStateListener_Changed: {ex.Message}", ex);
        }
    }

    private void PowerStateListener_Changed(object? sender, PowerStateListener.ChangedEventArgs args)
    {
        _ = PowerStateListener_ChangedAsync(sender, args);
    }

    private async Task PowerModeListener_ChangedAsync(object? sender, PowerModeListener.ChangedEventArgs args)
    {
        try
        {
            var e = new PowerModeAutomationEvent(args.State);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in PowerModeListener_Changed: {ex.Message}", ex);
        }
    }

    private void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs args)
    {
        _ = PowerModeListener_ChangedAsync(sender, args);
    }

    private async Task GodModeController_PresetChangedAsync(object? sender, Guid presetId)
    {
        try
        {
            var e = new CustomModePresetAutomationEvent(presetId);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in GodModeController_PresetChanged: {ex.Message}", ex);
        }
    }

    private void GodModeController_PresetChanged(object? sender, Guid presetId)
    {
        _ = GodModeController_PresetChangedAsync(sender, presetId);
    }

    private async Task GameAutoListener_ChangedAsync(object? sender, GameAutoListener.ChangedEventArgs args)
    {
        try
        {
            var e = new GameAutomationEvent(args.Running);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in GameAutoListener_Changed: {ex.Message}", ex);
        }
    }

    private void GameAutoListener_Changed(object? sender, GameAutoListener.ChangedEventArgs args)
    {
        _ = GameAutoListener_ChangedAsync(sender, args);
    }

    private async Task ProcessAutoListener_ChangedAsync(object? sender, ProcessAutoListener.ChangedEventArgs args)
    {
        try
        {
            var e = new ProcessAutomationEvent(args.Type, args.ProcessInfo);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in ProcessAutoListener_Changed: {ex.Message}", ex);
        }
    }

    private void ProcessAutoListener_Changed(object? sender, ProcessAutoListener.ChangedEventArgs args)
    {
        _ = ProcessAutoListener_ChangedAsync(sender, args);
    }

    private async Task SessionLockUnlockListener_ChangedAsync(object? sender, SessionLockUnlockListener.ChangedEventArgs args)
    {
        try
        {
            var e = new SessionLockUnlockAutomationEvent(args.Locked);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in SessionLockUnlockListener_Changed: {ex.Message}", ex);
        }
    }

    private void SessionLockUnlockListener_Changed(object? sender, SessionLockUnlockListener.ChangedEventArgs args)
    {
        _ = SessionLockUnlockListener_ChangedAsync(sender, args);
    }

    private async Task TimeAutoListener_ChangedAsync(object? sender, TimeAutoListener.ChangedEventArgs args)
    {
        try
        {
            var e = new TimeAutomationEvent(args.Time, args.Day);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in TimeAutoListener_Changed: {ex.Message}", ex);
        }
    }

    private void TimeAutoListener_Changed(object? sender, TimeAutoListener.ChangedEventArgs args)
    {
        _ = TimeAutoListener_ChangedAsync(sender, args);
    }

    private async Task UserInactivityAutoListener_ChangedAsync(object? sender, UserInactivityAutoListener.ChangedEventArgs args)
    {
        try
        {
            var e = new UserInactivityAutomationEvent(args.TimerResolution * args.TickCount);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in UserInactivityAutoListener_Changed: {ex.Message}", ex);
        }
    }

    private void UserInactivityAutoListener_Changed(object? sender, UserInactivityAutoListener.ChangedEventArgs args)
    {
        _ = UserInactivityAutoListener_ChangedAsync(sender, args);
    }

    private async Task WiFiAutoListener_ChangedAsync(object? sender, WiFiAutoListener.ChangedEventArgs args)
    {
        try
        {
            var e = new WiFiAutomationEvent(args.IsConnected, args.Ssid);
            await ProcessEvent(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Error($"Error in WiFiAutoListener_Changed: {ex.Message}", ex);
        }
    }

    private void WiFiAutoListener_Changed(object? sender, WiFiAutoListener.ChangedEventArgs args)
    {
        _ = WiFiAutoListener_ChangedAsync(sender, args);
    }

    #endregion

    #region Event processing

    private async Task ProcessEvent(IAutomationEvent e)
    {
        if (_disposed)
            return;

        // Do not pre-evaluate triggers here. Stateful triggers (battery %, sensors) arm
        // cooldown/_lastMatchedAt inside IsMatchingEvent; a separate pre-check would
        // consume the match so RunAsync always fails cooldown (pipelines never run).
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Processing event {e}... [type={e.GetType().Name}]");

        await RunAsync(e).ConfigureAwait(false);
    }

    #endregion

    #region Helper methods

    private async Task UpdateListenersAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping listeners...");

        await gameAutoListener.UnsubscribeChangedAsync(GameAutoListener_Changed).ConfigureAwait(false);
        await processAutoListener.UnsubscribeChangedAsync(ProcessAutoListener_Changed).ConfigureAwait(false);
        await timeAutoListener.UnsubscribeChangedAsync(TimeAutoListener_Changed).ConfigureAwait(false);
        await userInactivityAutoListener.UnsubscribeChangedAsync(UserInactivityAutoListener_Changed).ConfigureAwait(false);
        await wifiAutoListener.UnsubscribeChangedAsync(WiFiAutoListener_Changed).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped listeners...");

        if (!IsEnabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not enabled. Will not start listeners.");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting listeners...");

        var triggers = _pipelines.SelectMany(p => p.AllTriggers).ToArray();

        if (triggers.OfType<IGameAutomationPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting game listener...");

            await gameAutoListener.SubscribeChangedAsync(GameAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<IProcessesAutomationPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting process listener...");

            await processAutoListener.SubscribeChangedAsync(ProcessAutoListener_Changed).ConfigureAwait(false);
        }

        // Battery % / hardware-sensor triggers only re-evaluate when some listener fires.
        // Poll via the time listener so Duration/cooldown edge detection can work.
        var needsTimePolling =
            triggers.OfType<ITimeAutomationPipelineTrigger>().Any() ||
            triggers.OfType<IPeriodicAutomationPipelineTrigger>().Any() ||
            triggers.OfType<BatteryPercentageAutomationPipelineTrigger>().Any() ||
            triggers.OfType<HardwareSensorAutomationPipelineTrigger>().Any();

        if (needsTimePolling)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting time listener...");

            await timeAutoListener.SubscribeChangedAsync(TimeAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<IUserInactivityPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting user inactivity listener...");

            await userInactivityAutoListener.SubscribeChangedAsync(UserInactivityAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<IWiFiConnectedPipelineTrigger>().Any() || triggers.OfType<WiFiDisconnectedAutomationPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting WiFi listener...");

            await wifiAutoListener.SubscribeChangedAsync(WiFiAutoListener_Changed).ConfigureAwait(false);
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started relevant listeners.");
    }

    private void RaisePipelinesChanged()
    {
        PipelinesChanged?.Invoke(this, _pipelines.Select(p => p.DeepCopy()).ToList());
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        // Block new ProcessEvent/RunAsync before tearing down listeners.
        _disposed = true;

        if (disposing)
        {
            try
            {
                displayConfigurationListener.Changed -= DisplayConfigurationListener_Changed;
                nativeWindowsMessageListener.Changed -= NativeWindowsMessageListener_Changed;
                powerStateListener.Changed -= PowerStateListener_Changed;
                powerModeListener.Changed -= PowerModeListener_Changed;
                godModeController.PresetChanged -= GodModeController_PresetChanged;
                sessionLockUnlockListener.Changed -= SessionLockUnlockListener_Changed;

                // AutoListeners were only unsubscribed in UpdateListenersAsync; dispose must
                // also detach them so late ticks cannot allocate a new CTS after dispose.
                try
                {
                    gameAutoListener.UnsubscribeChangedAsync(GameAutoListener_Changed)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    processAutoListener.UnsubscribeChangedAsync(ProcessAutoListener_Changed)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    timeAutoListener.UnsubscribeChangedAsync(TimeAutoListener_Changed)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    userInactivityAutoListener.UnsubscribeChangedAsync(UserInactivityAutoListener_Changed)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    wifiAutoListener.UnsubscribeChangedAsync(WiFiAutoListener_Changed)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace("Error unsubscribing auto listeners during disposal", ex);
                }

                CancellationTokenSource? ctsToDispose;
                lock (_ctsLock)
                {
                    ctsToDispose = _cts;
                    _cts = null;
                }

                if (ctsToDispose is not null)
                {
                    try
                    {
                        ctsToDispose.Cancel();
                    }
                    catch (ObjectDisposedException ex)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace("CTS already disposed during disposal", ex);
                    }
                    ctsToDispose.Dispose();
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error during AutomationProcessor disposal", ex);
            }
        }
    }

    #endregion
}
