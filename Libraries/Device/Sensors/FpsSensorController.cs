using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using PresentMonFps;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Controllers.Sensors
{
    public class FpsSensorController : IDisposable
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public class FpsData
        {
            public string Fps { get; set; } = "-1";
            public string LowFps { get; set; } = "-1";
            public string FrameTime { get; set; } = "-1";
            public override string ToString() => $"FPS: {Fps}, Low: {LowFps}, Time: {FrameTime}ms";
        }

        private List<string> _blacklist = new List<string>();

        /// <summary>
        /// Gets the read-only list of blacklisted process names (by process name, case-insensitive).
        /// Thread-safe: the list is atomically replaced during initialization and never mutated in-place.
        /// </summary>
        public IReadOnlyList<string> Blacklist => _blacklist;

        private FpsData _currentFpsData = new FpsData();
        private CancellationTokenSource? _cancellationTokenSource;
        private Process? _currentMonitoredProcess;
        private readonly Lock _lockObject = new Lock();
        private volatile bool _isRunning = false;
        private bool _disposed;
        private bool _monitoringLoopErrorLogged;
        private CancellationTokenSource? _currentProcessTokenSource;
        private readonly IDelayProvider _delayProvider;

        public event EventHandler<FpsData>? FpsDataUpdated;

        public FpsSensorController(IDelayProvider delayProvider)
        {
            _delayProvider = delayProvider;
        }

        public Task StartMonitoringAsync()
        {
            CancellationToken token;
            lock (_lockObject)
            {
                if (_disposed || _isRunning)
                    return Task.CompletedTask;

                _isRunning = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                token = _cancellationTokenSource.Token;
            }

            _ = Task.Run(() => MonitorForegroundProcessAsync(token), token);
            return Task.CompletedTask;
        }

        public void StopMonitoring()
        {
            CancellationTokenSource? cts;
            lock (_lockObject)
            {
                _isRunning = false;
                cts = _cancellationTokenSource;
                _cancellationTokenSource = null;
                CancelProcessMonitoring();
                _currentMonitoredProcess = null;
                _currentFpsData = new FpsData();
            }

            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                cts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            FpsDataUpdated?.Invoke(this, GetCurrentFpsData());
        }

        private async Task MonitorForegroundProcessAsync(CancellationToken token)
        {
            Process? lastProcess = null;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var currentProcess = GetForegroundProcess();

                    if (currentProcess != null && currentProcess.Id != lastProcess?.Id)
                    {
                        StopProcessMonitoring();

                        if (!currentProcess.HasExited)
                        {
                            await StartProcessMonitoringAsync(currentProcess).ConfigureAwait(false);
                        }
                        lastProcess?.Dispose();
                        lastProcess = currentProcess.HasExited ? null : currentProcess;
                    }
                    else if (_currentMonitoredProcess != null
                             && (currentProcess == null
                                 || (currentProcess.Id == _currentMonitoredProcess.Id && _currentMonitoredProcess.HasExited)))
                    {
                        StopProcessMonitoring();
                        lastProcess?.Dispose();
                        lastProcess = null;
                    }
                    else
                    {
                        currentProcess?.Dispose();
                    }

                    _monitoringLoopErrorLogged = false;
                    await _delayProvider.Delay(TimeSpan.FromMilliseconds(1000), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_monitoringLoopErrorLogged)
                    {
                        Log.Instance.Warning($"Monitoring loop error: {ex.Message}");
                        _monitoringLoopErrorLogged = true;
                    }

                    try
                    {
                        await _delayProvider.Delay(TimeSpan.FromMilliseconds(1000), token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            lastProcess?.Dispose();
        }

        public FpsData GetCurrentFpsData()
        {
            lock (_lockObject)
            {
                return new FpsData
                {
                    Fps = _currentFpsData.Fps,
                    LowFps = _currentFpsData.LowFps,
                    FrameTime = _currentFpsData.FrameTime
                };
            }
        }

        private Process? GetForegroundProcess()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return null;

                GetWindowThreadProcessId(hwnd, out var processId);
                switch (processId)
                {
                    case 0:
                    case 4:
                        return null;
                }

                var process = Process.GetProcessById((int)processId);

                if (string.IsNullOrEmpty(process.ProcessName) || process.HasExited)
                {
                    process.Dispose();
                    return null;
                }

                if (IsProcessBlacklisted(process.ProcessName))
                {
                    process.Dispose();
                    return null;
                }

                return process;
            }
            catch (ArgumentException ex)
            {
                Log.Instance.TraceOnce("fps-process-arg", "FPS process attach skipped (process gone).", ex);
                return null;
            }
            catch (InvalidOperationException ex)
            {
                Log.Instance.TraceOnce("fps-process-invalid", "FPS process attach skipped (invalid process state).", ex);
                return null;
            }
            catch (Win32Exception ex)
            {
                Log.Instance.TraceOnce("fps-process-win32", "FPS process attach skipped (Win32 access).", ex);
                return null;
            }
        }

        private Task StartProcessMonitoringAsync(Process process)
        {
            try
            {
                CancellationTokenSource? oldProcessCts;
                CancellationToken processToken;
                CancellationToken monitorToken;
                lock (_lockObject)
                {
                    oldProcessCts = _currentProcessTokenSource;
                    _currentProcessTokenSource = new CancellationTokenSource();
                    _currentMonitoredProcess = process;
                    processToken = _currentProcessTokenSource.Token;
                    monitorToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
                }

                try
                {
                    oldProcessCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    oldProcessCts?.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }

                var request = new FpsRequest((uint)process.Id);
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(processToken, monitorToken);

                var monitoringTask = Task.Run(async () =>
                {
                    try
                    {
                        await FpsInspector.StartForeverAsync(request, OnFpsDataReceived, linkedTokenSource.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        linkedTokenSource.Dispose();
                    }
                }, linkedTokenSource.Token);

                monitoringTask.ContinueWith(t =>
                {
                    if (t.IsCanceled)
                    {
                        return;
                    }

                    if (!t.IsFaulted)
                    {
                        return;
                    }

                    var ex = t.Exception?.Flatten().InnerException ?? t.Exception;

                    Log.Instance.Trace($"Monitoring failed for {process.ProcessName}", ex!);

                    lock (_lockObject)
                    {
                        if (_currentMonitoredProcess?.Id == process.Id)
                        {
                            _currentMonitoredProcess = null;
                        }
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Failed to start monitoring for {process.ProcessName}", ex);

                lock (_lockObject)
                {
                    _currentMonitoredProcess = null;
                }
            }

            return Task.CompletedTask;
        }

        private void StopProcessMonitoring()
        {
            try
            {
                lock (_lockObject)
                {
                    CancelProcessMonitoring();
                    if (_currentMonitoredProcess != null)
                    {
                        _currentMonitoredProcess = null;
                        _currentFpsData = new FpsData();
                    }
                }

                FpsDataUpdated?.Invoke(this, GetCurrentFpsData());
            }
            catch (Exception ex)
            {
                Log.Instance.Trace($"Error stopping process monitoring", ex);
            }
        }

        private void CancelProcessMonitoring()
        {
            var processCts = _currentProcessTokenSource;
            _currentProcessTokenSource = null;
            try
            {
                processCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                processCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void OnFpsDataReceived(FpsResult result)
        {
            var fpsData = new FpsData
            {
                Fps = $"{result.Fps:0}",
                LowFps = $"{result.OnePercentLowFps:0}",
                FrameTime = $"{result.FrameTime:0.0}"
            };

            lock (_lockObject)
            {
                _currentFpsData = fpsData;
            }

            FpsDataUpdated?.Invoke(this, fpsData);
        }

        private bool IsProcessBlacklisted(string processName)
        {
            return _blacklist.Any(x => string.Equals(processName, x, StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            StopMonitoring();
        }
    }
}
