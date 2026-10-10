using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation.Optimization;
using UniversalDeviceToolkit.Lib.Network;
using UniversalDeviceToolkit.Lib.Optimization;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

internal static class OptimizationRequests
{
    internal const int ElevationRequiredErrorCode = BridgeErrorCodes.ElevationRequired;
    private static WindowsOptimizationService OptimizationService => IoCContainer.Resolve<WindowsOptimizationService>();

    internal static bool TryGetActionKeys(BridgeRequest request, out IReadOnlyList<string> actionKeys)
    {
        actionKeys = [];
        if (!request.Parameters.TryGetProperty("actionKeys", out var keysProp) ||
            keysProp.ValueKind != JsonValueKind.Array)
            return false;

        var keys = new List<string>();
        foreach (var item in keysProp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return false;
            var key = item.GetString();
            if (string.IsNullOrWhiteSpace(key))
                return false;
            keys.Add(key.Trim());
        }

        actionKeys = keys;
        return true;
    }

    /// <summary>Logs why the elevation channel cannot serve a mutation before returning -1006.</summary>
    internal static void LogElevationUnavailable(string method)
    {
        Log.Instance.Warning(
            $"{method} requires elevation but the optimization elevation executor is not registered " +
            "(WindowsOptimizationElevationIoCModule missing from the Host IoC container).");
    }

    internal static BridgeResult MapMutationError(Exception ex, string method)
    {
        if (ex is OperationCanceledException)
            return BridgeResult.Error(BridgeErrorCodes.RequestCancelled, "Request cancelled");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Optimization bridge operation failed. [method={method}]", ex);

        if (IsElevationFailure(ex))
        {
            return BridgeResult.Error(
                ElevationRequiredErrorCode,
                $"{method} requires elevation; the bridge host is not elevated. {ex.GetType().Name}: {ex.Message}");
        }

        return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
    }

    internal static bool IsElevationFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is InvalidOperationException &&
                (current.Message.Contains("elevation executor is not registered", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("is not elevated", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    internal static void EnsureNonEmptyActionKeys(IReadOnlyList<string> actionKeys)
    {
        if (actionKeys.Count == 0)
            throw new BridgeErrorException(-32602, "Parameter 'actionKeys' must contain at least one action key.");
    }

    internal static void EnsureKnownActionKeys(IReadOnlyList<string> actionKeys, bool cleanup)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in OptimizationService.GetCategories())
        {
            var isCleanup = category.Key.StartsWith("cleanup.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category.Key, WindowsOptimizationService.CleanupCategoryKey, StringComparison.OrdinalIgnoreCase);
            if (cleanup != isCleanup)
                continue;
            foreach (var action in category.Actions)
                known.Add(action.Key);
        }

        foreach (var key in actionKeys)
        {
            if (!known.Contains(key))
            {
                throw new BridgeErrorException(
                    -32602,
                    cleanup
                        ? $"Unknown cleanup action key '{key}'."
                        : $"Unknown optimization action key '{key}'.");
            }
        }
    }
}
