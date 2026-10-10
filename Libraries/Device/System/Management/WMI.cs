using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System.Management;

public static partial class WMI
{
    // Caller-visible WMI method waits must honor the 2,500ms ceiling enforced by
    // KNOWNLEDGE_BASE.md "WMI Timeout Protection" rule (#2). InvokeMethod itself cannot
    // be cancelled, so timed-out writes retain their keyed queue slot until they really finish.
    // Never raise this above 3,000ms or the caller may stall well past the async contract.
    // Keep well under AbstractSensorsController's 2s snapshot budget (CPU+GPU fan in parallel).
    private const int _wmiInvokeTimeoutMs = 800;

    // Soft-failed method signatures for this process — avoids re-invoking known-missing firmware methods
    // (which would otherwise spam first-chance ManagementException during capability probes).
    private static readonly ConcurrentDictionary<string, byte> _softFailedMethodKeys = new(StringComparer.Ordinal);
    private static readonly WmiWriteCoordinator _writeCoordinator = new();

    private static bool IsAccessDenied(ManagementException ex) =>
        ex.ErrorCode == ManagementStatus.AccessDenied
        || ex.Message.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for WMI failures that capability probing should treat as "feature unavailable"
    /// rather than a hard crash. Access denied is excluded so elevation issues still surface.
    /// </summary>
    private static bool IsSoftWmiFailure(ManagementException ex) =>
        !IsAccessDenied(ex);

    /// <summary>
    /// Only permanent method-missing cases. Do NOT match generic "does not exist" / NotFound —
    /// those fire for bad object state / access and wrongly disabled Fan_GetCurrentFanSpeed
    /// process-wide (sticky 0 RPM with no LHM recovery).
    /// </summary>
    private static bool IsMethodMissing(ManagementException ex) =>
        ex.ErrorCode is ManagementStatus.InvalidMethod
        || ex.Message.Contains("not implemented", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("未实现", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("未在任何类中实现", StringComparison.OrdinalIgnoreCase);

    private static string SoftFailKey(string scope, string queryFormatted, string methodName) =>
        string.Concat(scope, "\u001f", queryFormatted, "\u001f", methodName);

    internal static bool IsWmiMethodSoftFailed(string scope, string queryFormatted, string methodName) =>
        _softFailedMethodKeys.ContainsKey(SoftFailKey(scope, queryFormatted, methodName));

    private static void MarkWmiMethodSoftFailed(string scope, string queryFormatted, string methodName) =>
        _softFailedMethodKeys.TryAdd(SoftFailKey(scope, queryFormatted, methodName), 0);

    /// <summary>
    /// Try GetMethodParameters without rethrowing soft failures (no InvalidOperationException).
    /// Permanent soft-fail cache is only for true method-missing — never for per-ID invoke fails.
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    private static bool TryGetWmiMethodParameters(
        ManagementObject target,
        string methodName,
        string scope,
        string queryFormatted,
        out ManagementBaseObject? args)
    {
        try
        {
            args = target.GetMethodParameters(methodName);
            return true;
        }
        catch (ManagementException ex) when (IsSoftWmiFailure(ex))
        {
            // Only cache true method-missing; not every soft ManagementException.
            if (IsMethodMissing(ex))
                MarkWmiMethodSoftFailed(scope, queryFormatted, methodName);
            args = null;
            return false;
        }
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    private static bool TryInvokeWmiMethod(
        ManagementObject target,
        string methodName,
        ManagementBaseObject args,
        string scope,
        string queryFormatted,
        out ManagementBaseObject? result)
    {
        try
        {
            result = target.InvokeMethod(methodName, args, new InvokeMethodOptions());
            // A completed void WMI method may legitimately return no out-parameter object.
            // Completion without an exception is still a successful invocation.
            return true;
        }
        catch (ManagementException ex) when (IsSoftWmiFailure(ex))
        {
            // Soft-fail including Invalid object / invalid object — never rethrow.
            // Lenovo providers often invalidate the MO between Get() and InvokeMethod();
            // TryCallReadInternalAsync re-queries a fresh instance on the next attempt.
            // Rethrowing here only spammed first-chance exceptions in the debugger while
            // fan/sensor probes ran every second.
            //
            // Do NOT permanently soft-fail on Invoke failures.
            // GetFeatureValue / similar methods accept many IDs: one unsupported fan ID must
            // not poison temperature / other capabilities for the rest of the process.
            // Method-missing is still cached in TryGetWmiMethodParameters.
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(
                    $"WMI invoke soft-failed (not cached). [method={methodName}, query={queryFormatted}, code={ex.ErrorCode}]",
                    ex);
            result = null;
            return false;
        }
    }

    internal static async Task<bool> ExistsAsync(string scope, FormattableString query)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        try
        {
            using var mos = new ManagementObjectSearcher(scope, queryFormatted);
            var managementObjects = await mos.GetAsyncWithTimeout().ConfigureAwait(false);
            try
            {
                return managementObjects.Length > 0;
            }
            finally
            {
                managementObjects.DisposeAll();
            }
        }
        catch (ManagementException ex) when (IsAccessDenied(ex))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI exists probe denied. [scope={scope}, query={queryFormatted}]", ex);

            return false;
        }
        catch (ManagementException ex) when (
            ex.ErrorCode is ManagementStatus.InvalidClass
                or ManagementStatus.InvalidNamespace
                or ManagementStatus.NotSupported
                or ManagementStatus.NotFound
                or ManagementStatus.InvalidQuery
            || ex.Message.Contains("不支持", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI exists probe unavailable. [scope={scope}, query={queryFormatted}]", ex);

            return false;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"WMI exists probe failed or timed out. [scope={scope}, query={queryFormatted}]", ex);

            return false;
        }
    }

    /// <summary>
    /// Sync WMI event subscription. Blocks the caller while starting the watcher
    /// (see <see cref="ManagementEventWatcherExtensions.StartWithTimeout"/>). Prefer
    /// <see cref="ListenAsync"/> from async / UI code paths.
    /// </summary>
    private static LambdaDisposable Listen(string scope, FormattableString query, Action<PropertyDataCollection> handler)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var watcher = new ManagementEventWatcher(scope, queryFormatted);
        watcher.EventArrived += (_, e) => handler(e.NewEvent.Properties);

        try
        {
            // Blocks calling thread; Start itself runs on the thread pool. Prefer ListenAsync.
            watcher.StartWithTimeout();
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.InvalidClass || ex.ErrorCode == ManagementStatus.InvalidNamespace)
        {
            watcher.Dispose();
            throw ExceptionHelper.WmiClassNotAvailable(scope, queryFormatted, ex);
        }

        return CreateWatcherDisposable(watcher);
    }

