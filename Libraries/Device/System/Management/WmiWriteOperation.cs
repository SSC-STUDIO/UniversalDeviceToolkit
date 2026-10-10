using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.System.Management;

public enum WmiWriteStatus
{
    Unavailable = 0,
    Succeeded = 1,
    TimedOutIndeterminate = 2,
    NotStartedBusy = 3,
    FailedIndeterminate = 4
}

public readonly record struct WmiWriteResult(WmiWriteStatus Status)
{
    public bool IsSuccess => Status == WmiWriteStatus.Succeeded;

    internal static WmiWriteResult Success => new(WmiWriteStatus.Succeeded);
    internal static WmiWriteResult Unavailable => new(WmiWriteStatus.Unavailable);
    internal static WmiWriteResult TimedOutIndeterminate => new(WmiWriteStatus.TimedOutIndeterminate);
    internal static WmiWriteResult NotStartedBusy => new(WmiWriteStatus.NotStartedBusy);
    internal static WmiWriteResult FailedIndeterminate => new(WmiWriteStatus.FailedIndeterminate);

    internal void ThrowIfNotSucceeded(
        string scope,
        string query,
        string methodName,
        int timeoutMilliseconds)
    {
        switch (Status)
        {
            case WmiWriteStatus.Succeeded:
                return;
            case WmiWriteStatus.Unavailable:
                throw new WmiWriteUnavailableException(scope, query, methodName);
            case WmiWriteStatus.TimedOutIndeterminate:
                throw new WmiWriteIndeterminateException(scope, query, methodName, timeoutMilliseconds);
            case WmiWriteStatus.NotStartedBusy:
                throw new WmiWriteBusyException(scope, query, methodName, timeoutMilliseconds);
            case WmiWriteStatus.FailedIndeterminate:
                throw new WmiWriteFailedIndeterminateException(scope, query, methodName);
            default:
                throw new ArgumentOutOfRangeException(nameof(Status), Status, "Unknown WMI write status.");
        }
    }
}

public readonly record struct WmiWriteResult<T>(WmiWriteStatus Status, T Value)
{
    public bool IsSuccess => Status == WmiWriteStatus.Succeeded;

    internal static WmiWriteResult<T> Success(T value) => new(WmiWriteStatus.Succeeded, value);
    internal static WmiWriteResult<T> Unavailable => new(WmiWriteStatus.Unavailable, default!);
    internal static WmiWriteResult<T> TimedOutIndeterminate => new(WmiWriteStatus.TimedOutIndeterminate, default!);
    internal static WmiWriteResult<T> NotStartedBusy => new(WmiWriteStatus.NotStartedBusy, default!);
    internal static WmiWriteResult<T> FailedIndeterminate => new(WmiWriteStatus.FailedIndeterminate, default!);

    internal T GetValueOrThrow(
        string scope,
        string query,
        string methodName,
        int timeoutMilliseconds)
    {
        new WmiWriteResult(Status).ThrowIfNotSucceeded(scope, query, methodName, timeoutMilliseconds);
        return Value;
    }
}

public sealed class WmiWriteUnavailableException : InvalidOperationException
{
    public WmiWriteUnavailableException(string scope, string query, string methodName)
        : base($"WMI write method '{methodName}' is unavailable. [scope={scope}, query={query}]")
    {
        Scope = scope;
        Query = query;
        MethodName = methodName;
    }

    public string Scope { get; }
    public string Query { get; }
    public string MethodName { get; }
    public WmiWriteStatus Status => WmiWriteStatus.Unavailable;
}

public sealed class WmiWriteIndeterminateException : TimeoutException
{
    public WmiWriteIndeterminateException(
        string scope,
        string query,
        string methodName,
        int timeoutMilliseconds)
        : base(
            $"WMI write method '{methodName}' timed out after {timeoutMilliseconds}ms. " +
            $"The invocation may still complete; no overlapping retry was started. [scope={scope}, query={query}]")
    {
        Scope = scope;
        Query = query;
        MethodName = methodName;
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    public string Scope { get; }
    public string Query { get; }
    public string MethodName { get; }
    public int TimeoutMilliseconds { get; }
    public WmiWriteStatus Status => WmiWriteStatus.TimedOutIndeterminate;
}

public sealed class WmiWriteBusyException : TimeoutException
{
    public WmiWriteBusyException(
        string scope,
        string query,
        string methodName,
        int timeoutMilliseconds)
        : base(
            $"WMI write method '{methodName}' did not start within {timeoutMilliseconds}ms because a prior write " +
            $"is still active. This invocation was not launched. [scope={scope}, query={query}]")
    {
        Scope = scope;
        Query = query;
        MethodName = methodName;
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    public string Scope { get; }
    public string Query { get; }
    public string MethodName { get; }
    public int TimeoutMilliseconds { get; }
    public WmiWriteStatus Status => WmiWriteStatus.NotStartedBusy;
}

public sealed class WmiWriteFailedIndeterminateException : InvalidOperationException
{
    public WmiWriteFailedIndeterminateException(string scope, string query, string methodName)
        : base(
            $"WMI write method '{methodName}' failed after invocation started. " +
            $"The hardware side effect is indeterminate. [scope={scope}, query={query}]")
    {
        Scope = scope;
        Query = query;
        MethodName = methodName;
    }

