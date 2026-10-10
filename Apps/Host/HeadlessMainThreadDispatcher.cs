using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Host;

/// <summary>
/// Headless implementation of <see cref="IMainThreadDispatcher"/> for the
/// bridge host. On Windows this is a dedicated STA thread that pumps Win32
/// messages so WH_* hooks installed via <see cref="Dispatch"/> keep receiving
/// callbacks (the thread pool never pumps). On other platforms work runs on
/// the thread pool.
/// </summary>
public sealed class HeadlessMainThreadDispatcher : IMainThreadDispatcher, IDisposable
{
    private int _disposed;

#if WINDOWS
    private readonly DispatcherMessagePump? _pump;
    internal bool IsPumpThreadAlive => _pump?.IsThreadAlive == true;
#endif

    public HeadlessMainThreadDispatcher()
    {
#if WINDOWS
        _pump = DispatcherMessagePump.TryStart();
#endif
    }

    public void Dispatch(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

#if WINDOWS
        if (_pump is not null)
        {
            _pump.Post(callback);
            return;
        }
#endif

        _ = Task.Run(() => RunLogged(callback));
    }

    public Task DispatchAsync(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

#if WINDOWS
        if (_pump is not null)
            return _pump.PostAsync(callback);
#endif

        return Task.Run(() => RunLoggedAsync(callback));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

#if WINDOWS
        _pump?.Dispose();
#endif
    }

    private static void RunLogged(Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Headless dispatcher callback failed: {ex.Message}", ex);
        }
    }

    private static async Task RunLoggedAsync(Func<Task> callback)
    {
        try
        {
            await callback().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Instance.Warning($"Headless dispatcher async callback failed: {ex.Message}", ex);
            throw;
        }
    }

