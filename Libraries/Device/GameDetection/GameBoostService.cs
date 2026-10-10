using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace UniversalDeviceToolkit.Lib.GameDetection;

public sealed class GameBoostService : IDisposable
{
    public sealed record BoostStatus(
        bool IsBoosting,
        string? ActiveGameProcess,
        int? ActiveGameProcessId,
        IReadOnlyList<string> BoostedProcesses,
        int SuppressedProcessesCount);

    private readonly GameBoostSettings _settings;
    private readonly GameAutoListener _gameAutoListener;
    private readonly FpsSensorController _fpsSensorController;
    private readonly Lock _lock = new();

    private readonly ConcurrentDictionary<int, ProcessPriorityClass> _originalPriorities = new();
    private readonly ConcurrentDictionary<int, nint> _originalAffinities = new();
    private readonly HashSet<int> _suppressedPids = [];
    private readonly HashSet<int> _boostedPids = [];

    private string? _activeGameName;
    private int? _activeGamePid;
    private bool _isBoosting;
    private bool _disposed;
    private int _optimizationInFlight;

    public event EventHandler<BoostStatus>? StatusChanged;

    public GameBoostService(
        GameBoostSettings settings,
        GameAutoListener gameAutoListener,
        FpsSensorController fpsSensorController)
    {
        _settings = settings;
        _gameAutoListener = gameAutoListener;
        _fpsSensorController = fpsSensorController;
    }

    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_disposed) return;
        }

        await _gameAutoListener.SubscribeChangedAsync(OnGameListenerChanged).ConfigureAwait(false);
        _fpsSensorController.FpsDataUpdated += OnFpsDataUpdated;
    }

    public async Task StopAsync()
    {
        await _gameAutoListener.UnsubscribeChangedAsync(OnGameListenerChanged).ConfigureAwait(false);
        _fpsSensorController.FpsDataUpdated -= OnFpsDataUpdated;

        await RevertOptimizationsAsync().ConfigureAwait(false);
    }

    public BoostStatus GetStatus()
    {
        lock (_lock)
        {
            var boostedNames = new List<string>();
            foreach (var pid in _boostedPids)
            {
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    if (!proc.HasExited)
                        boostedNames.Add(proc.ProcessName);
                }
                catch
                {
                    // Ignore transient process lookup failures
                }
            }

            return new BoostStatus(
                _isBoosting,
                _activeGameName,
                _activeGamePid,
                boostedNames,
                _suppressedPids.Count);
        }
    }

    public async Task<bool> OptimizeForegroundGameAsync()
    {
        try
        {
            var foregroundPid = GetForegroundProcessId();
            if (foregroundPid <= 4)
                return false;

            using var process = Process.GetProcessById((int)foregroundPid);
            if (process.HasExited)
                return false;

            await ApplyOptimizationsAsync(process).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"OptimizeForegroundGameAsync error: {ex.Message}", ex);
            return false;
        }
    }

    public async Task RevertOptimizationsAsync()
    {
        List<int> boostedToRevert;
        List<int> suppressedToRevert;

        lock (_lock)
        {
            if (!_isBoosting) return;

            boostedToRevert = [.. _boostedPids];
            suppressedToRevert = [.. _suppressedPids];

            _isBoosting = false;
            _activeGameName = null;
            _activeGamePid = null;
            _boostedPids.Clear();
            _suppressedPids.Clear();
        }

        // Revert boosted game processes
        foreach (var pid in boostedToRevert)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited) continue;

                if (_originalPriorities.TryRemove(pid, out var origPriority))
                {
                    try { proc.PriorityClass = origPriority; }
                    catch { /* Access denied or process dying */ }
                }

                if (_originalAffinities.TryRemove(pid, out var origAffinity))
                {
                    try { proc.ProcessorAffinity = origAffinity; }
                    catch { /* Access denied or process dying */ }
                }
            }
            catch
            {
                // Process already terminated
            }
        }

        // Revert suppressed background processes
        foreach (var pid in suppressedToRevert)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited) continue;

                ProcessScheduling.TrySetBackgroundEfficiency(pid, false);

                if (_originalPriorities.TryRemove(pid, out var origPriority))
                {
                    try { proc.PriorityClass = origPriority; }
                    catch { /* Access denied */ }
                }

                if (_originalAffinities.TryRemove(pid, out var origAffinity))
                {
                    try { proc.ProcessorAffinity = origAffinity; }
                    catch { /* Access denied */ }
                }
            }
            catch
            {
                // Process already terminated
            }
        }

        _originalPriorities.Clear();
        _originalAffinities.Clear();

        NotifyStatusChanged();
        await Task.CompletedTask;
    }

    private async void OnGameListenerChanged(object? sender, GameAutoListener.ChangedEventArgs e)
    {
        try
        {
            var store = await _settings.LoadStoreAsync().ConfigureAwait(false) ?? new GameBoostSettings.GameBoostSettingsStore();
            if (!store.AutoGameBoost)
                return;

            if (e.Running)
            {
                await OptimizeForegroundGameAsync().ConfigureAwait(false);
            }
            else
            {
                await RevertOptimizationsAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace("Error processing GameAutoListener changed event in GameBoostService", ex);
        }
    }

    private void OnFpsDataUpdated(object? sender, FpsSensorController.FpsData e)
    {
        // When valid FPS data starts streaming (game is in foreground and rendering),
        // trigger boost if not already boosting.
        if (int.TryParse(e.Fps, out var fps) && fps > 0)
        {
            var store = _settings.Store;
            if (store.AutoGameBoost
                && !_isBoosting
                && Interlocked.CompareExchange(ref _optimizationInFlight, 1, 0) == 0)
            {
                OptimizeFromFpsUpdateAsync().Forget("optimize foreground game after FPS update");
            }
        }
    }

    private async Task OptimizeFromFpsUpdateAsync()
    {
        try
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
            }

            await OptimizeForegroundGameAsync().ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _optimizationInFlight, 0);
        }
    }

    private async Task ApplyOptimizationsAsync(Process gameProcess)
    {
        var store = await _settings.LoadStoreAsync().ConfigureAwait(false) ?? new GameBoostSettings.GameBoostSettingsStore();

        lock (_lock)
        {
            _activeGameName = gameProcess.ProcessName;
            _activeGamePid = gameProcess.Id;
            _isBoosting = true;
            _boostedPids.Add(gameProcess.Id);
        }

        // 1. Boost Game Process Priority & Affinity
        try
        {
            if (!_originalPriorities.ContainsKey(gameProcess.Id))
                _originalPriorities[gameProcess.Id] = gameProcess.PriorityClass;

            if (!_originalAffinities.ContainsKey(gameProcess.Id))
                _originalAffinities[gameProcess.Id] = gameProcess.ProcessorAffinity;

            if (store.BoostGamePriority)
            {
                try
                {
                    gameProcess.PriorityClass = ProcessPriorityClass.High;
                }
                catch
                {
                    try { gameProcess.PriorityClass = ProcessPriorityClass.AboveNormal; }
                    catch { /* Fallback ignore */ }
                }
            }

            if (store.OptimizeCpuAffinity)
            {
                var optimalAffinity = CalculateOptimalGameAffinity();
                if (optimalAffinity != 0)
                {
                    try { gameProcess.ProcessorAffinity = optimalAffinity; }
                    catch { /* Access denied or invalid mask */ }
                }
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set game priority/affinity for {gameProcess.ProcessName}: {ex.Message}");
        }

        // 2. Suppress Non-Essential Background Processes
        if (store.SuppressBackgroundProcesses)
        {
            await SuppressBackgroundProcessesAsync(gameProcess.Id, store).ConfigureAwait(false);
        }

        NotifyStatusChanged();
    }

    private async Task SuppressBackgroundProcessesAsync(int gamePid, GameBoostSettings.GameBoostSettingsStore store)
    {
        var currentSessionId = Process.GetCurrentProcess().SessionId;
        var whitelist = new HashSet<string>(store.BackgroundWhitelist, StringComparer.OrdinalIgnoreCase);

        var backgroundAffinities = store.OptimizeCpuAffinity
            ? CalculateOptimalBackgroundAffinity()
            : 0;

        await Task.Run(() =>
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch
            {
                return;
            }

            foreach (var proc in processes)
            {
                using (proc)
                {
                    try
                    {
                        if (proc.Id == gamePid || proc.Id <= 4)
                            continue;

                        if (proc.SessionId != currentSessionId)
                            continue;

                        var name = proc.ProcessName;
                        if (whitelist.Contains(name))
                            continue;

                        if (IsCriticalSystemProcess(name))
                            continue;

                        // Capture original state before mutation
                        if (!_originalPriorities.ContainsKey(proc.Id))
                            _originalPriorities[proc.Id] = proc.PriorityClass;

                        if (backgroundAffinities != 0 && !_originalAffinities.ContainsKey(proc.Id))
                            _originalAffinities[proc.Id] = proc.ProcessorAffinity;

                        // Apply EcoQoS & BelowNormal priority
                        ProcessScheduling.TrySetBackgroundEfficiency(proc.Id, true);

                        try
                        {
                            proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                        }
                        catch
                        {
                            // Ignore access denied on protected/system services
                        }

                        if (backgroundAffinities != 0)
                        {
                            try
                            {
                                proc.ProcessorAffinity = backgroundAffinities;
                            }
                            catch
                            {
                                // Ignore
                            }
                        }

                        lock (_lock)
                        {
                            _suppressedPids.Add(proc.Id);
                        }
                    }
                    catch
                    {
                        // Ignore exited or inaccessible process
                    }
                }
            }
        }).ConfigureAwait(false);
    }

    private static bool IsCriticalSystemProcess(string processName)
    {
        return processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("dwmp", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("dwm", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("csrss", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("lsass", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("services", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("smss", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("wininit", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("winlogon", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("fontdrvhost", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("sihost", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("ctfmon", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
               processName.Equals("Taskmgr", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Calculates optimal CPU Affinity for games on Hybrid/High Core Count CPUs.
    /// E.g. On Intel 12th+ Gen (e.g. 6P+8E = 20 threads), P-cores occupy threads 0..11.
    /// </summary>
    internal static nint CalculateOptimalGameAffinity()
    {
        var coreCount = Environment.ProcessorCount;
        if (coreCount <= 8)
            return 0; // <= 8 threads: use all cores

        // If core count > 12 (likely hybrid Intel 12th-14th gen or dual-CCD AMD),
        // prioritize primary performance cores (lower 12 threads) while retaining headroom.
        if (coreCount >= 16)
        {
            var mask = (1L << Math.Min(16, coreCount)) - 1L;
            return (nint)mask;
        }

        return 0;
    }

    /// <summary>
    /// Calculates affinity mask for throttled background processes away from P-core 0/1.
    /// </summary>
    internal static nint CalculateOptimalBackgroundAffinity()
    {
        var coreCount = Environment.ProcessorCount;
        if (coreCount < 8)
            return 0;

        // Shift background processes off core 0/1 to avoid frame-time spikes on main render thread
        var allMask = (1L << Math.Min(62, coreCount)) - 1L;
        var backgroundMask = allMask & ~0x3L; // clear core 0 & 1
        return (nint)backgroundMask;
    }

    private static uint GetForegroundProcessId()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull) return 0;

        _ = PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    private void NotifyStatusChanged()
    {
        StatusChanged?.Invoke(this, GetStatus());
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        try
        {
            var stopTask = StopAsync();
            if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                Log.Instance.Warning("Timed out while stopping Game Boost during disposal.");
        }
        catch (Exception ex)
        {
            Log.Instance.Error("Failed to stop Game Boost during disposal.", ex);
        }
    }
}
