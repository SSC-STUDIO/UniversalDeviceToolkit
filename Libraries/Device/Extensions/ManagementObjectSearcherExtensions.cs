using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class ManagementObjectSearcherExtensions
{
    // Queries that already failed with "not supported" / missing class, etc.
    // Avoids re-hitting WMI and spamming first-chance ManagementException.
    private static readonly TimeSpan _softFailureTimeToLive = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> _softFailedQueries = new(StringComparer.OrdinalIgnoreCase);

    public static Task<ManagementBaseObject[]> GetAsyncWithTimeout(this ManagementObjectSearcher searcher, int timeoutMs = 2500) =>
        searcher.GetAsync(timeoutMs);

    public static async Task<ManagementBaseObject[]> GetAsync(this ManagementObjectSearcher mos, int timeoutMs = 2500)
    {
        var scopePath = mos.Scope?.Path?.Path ?? string.Empty;
        var queryString = mos.Query?.QueryString ?? throw new ArgumentException("Query is required.", nameof(mos));
        var cacheKey = string.Concat(scopePath, "\u001f", queryString);

        if (_softFailedQueries.TryGetValue(cacheKey, out var failedAt))
        {
            if (DateTimeOffset.UtcNow - failedAt < _softFailureTimeToLive)
                return [];

            _softFailedQueries.TryRemove(cacheKey, out _);
        }

        var task = Task.Run(() => ExecuteQuery(scopePath, queryString, cacheKey));

        using var cts = new CancellationTokenSource();
        var completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)).ConfigureAwait(false);
        if (completedTask == task)
        {
            cts.Cancel();
            return await task.ConfigureAwait(false);
        }

        Log.Instance.Warning($"WMI query timed out after {timeoutMs}ms: {queryString}");

        ObserveOrphanedTask(task, queryString);

        throw new TimeoutException($"WMI query timed out after {timeoutMs}ms.");
    }

    /// <summary>
    /// Enumerate WMI results without calling <see cref="ManagementObjectCollection.Count"/>
    /// (Count also throws ManagementException "not supported" on some providers).
    /// Soft failures return an empty array instead of throwing.
    /// </summary>
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    private static ManagementBaseObject[] ExecuteQuery(string scopePath, string queryString, string cacheKey)
    {
        try
        {
            using var searcher = string.IsNullOrEmpty(scopePath)
                ? new ManagementObjectSearcher(queryString)
                : new ManagementObjectSearcher(scopePath, queryString);

            using var collection = searcher.Get();
            var list = new List<ManagementBaseObject>();

            // Manual enumeration — avoid LINQ ToArray/Count which force ICollection.Count.
            foreach (ManagementBaseObject obj in collection)
                list.Add(obj);

            return list.ToArray();
        }
        catch (ManagementException ex) when (IsSoftQueryFailure(ex))
        {
            _softFailedQueries[cacheKey] = DateTimeOffset.UtcNow;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(
                    $"WMI query not supported or unavailable (soft-fail, cached). [scope={scopePath}, query={queryString}, code={ex.ErrorCode}]",
                    ex);

            return Array.Empty<ManagementBaseObject>();
        }
    }

    public static void DisposeAll(this IEnumerable<ManagementBaseObject> objects)
    {
        foreach (var obj in objects)
        {
            try
            {
                obj.Dispose();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace("Failed to dispose a WMI query result.", ex);
            }
        }
    }

    /// <summary>
    /// Capability / platform probes: provider missing, class missing, "not supported", etc.
    /// </summary>
    private static bool IsSoftQueryFailure(ManagementException ex)
    {
        if (ex.ErrorCode is ManagementStatus.NotSupported
            or ManagementStatus.InvalidClass
            or ManagementStatus.InvalidNamespace
            or ManagementStatus.NotFound
            or ManagementStatus.InvalidObject
            or ManagementStatus.InvalidOperation
            or ManagementStatus.InvalidQuery
            or ManagementStatus.InvalidQueryType
            or ManagementStatus.ProviderNotFound
            or ManagementStatus.ProviderFailure
            or ManagementStatus.ProviderLoadFailure
            or ManagementStatus.Failed)
            return true;

        var msg = ex.Message ?? string.Empty;
        return msg.Contains("not supported", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("不支持", StringComparison.Ordinal)
               || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("找不到", StringComparison.Ordinal)
               || msg.Contains("invalid class", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("无效", StringComparison.Ordinal)
               || msg.Contains("not implemented", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("未实现", StringComparison.Ordinal);
    }

    private static void ObserveOrphanedTask(Task<ManagementBaseObject[]> task, string queryString)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Orphaned WMI query task faulted after timeout. [query={queryString}]", t.Exception);
            }
            else if (t.IsCompletedSuccessfully)
            {
                foreach (var obj in t.Result)
                    obj.Dispose();
            }
        }, TaskContinuationOptions.None);
    }
}