#if WINDOWS
    /// <summary>
    /// STA thread with a GetMessage loop. Posted work is delivered with
    /// <c>PostThreadMessage</c>; low-level hooks installed on this thread
    /// receive callbacks while the loop runs.
    /// </summary>
    private sealed class DispatcherMessagePump : IDisposable
    {
        private const uint WM_QUIT = 0x0012;
        private const uint WM_APP = 0x8000;
        private const uint WM_DISPATCH = WM_APP + 1;
        private const uint PM_NOREMOVE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMsg
        {
            public IntPtr Hwnd;
            public uint Message;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int PtX;
            public int PtY;
        }

        [DllImport("user32.dll", EntryPoint = "GetMessageW")]
        private static extern int GetMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", EntryPoint = "PeekMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage(ref NativeMsg lpMsg);

        [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
        private static extern IntPtr DispatchMessage(ref NativeMsg lpMsg);

        [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private readonly ConcurrentQueue<WorkItem> _work = new();
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _lifecycleLock = new();
        private readonly HashSet<AsyncCompletion> _pendingAsync = [];
        private readonly Thread _thread;
        private uint _threadId;
        private volatile bool _failedToStart;
        private bool _stopping;

        public bool IsThreadAlive => _thread.IsAlive;

        private DispatcherMessagePump()
        {
            _thread = new Thread(Pump)
            {
                IsBackground = true,
                Name = "HostDispatcher",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public static DispatcherMessagePump? TryStart()
        {
            try
            {
                var pump = new DispatcherMessagePump();
                if (!pump._ready.Task.Wait(TimeSpan.FromSeconds(5)) || pump._failedToStart || pump._threadId == 0)
                {
                    pump.Dispose();
                    Log.Instance.Warning("Headless dispatcher message pump failed to start; falling back to the thread pool.");
                    return null;
                }

                return pump;
            }
            catch (Exception ex)
            {
                Log.Instance.Warning($"Headless dispatcher message pump could not be created: {ex.Message}", ex);
                return null;
            }
        }

        public void Post(Action callback)
        {
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(_stopping, this);
                if (!IsOnPumpThread)
                    _work.Enqueue(WorkItem.Sync(callback));
            }

            if (IsOnPumpThread)
            {
                RunLogged(callback);
                return;
            }

            WakePumpOrFallback();
        }

        public Task PostAsync(Func<Task> callback)
        {
            var completion = new AsyncCompletion(this);
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(_stopping, this);
                _pendingAsync.Add(completion);
                if (!IsOnPumpThread)
                    _work.Enqueue(WorkItem.Async(callback, completion));
            }

            if (IsOnPumpThread)
                WorkItem.Async(callback, completion).Execute();
            else
                WakePumpOrFallback();
            return completion.Task;
        }

        private bool IsOnPumpThread => Thread.CurrentThread == _thread;

        private void WakePumpOrFallback()
        {
            if (PostThreadMessage(_threadId, WM_DISPATCH, IntPtr.Zero, IntPtr.Zero))
                return;

            Log.Instance.Warning("Headless dispatcher PostThreadMessage failed; running queued work on the thread pool.");
            _ = Task.Run(DrainWork);
        }

        private void RequestStop()
        {
            var threadId = _threadId;
            if (threadId != 0 && _thread.IsAlive
                && !PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero))
                Log.Instance.Warning("Headless dispatcher could not post its shutdown message.");
        }

        public void Dispose()
        {
            StopOutstandingWork();
            RequestStop();
            if (!IsOnPumpThread && !_thread.Join(TimeSpan.FromSeconds(5)))
                Log.Instance.Warning("Headless dispatcher pump did not stop within the timeout.");
        }

        private void StopOutstandingWork()
        {
            AsyncCompletion[] pending;
            lock (_lifecycleLock)
            {
                _stopping = true;
                _work.Clear();
                pending = [.. _pendingAsync];
                _pendingAsync.Clear();
            }

            foreach (var completion in pending)
                completion.Fail(new ObjectDisposedException(nameof(HeadlessMainThreadDispatcher)));
        }

        private void Forget(AsyncCompletion completion)
        {
            lock (_lifecycleLock)
                _pendingAsync.Remove(completion);
        }

        private void Pump()
        {
            try
            {
                _threadId = GetCurrentThreadId();
                // Create the thread message queue before callers PostThreadMessage.
                _ = PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);
            }
            catch (Exception ex)
            {
                _failedToStart = true;
                Log.Instance.Warning($"Headless dispatcher pump thread failed to initialize: {ex.Message}", ex);
                _ready.TrySetResult();
                StopOutstandingWork();
                return;
            }

            _ready.TrySetResult();

            try
            {
                lock (_lifecycleLock)
                {
                    if (_stopping)
                        return;
                }

                int result;
                while ((result = GetMessage(out var msg, IntPtr.Zero, 0, 0)) > 0)
                {
                    if (msg.Message == WM_DISPATCH)
                    {
                        DrainWork();
                        continue;
                    }

                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                if (result < 0)
                    Log.Instance.Warning("Headless dispatcher GetMessage failed; stopping the pump.");
            }
            catch (Exception ex)
            {
                Log.Instance.Warning($"Headless dispatcher pump failed: {ex.Message}", ex);
            }
            finally
            {
                StopOutstandingWork();
            }
        }

        private void DrainWork()
        {
            while (true)
            {
                WorkItem item;
                lock (_lifecycleLock)
                {
                    if (_stopping || !_work.TryDequeue(out item))
                        return;
                }
                item.Execute();
            }
        }

        private readonly struct WorkItem
        {
            private readonly Action? _sync;
            private readonly Func<Task>? _async;
            private readonly AsyncCompletion? _completion;

            private WorkItem(Action? sync, Func<Task>? async, AsyncCompletion? completion)
            {
                _sync = sync;
                _async = async;
                _completion = completion;
            }

            public static WorkItem Sync(Action callback) => new(callback, null, null);

            public static WorkItem Async(Func<Task> callback, AsyncCompletion completion) => new(null, callback, completion);

            public void Execute()
            {
                if (_sync is not null)
                {
                    RunLogged(_sync);
                    return;
                }

                if (_async is null || _completion is null)
                    return;

                try
                {
                    var task = _async();
                    if (task.IsCompleted)
                    {
                        _completion.Complete(task);
                        return;
                    }

                    _ = task.ContinueWith(
                        static (completed, state) =>
                        {
                            if (state is AsyncCompletion completion)
                                completion.Complete(completed);
                        },
                        _completion,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    if (!_completion.Fail(ex))
                        Log.Instance.Warning($"Headless dispatcher async callback failed: {ex.Message}", ex);
                }
            }
        }

        private sealed class AsyncCompletion(DispatcherMessagePump owner)
        {
            private readonly TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private DispatcherMessagePump? _owner = owner;

            public Task Task => _source.Task;

            public bool Fail(Exception exception)
            {
                var completed = _source.TrySetException(exception);
                Interlocked.Exchange(ref _owner, null)?.Forget(this);
                return completed;
            }

            public void Complete(Task task)
            {
                if (task.IsFaulted)
                {
                    var exception = task.Exception;
                    if (exception is not null)
                        _source.TrySetException(exception.InnerExceptions);
                    else
                        _source.TrySetException(new InvalidOperationException("Dispatcher async callback faulted without an exception."));
                }
                else if (task.IsCanceled)
                {
                    _source.TrySetCanceled();
                }
                else
                {
                    _source.TrySetResult();
                }
                Interlocked.Exchange(ref _owner, null)?.Forget(this);
            }
        }
    }
#endif
}