    /// <summary>
    /// Async WMI event subscription. Does not block the calling thread while starting.
    /// Preferred for <see cref="Listeners.AbstractWMIListener{TEventArgs,TValue,TRawValue}"/> and other async hosts.
    /// </summary>
    private static async Task<IDisposable> ListenAsync(string scope, FormattableString query, Action<PropertyDataCollection> handler)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var watcher = new ManagementEventWatcher(scope, queryFormatted);
        watcher.EventArrived += (_, e) => handler(e.NewEvent.Properties);

        try
        {
            await watcher.StartAsyncWithTimeout().ConfigureAwait(false);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.InvalidClass || ex.ErrorCode == ManagementStatus.InvalidNamespace)
        {
            watcher.Dispose();
            throw ExceptionHelper.WmiClassNotAvailable(scope, queryFormatted, ex);
        }

        return CreateWatcherDisposable(watcher);
    }

    private static LambdaDisposable CreateWatcherDisposable(ManagementEventWatcher watcher) =>
        new(() =>
        {
            try
            {
                watcher.Stop();
            }
            catch (ManagementException ex)
            {
                Log.Instance.TraceOnce("wmi-watcher-stop", "WMI event watcher Stop failed during dispose.", ex);
            }
            finally
            {
                watcher.Dispose();
            }
        });

    internal static async Task<IEnumerable<T>> ReadAsync<T>(string scope, FormattableString query, Func<PropertyDataCollection, T> converter)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        try
        {
            using var mos = new ManagementObjectSearcher(scope, queryFormatted);
            var managementObjects = await mos.GetAsyncWithTimeout().ConfigureAwait(false);
            try
            {
                return managementObjects.Select(mo => converter(mo.Properties)).ToArray();
            }
            finally
            {
                managementObjects.DisposeAll();
            }
        }
        catch (ManagementException ex)
        {
            throw ExceptionHelper.WmiReadFailed(ex.Message, scope, query, ex);
        }
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    internal static async Task CallAsync(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams)
    {
        // The non-generic overload is reserved for mutating calls. Keep its Task-returning
        // compatibility surface, but never report successful completion for an unavailable
        // or indeterminate write.
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var result = await TryCallWriteInternalAsync(
            scope,
            queryFormatted,
            methodName,
            methodParams).ConfigureAwait(false);
        result.ThrowIfNotSucceeded(scope, queryFormatted, methodName, _wmiInvokeTimeoutMs);
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    internal static async Task<T> CallAsync<T>(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams, Func<PropertyDataCollection, T> converter)
    {
        using var result = await TryCallReadInternalAsync(
            scope,
            query,
            methodName,
            methodParams).ConfigureAwait(false);
        if (result is null)
        {
            // Do not throw: sensor/capability probes hit this path every second on some machines.
            // Return default; callers already treat 0 / false / null as unsupported.
            // Permanent soft-fail is reserved for IsMethodMissing only (see TryGetWmiMethodParameters).
            return default!;
        }

        return converter(result.Properties);
    }

    /// <summary>
    /// Probe-friendly call: returns false instead of throwing when the firmware does not
    /// implement the method (e.g. Fan_GetCurrentFanSpeed on some Legion models).
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    internal static async Task<(bool Success, T Value)> TryCallAsync<T>(
        string scope,
        FormattableString query,
        string methodName,
        Dictionary<string, object> methodParams,
        Func<PropertyDataCollection, T> converter,
        T fallback = default!)
    {
        using var result = await TryCallReadInternalAsync(
            scope,
            query,
            methodName,
            methodParams).ConfigureAwait(false);
        if (result is null)
            return (false, fallback);

        try
        {
            return (true, converter(result.Properties));
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                $"wmi-trycall-convert-{methodName}",
                $"WMI TryCallAsync converter failed for {methodName}.",
                ex);
            return (false, fallback);
        }
    }

    /// <summary>
    /// Mutating WMI call with an explicit outcome. It is invoked once, serialized by
    /// scope/query/method, and remains the head of that key's queue until the underlying
    /// invocation actually completes even if this method returns TimedOutIndeterminate.
    /// A queued caller whose own deadline expires returns NotStartedBusy and is removed.
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    internal static Task<WmiWriteResult> CallWriteAsync(
        string scope,
        FormattableString query,
        string methodName,
        Dictionary<string, object> methodParams)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        return TryCallWriteInternalAsync(scope, queryFormatted, methodName, methodParams);
    }

    /// <summary>
    /// Mutating WMI call that also converts an out parameter. Unlike the probe/read APIs,
    /// this call is serialized and never retried.
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    internal static Task<WmiWriteResult<T>> CallWriteAsync<T>(
        string scope,
        FormattableString query,
        string methodName,
        Dictionary<string, object> methodParams,
        Func<PropertyDataCollection, T> converter)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        return TryCallWriteInternalAsync(
            scope,
            queryFormatted,
            methodName,
            methodParams,
            converter);
    }

    /// <summary>
    /// Compatibility shape for a mutating method that returns an out value. The value is
    /// returned only for a confirmed invocation; unavailable and indeterminate outcomes throw.
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    internal static async Task<T> CallWriteRequiredAsync<T>(
        string scope,
        FormattableString query,
        string methodName,
        Dictionary<string, object> methodParams,
        Func<PropertyDataCollection, T> converter)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var result = await TryCallWriteInternalAsync(
            scope,
            queryFormatted,
            methodName,
            methodParams,
            converter).ConfigureAwait(false);
        return result.GetValueOrThrow(
            scope,
            queryFormatted,
            methodName,
            _wmiInvokeTimeoutMs);
    }

    /// <summary>
    /// Serializes a complete write sequence under one scope/query/method key. The continuation
    /// runs before the key is released, allowing a safe fallback without nested coordination.
    /// </summary>
    internal static async Task<WmiWriteResult> CallWriteSequenceAsync(
        string scope,
        FormattableString query,
        string methodName,
        Dictionary<string, object> methodParams,
        Func<WmiWriteResult, Task<WmiWriteResult>> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var key = SoftFailKey(scope, queryFormatted, methodName);
        var methodParamsSnapshot = SnapshotWriteParameters(methodParams);
        var result = await _writeCoordinator.ExecuteAsync(
            key,
            async () =>
            {
                var classicResult = _softFailedMethodKeys.ContainsKey(key)
                    ? WmiWriteResult.Unavailable
                    : await Task.Run(
                        () => ExecuteWmiWriteMethodCall(
                            scope,
                            queryFormatted,
                            methodName,
                            methodParamsSnapshot)).ConfigureAwait(false);
                return await continuation(classicResult).ConfigureAwait(false);
            },
            TimeSpan.FromMilliseconds(_wmiInvokeTimeoutMs),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy,
            ex => LogLateWriteFailure(scope, queryFormatted, methodName, ex)).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Full WMI method call. Soft failures return false (no InvalidOperationException).
    /// Do not use GetAsyncWithTimeout here — it disposes the collection before InvokeMethod.
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    private static bool TryExecuteWmiMethodCall(
        string scope,
        string queryFormatted,
        string methodName,
        Dictionary<string, object> methodParams,
        out ManagementBaseObject? result)
    {
        result = null;
        using var searcher = new ManagementObjectSearcher(scope, queryFormatted);
        using var collection = searcher.Get();
        using var mo = collection.Cast<ManagementObject>().FirstOrDefault();
        if (mo is null)
            return false;

        if (!TryGetWmiMethodParameters(mo, methodName, scope, queryFormatted, out var args) || args is null)
            return false;

        using (args)
        {
            foreach (var pair in methodParams)
                args[pair.Key] = pair.Value;

            return TryInvokeWmiMethod(mo, methodName, args, scope, queryFormatted, out result);
        }
    }

    private static WmiWriteResult ExecuteWmiWriteMethodCall(
        string scope,
        string queryFormatted,
        string methodName,
        Dictionary<string, object> methodParams)
    {
        var status = ExecuteWmiWriteInvocation(
            scope,
            queryFormatted,
            methodName,
            methodParams,
            out var result);

        result?.Dispose();
        return new WmiWriteResult(status);
    }

    private static WmiWriteResult<T> ExecuteWmiWriteMethodCall<T>(
        string scope,
        string queryFormatted,
        string methodName,
        Dictionary<string, object> methodParams,
        Func<PropertyDataCollection, T> converter)
    {
        var status = ExecuteWmiWriteInvocation(
            scope,
            queryFormatted,
            methodName,
            methodParams,
            out var result);
        if (status != WmiWriteStatus.Succeeded || result is null)
        {
            result?.Dispose();
            return new WmiWriteResult<T>(
                status == WmiWriteStatus.Succeeded
                    ? WmiWriteStatus.FailedIndeterminate
                    : status,
                default!);
        }

        using (result)
        {
            try
            {
                return WmiWriteResult<T>.Success(converter(result.Properties));
            }
            catch (Exception ex)
            {
                Log.Instance.Warning(
                    $"WMI write completed but its response could not be converted. " +
                    $"[scope={scope}, query={queryFormatted}, methodName={methodName}]",
                    ex);
                return WmiWriteResult<T>.FailedIndeterminate;
            }
        }
    }

    private static ManagementBaseObject? GetWmiWriteMethodParameters(
        ManagementObject target,
        string methodName,
        string scope,
        string queryFormatted)
    {
        try
        {
            return target.GetMethodParameters(methodName);
        }
        catch (ManagementException ex) when (IsMethodMissing(ex))
        {
            MarkWmiMethodSoftFailed(scope, queryFormatted, methodName);
            return null;
        }
    }

    private static WmiWriteStatus ExecuteWmiWriteInvocation(
        string scope,
        string queryFormatted,
        string methodName,
        Dictionary<string, object> methodParams,
        out ManagementBaseObject? result)
    {
        result = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, queryFormatted);
            using var collection = searcher.Get();
            using var target = collection.Cast<ManagementObject>().FirstOrDefault();
            if (target is null)
                return WmiWriteStatus.Unavailable;

            using var args = GetWmiWriteMethodParameters(
                target,
                methodName,
                scope,
                queryFormatted);
            if (args is null)
                return WmiWriteStatus.Unavailable;

            foreach (var pair in methodParams)
                args[pair.Key] = pair.Value;

            try
            {
                result = target.InvokeMethod(methodName, args, new InvokeMethodOptions());
                return WmiWriteStatus.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Instance.Warning(
                    $"WMI write failed after InvokeMethod started; side effect is indeterminate. " +
                    $"[scope={scope}, query={queryFormatted}, methodName={methodName}]",
                    ex);
                return WmiWriteStatus.FailedIndeterminate;
            }
        }
        catch (ManagementException ex) when (IsPreInvocationWriteUnavailable(ex.ErrorCode))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(
                    $"WMI write provider unavailable before invocation. " +
                    $"[scope={scope}, query={queryFormatted}, methodName={methodName}, code={ex.ErrorCode}]",
                    ex);
            return WmiWriteStatus.Unavailable;
        }
    }

    internal static bool IsPreInvocationWriteUnavailable(ManagementStatus status) =>
        status is ManagementStatus.InvalidClass
            or ManagementStatus.InvalidNamespace
            or ManagementStatus.NotFound
            or ManagementStatus.NotSupported;

    private static Dictionary<string, object> SnapshotWriteParameters(
        Dictionary<string, object> methodParams)
    {
        var snapshot = new Dictionary<string, object>(methodParams.Count, methodParams.Comparer);
        foreach (var pair in methodParams)
            snapshot.Add(pair.Key, pair.Value is Array array ? array.Clone() : pair.Value);
        return snapshot;
    }

    private static async Task<WmiWriteResult> TryCallWriteInternalAsync(
        string scope,
        string queryFormatted,
        string methodName,
        Dictionary<string, object> methodParams)
    {
        var key = SoftFailKey(scope, queryFormatted, methodName);
        if (_softFailedMethodKeys.ContainsKey(key))
            return WmiWriteResult.Unavailable;

        var methodParamsSnapshot = SnapshotWriteParameters(methodParams);
        var result = await _writeCoordinator.ExecuteAsync(
            key,
            () =>
            {
                if (_softFailedMethodKeys.ContainsKey(key))
                    return Task.FromResult(WmiWriteResult.Unavailable);

                return Task.Run(
                    () => ExecuteWmiWriteMethodCall(
                        scope,
                        queryFormatted,
                        methodName,
                        methodParamsSnapshot));
            },
            TimeSpan.FromMilliseconds(_wmiInvokeTimeoutMs),
            WmiWriteResult.TimedOutIndeterminate,
            WmiWriteResult.NotStartedBusy,
            ex => LogLateWriteFailure(scope, queryFormatted, methodName, ex)).ConfigureAwait(false);

        if (result.Status == WmiWriteStatus.TimedOutIndeterminate)
        {
            Log.Instance.Warning(
                $"WMI write timed out after {_wmiInvokeTimeoutMs}ms and remains serialized until completion. " +
                $"[scope={scope}, query={queryFormatted}, methodName={methodName}]");
        }
        else if (result.Status == WmiWriteStatus.NotStartedBusy)
        {
            Log.Instance.Warning(
                $"WMI write was not started because the prior same-key invocation remained active for " +
                $"{_wmiInvokeTimeoutMs}ms. [scope={scope}, query={queryFormatted}, methodName={methodName}]");
        }

        return result;
    }

    private static async Task<WmiWriteResult<T>> TryCallWriteInternalAsync<T>(
        string scope,
        string queryFormatted,
        string methodName,
        Dictionary<string, object> methodParams,
        Func<PropertyDataCollection, T> converter)
    {
        var key = SoftFailKey(scope, queryFormatted, methodName);
        if (_softFailedMethodKeys.ContainsKey(key))
            return WmiWriteResult<T>.Unavailable;

        var methodParamsSnapshot = SnapshotWriteParameters(methodParams);
        var result = await _writeCoordinator.ExecuteAsync(
            key,
            () =>
            {
                if (_softFailedMethodKeys.ContainsKey(key))
                    return Task.FromResult(WmiWriteResult<T>.Unavailable);

                return Task.Run(
                    () => ExecuteWmiWriteMethodCall(
                        scope,
                        queryFormatted,
                        methodName,
                        methodParamsSnapshot,
                        converter));
            },
            TimeSpan.FromMilliseconds(_wmiInvokeTimeoutMs),
            WmiWriteResult<T>.TimedOutIndeterminate,
            WmiWriteResult<T>.NotStartedBusy,
            ex => LogLateWriteFailure(scope, queryFormatted, methodName, ex)).ConfigureAwait(false);

        if (result.Status == WmiWriteStatus.TimedOutIndeterminate)
        {
            Log.Instance.Warning(
                $"WMI write timed out after {_wmiInvokeTimeoutMs}ms and remains serialized until completion. " +
                $"[scope={scope}, query={queryFormatted}, methodName={methodName}]");
        }
        else if (result.Status == WmiWriteStatus.NotStartedBusy)
        {
            Log.Instance.Warning(
                $"WMI write was not started because the prior same-key invocation remained active for " +
                $"{_wmiInvokeTimeoutMs}ms. [scope={scope}, query={queryFormatted}, methodName={methodName}]");
        }

        return result;
    }

    private static void LogLateWriteFailure(
        string scope,
        string queryFormatted,
        string methodName,
        Exception exception)
    {
        Log.Instance.Warning(
            $"WMI write failed after its caller received an indeterminate timeout. " +
            $"[scope={scope}, query={queryFormatted}, methodName={methodName}]",
            exception);
    }

    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    private static async Task<ManagementBaseObject?> TryCallReadInternalAsync(
        string scope,
        FormattableString query,
        string methodName,
        Dictionary<string, object> methodParams)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var softKey = SoftFailKey(scope, queryFormatted, methodName);
        if (_softFailedMethodKeys.ContainsKey(softKey))
            return null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var callTask = Task.Run(() =>
                {
                    if (TryExecuteWmiMethodCall(scope, queryFormatted, methodName, methodParams, out var r))
                        return r;
                    return null;
                });

                var completed = await Task.WhenAny(callTask, Task.Delay(_wmiInvokeTimeoutMs)).ConfigureAwait(false);
                if (completed != callTask)
                {
                    _ = ObserveTimedOutReadInvocationAsync(
                        callTask,
                        scope,
                        queryFormatted,
                        methodName);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace(
                            $"WMI method {methodName} timed out after {_wmiInvokeTimeoutMs}ms. [query={queryFormatted}]");
                    return null;
                }

                var result = await callTask.ConfigureAwait(false);
                if (result is not null)
                    return result;

                // True method-missing was cached during this attempt — do not retry.
                if (_softFailedMethodKeys.ContainsKey(softKey))
                    return null;

                // Null = transient soft invoke fail (Invalid object / invalid object, bad fan ID…).
                // Re-query once: each TryExecuteWmiMethodCall does a fresh Get(), matching the
                // historical contract when Lenovo invalidates the MO between Get and Invoke.
                // Previously Invalid object was rethrown only to hit catch-retry — that worked
                // but broke into the debugger on every sensor/fan poll.
                if (attempt < 2)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace(
                            $"WMI call soft-failed, retrying with fresh instance. [scope={scope}, query={queryFormatted}, methodName={methodName}, attempt={attempt}]");
                    continue;
                }

                return null;
            }
            catch (Exception ex) when (attempt < 2)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace(
                        $"WMI call failed, retrying. [scope={scope}, query={queryFormatted}, methodName={methodName}, attempt={attempt}]",
                        ex);
            }
            catch (ManagementException ex) when (IsMethodMissing(ex))
            {
                MarkWmiMethodSoftFailed(scope, queryFormatted, methodName);
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace(
                        $"WMI method missing. [scope={scope}, query={queryFormatted}, methodName={methodName}]",
                        ex);
                return null;
            }
            catch (Exception ex)
            {
                // Transient errors: do not permanent-cache; let the next poll retry.
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace(
                        $"WMI call failed (not cached). [scope={scope}, query={queryFormatted}, methodName={methodName}]",
                        ex);
                return null;
            }
        }

        return null;
    }

    private static async Task ObserveTimedOutReadInvocationAsync(
        Task<ManagementBaseObject?> callTask,
        string scope,
        string queryFormatted,
        string methodName)
    {
        try
        {
            using var lateResult = await callTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(
                    $"Timed-out WMI read later failed. [scope={scope}, query={queryFormatted}, methodName={methodName}]",
                    ex);
        }
    }

    internal class WMIPropertyValueFormatter : IFormatProvider, ICustomFormatter
    {
        public static readonly WMIPropertyValueFormatter Instance = new();

        private WMIPropertyValueFormatter() { }

        public object GetFormat(Type? formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;

            throw ExceptionHelper.InvalidTypeOfFormatted();
        }

        public string Format(string? format, object? arg, IFormatProvider? formatProvider)
        {
            var stringArg = arg?.ToString()?.Replace("\\", "\\\\");
            return stringArg ?? string.Empty;
        }
    }
}