    public string Scope { get; }
    public string Query { get; }
    public string MethodName { get; }
    public WmiWriteStatus Status => WmiWriteStatus.FailedIndeterminate;
}

internal sealed class WmiWriteCoordinator
{
    internal const int MaxPendingOperationsPerKey = 8;

    private enum OperationState
    {
        Queued,
        Running,
        Completed
    }

    private interface IQueuedOperation
    {
        string Key { get; }
        QueueState Queue { get; set; }
        LinkedListNode<IQueuedOperation>? Node { get; set; }
        OperationState State { get; set; }
        Task TimeoutTask { get; }

        void Start();
        void CompleteNotStarted();
    }

    private sealed class QueueState
    {
        internal bool IsRunning { get; set; }
        internal LinkedList<IQueuedOperation> Pending { get; } = [];
    }

    private sealed class QueuedOperation<TResult> : IQueuedOperation
    {
        private readonly WmiWriteCoordinator _owner;

        internal QueuedOperation(
            WmiWriteCoordinator owner,
            string key,
            Func<Task<TResult>> invocation,
            Task timeoutTask,
            TResult invocationTimeoutResult,
            TResult notStartedResult,
            Action<Exception>? lateFailureHandler)
        {
            _owner = owner;
            Key = key;
            Invocation = invocation;
            TimeoutTask = timeoutTask;
            InvocationTimeoutResult = invocationTimeoutResult;
            NotStartedResult = notStartedResult;
            LateFailureHandler = lateFailureHandler;
        }

        public string Key { get; }
        internal Func<Task<TResult>> Invocation { get; }
        internal TResult InvocationTimeoutResult { get; }
        internal TResult NotStartedResult { get; }
        internal Action<Exception>? LateFailureHandler { get; }
        internal TaskCompletionSource<TResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public QueueState Queue { get; set; } = null!;
        public LinkedListNode<IQueuedOperation>? Node { get; set; }
        public OperationState State { get; set; }
        public Task TimeoutTask { get; }

        public void Start() => _ = _owner.RunInvocationAsync(this);

        public void CompleteNotStarted() => Completion.TrySetResult(NotStartedResult);
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, QueueState> _queues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<TimeSpan, Task> _timeoutTaskFactory;

    internal WmiWriteCoordinator()
        : this(Task.Delay)
    {
    }

    internal WmiWriteCoordinator(Func<TimeSpan, Task> timeoutTaskFactory)
    {
        ArgumentNullException.ThrowIfNull(timeoutTaskFactory);
        _timeoutTaskFactory = timeoutTaskFactory;
    }

    internal int ActiveKeyCount
    {
        get
        {
            lock (_lock)
                return _queues.Count;
        }
    }

    internal int PendingOperationCount
    {
        get
        {
            lock (_lock)
            {
                var count = 0;
                foreach (var queue in _queues.Values)
                    count += queue.Pending.Count;
                return count;
            }
        }
    }

