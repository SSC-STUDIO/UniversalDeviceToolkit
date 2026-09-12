using System;
using System.Collections.Concurrent;
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
public sealed class HeadlessMainThreadDispatcher : IMainThreadDispatcher
{
#if WINDOWS
    private readonly DispatcherMessagePump? _pump;
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

#if WINDOWS
        if (_pump is not null)
            return _pump.PostAsync(callback);
#endif

        return Task.Run(() => RunLoggedAsync(callback));
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
    private sealed class DispatcherMessagePump
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
        private readonly ManualResetEventSlim _ready = new(false);
        private readonly Thread _thread;
        private uint _threadId;
        private volatile bool _failedToStart;

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
                if (!pump._ready.Wait(TimeSpan.FromSeconds(5)) || pump._failedToStart || pump._threadId == 0)
                {
                    pump.RequestStop();
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
            if (IsOnPumpThread)
            {
                RunLogged(callback);
                return;
            }

            _work.Enqueue(WorkItem.Sync(callback));
            WakePumpOrFallback();
        }

        public Task PostAsync(Func<Task> callback)
        {
            if (IsOnPumpThread)
                return RunLoggedAsync(callback);

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Enqueue(WorkItem.Async(callback, tcs));
            WakePumpOrFallback();
            return tcs.Task;
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
            if (threadId != 0)
                PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
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
                _ready.Set();
                return;
            }

            _ready.Set();

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.Message == WM_DISPATCH)
                {
                    DrainWork();
                    continue;
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private void DrainWork()
        {
            while (_work.TryDequeue(out var item))
                item.Execute();
        }

        private readonly struct WorkItem
        {
            private readonly Action? _sync;
            private readonly Func<Task>? _async;
            private readonly TaskCompletionSource? _tcs;

            private WorkItem(Action? sync, Func<Task>? async, TaskCompletionSource? tcs)
            {
                _sync = sync;
                _async = async;
                _tcs = tcs;
            }

            public static WorkItem Sync(Action callback) => new(callback, null, null);

            public static WorkItem Async(Func<Task> callback, TaskCompletionSource tcs) => new(null, callback, tcs);

            public void Execute()
            {
                if (_sync is not null)
                {
                    RunLogged(_sync);
                    return;
                }

                if (_async is null || _tcs is null)
                    return;

                try
                {
                    var task = _async();
                    if (task.IsCompleted)
                    {
                        Complete(_tcs, task);
                        return;
                    }

                    _ = task.ContinueWith(
                        static (completed, state) =>
                        {
                            if (state is TaskCompletionSource tcs)
                                Complete(tcs, completed);
                        },
                        _tcs,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
                    if (!_tcs.TrySetException(ex))
                        Log.Instance.Warning($"Headless dispatcher async callback failed: {ex.Message}", ex);
                }
            }

            private static void Complete(TaskCompletionSource tcs, Task task)
            {
                if (task.IsFaulted)
                {
                    var exception = task.Exception;
                    if (exception is not null)
                        tcs.TrySetException(exception.InnerExceptions);
                    else
                        tcs.TrySetException(new InvalidOperationException("Dispatcher async callback faulted without an exception."));
                    return;
                }

                if (task.IsCanceled)
                {
                    tcs.TrySetCanceled();
                    return;
                }

                tcs.TrySetResult();
            }
        }
    }
#endif
}