    internal Task<TResult> ExecuteAsync<TResult>(
        string key,
        Func<Task<TResult>> invocation,
        TimeSpan timeout,
        TResult invocationTimeoutResult,
        TResult notStartedResult,
        Action<Exception>? lateFailureHandler = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(invocation);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");

        var timeoutTask = _timeoutTaskFactory(timeout)
            ?? throw new InvalidOperationException("The WMI write timeout task factory returned null.");
        var operation = new QueuedOperation<TResult>(
            this,
            key,
            invocation,
            timeoutTask,
            invocationTimeoutResult,
            notStartedResult,
            lateFailureHandler);
        var startImmediately = false;
        var rejectImmediately = false;

        lock (_lock)
        {
            if (!_queues.TryGetValue(key, out var queue))
            {
                queue = new QueueState();
                _queues.Add(key, queue);
            }

            operation.Queue = queue;
            if (!queue.IsRunning)
            {
                queue.IsRunning = true;
                operation.State = OperationState.Running;
                startImmediately = true;
            }
            else if (queue.Pending.Count >= MaxPendingOperationsPerKey)
            {
                operation.State = OperationState.Completed;
                rejectImmediately = true;
            }
            else
            {
                operation.State = OperationState.Queued;
                operation.Node = queue.Pending.AddLast(operation);
            }
        }

        if (rejectImmediately)
        {
            operation.CompleteNotStarted();
        }
        else if (startImmediately)
        {
            _ = ObserveTimeoutTaskAsync(timeoutTask);
            operation.Start();
        }
        else
        {
            _ = ExpireQueuedOperationAsync(operation);
        }

        return operation.Completion.Task;
    }

    private async Task RunInvocationAsync<TResult>(QueuedOperation<TResult> operation)
    {
        Task<TResult> invocationTask;
        try
        {
            invocationTask = operation.Invocation()
                ?? throw new InvalidOperationException("The WMI write invocation returned null.");
        }
        catch (Exception ex)
        {
            operation.Completion.TrySetException(ex);
            FinishRunningOperation(operation);
            return;
        }

        var completedTask = await Task.WhenAny(
            invocationTask,
            operation.TimeoutTask).ConfigureAwait(false);
        if (completedTask == invocationTask || invocationTask.IsCompleted)
        {
            try
            {
                operation.Completion.TrySetResult(await invocationTask.ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                operation.Completion.TrySetException(ex);
            }
            finally
            {
                FinishRunningOperation(operation);
            }

            return;
        }

        operation.Completion.TrySetResult(operation.InvocationTimeoutResult);
        _ = CompleteTimedOutInvocationAsync(operation, invocationTask);
    }

    private async Task CompleteTimedOutInvocationAsync<TResult>(
        QueuedOperation<TResult> operation,
        Task<TResult> invocationTask)
    {
        Exception? lateFailure = null;
        try
        {
            _ = await invocationTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lateFailure = ex;
        }
        finally
        {
            FinishRunningOperation(operation);
        }

        if (lateFailure is null || operation.LateFailureHandler is null)
            return;

        try
        {
            operation.LateFailureHandler(lateFailure);
        }
        catch (Exception handlerException)
        {
            Debug.WriteLine(
                $"Failed to report a late WMI write failure. {handlerException}");
        }
    }

    private async Task ExpireQueuedOperationAsync(IQueuedOperation operation)
    {
        await ObserveTimeoutTaskAsync(operation.TimeoutTask).ConfigureAwait(false);

        var expired = false;
        lock (_lock)
        {
            if (operation.State != OperationState.Queued)
                return;

            if (operation.Node is not null)
                operation.Queue.Pending.Remove(operation.Node);
            operation.Node = null;
            operation.State = OperationState.Completed;
            expired = true;
        }

        if (expired)
            operation.CompleteNotStarted();
    }

    private static async Task ObserveTimeoutTaskAsync(Task timeoutTask)
    {
        try
        {
            await timeoutTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WMI write timeout task failed. {ex}");
        }
    }

    private void FinishRunningOperation(IQueuedOperation operation)
    {
        IQueuedOperation? next = null;
        List<IQueuedOperation>? expired = null;

        lock (_lock)
        {
            if (operation.State != OperationState.Running)
                return;

            operation.State = OperationState.Completed;
            var queue = operation.Queue;
            queue.IsRunning = false;

            while (queue.Pending.First is { } first)
            {
                var candidate = first.Value;
                queue.Pending.RemoveFirst();
                candidate.Node = null;

                // The deadline is authoritative even if its continuation has not run yet.
                // Never launch an operation whose queued wait already expired.
                if (candidate.TimeoutTask.IsCompleted)
                {
                    candidate.State = OperationState.Completed;
                    (expired ??= []).Add(candidate);
                    continue;
                }

                next = candidate;
                candidate.State = OperationState.Running;
                queue.IsRunning = true;
                break;
            }

            if (next is null)
            {
                _queues.Remove(operation.Key);
            }
        }

        if (expired is not null)
        {
            foreach (var expiredOperation in expired)
                expiredOperation.CompleteNotStarted();
        }

        next?.Start();
    }
}
