using static UniversalDeviceToolkit.Host.Rpc.Handlers.OptimizationRequests;
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

/// <summary>
/// System optimization categories, action status, apply and revert.
///
/// Elevation note: the Host process normally runs un-elevated, so apply/revert/
/// cleanup mutations route through WindowsOptimizationElevationClient
/// (UniversalDeviceToolkit.Lib.Automation.Optimization), which starts an elevated
/// worker (UAC prompt) over a private named pipe when needed.
/// </summary>
public static class OptimizationHandlers
{
    private static WindowsOptimizationService OptimizationService => IoCContainer.Resolve<WindowsOptimizationService>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("optimization.getCategories", (_, ct) => HandleGetCategoriesAsync(ct));
        rpc.RegisterHandler("optimization.apply", (request, ct) => HandleApplyAsync(request, ct));
        rpc.RegisterHandler("optimization.revert", (request, ct) => HandleRevertAsync(request, ct));
        rpc.RegisterHandler("optimization.applyRecommended", (_, ct) => HandleApplyRecommendedAsync(ct));
        rpc.RegisterHandler("optimization.getActionStatus", (request, ct) => HandleGetActionStatusAsync(request, ct));
    }

    /// <summary>Categories with titles/descriptions resolved for the current culture plus per-action applied state.</summary>
    private static async Task<BridgeResult> HandleGetCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var service = OptimizationService;
            var categories = service.GetCategories();

            // Probe the applied state of every action in parallel; a failed probe
            // (or an action without an IsAppliedAsync predicate) surfaces as null → "unknown".
            var actionDefinitions = categories.SelectMany(category => category.Actions).ToList();
            var appliedStates = await Task.WhenAll(
                actionDefinitions.Select(action => service.TryGetActionAppliedAsync(action.Key, cancellationToken)))
                .ConfigureAwait(false);

            var appliedByKey = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < actionDefinitions.Count; i++)
                appliedByKey[actionDefinitions[i].Key] = appliedStates[i];

            return BridgeResult.Ok(new
            {
                categories = categories.Select(category => new
                {
                    key = category.Key,
                    title = Localize(category.TitleResourceKey),
                    description = Localize(category.DescriptionResourceKey),
                    actions = category.Actions.Select(action => new
                    {
                        key = action.Key,
                        title = Localize(action.TitleResourceKey),
                        description = Localize(action.DescriptionResourceKey),
                        recommended = action.Recommended,
                        applied = appliedByKey.TryGetValue(action.Key, out var applied) ? applied : null,
                    }).ToArray(),
                }).ToArray(),
            });
        }
        catch (OperationCanceledException)
        {
            return BridgeResult.Error(BridgeErrorCodes.RequestCancelled, "Request cancelled");
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Applies the given actions through the elevation channel (or in-process for plugin actions).</summary>
    private static async Task<BridgeResult> HandleApplyAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetActionKeys(request, out var actionKeys))
                throw new BridgeErrorException(-32602, "Missing or invalid array parameter 'actionKeys'.");

            EnsureNonEmptyActionKeys(actionKeys);
            EnsureKnownActionKeys(actionKeys, cleanup: false);

            await ExecuteOptimizationMutationsAsync(actionKeys, apply: true, cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { applied = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return MapMutationError(ex, "optimization.apply");
        }
    }

    /// <summary>Reverts the given actions (rollback); built-in actions run in the elevated worker.</summary>
    private static async Task<BridgeResult> HandleRevertAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetActionKeys(request, out var actionKeys))
                throw new BridgeErrorException(-32602, "Missing or invalid array parameter 'actionKeys'.");

            EnsureNonEmptyActionKeys(actionKeys);
            EnsureKnownActionKeys(actionKeys, cleanup: false);

            await ExecuteOptimizationMutationsAsync(actionKeys, apply: false, cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { reverted = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return MapMutationError(ex, "optimization.revert");
        }
    }

    /// <summary>Applies every recommended non-cleanup action through the elevation channel.</summary>
    private static async Task<BridgeResult> HandleApplyRecommendedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recommendedKeys = OptimizationService.GetCategories()
                .Where(category =>
                    !category.Key.StartsWith("cleanup.", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(category.Key, WindowsOptimizationService.CleanupCategoryKey, StringComparison.OrdinalIgnoreCase))
                .SelectMany(category => category.Actions.Where(action => action.Recommended).Select(action => action.Key))
                .ToList();

            await ExecuteOptimizationMutationsAsync(recommendedKeys, apply: true, cancellationToken).ConfigureAwait(false);

            return BridgeResult.Ok(new { applied = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return MapMutationError(ex, "optimization.applyRecommended");
        }
    }

    /// <summary>Applied state of a single action: true/false/unknown (null).</summary>
    private static async Task<BridgeResult> HandleGetActionStatusAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("actionKey", out var keyProp) ||
                keyProp.ValueKind != JsonValueKind.String)
                throw new BridgeErrorException(-32602, "Missing or invalid string parameter 'actionKey'.");

            var actionKey = keyProp.GetString();
            if (string.IsNullOrWhiteSpace(actionKey))
                throw new BridgeErrorException(-32602, "Missing or invalid string parameter 'actionKey'.");

            var applied = await OptimizationService
                .TryGetActionAppliedAsync(actionKey, cancellationToken)
                .ConfigureAwait(false);

            return BridgeResult.Ok(new { applied });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BridgeResult.Error(-32602, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return BridgeResult.Error(BridgeErrorCodes.RequestCancelled, "Request cancelled");
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves a Lib resource key (e.g. "WindowsOptimization_Action_*_Title") to the current
    /// culture's string. Falls back to the raw key so unknown keys stay visible to the frontend.
    /// </summary>
    private static string Localize(string resourceKey)
        => LocalizationCatalog.GetString(
            Resource.ResourceManager,
            resourceKey,
            resourceKey,
            LocalizationRuntime.CurrentCulture);

    /// <summary>
    /// Executes apply/revert mutations through the elevation channel
    /// (WindowsOptimizationElevationClient starts an elevated worker over a
    /// private named pipe when the bridge host is un-elevated).
    /// </summary>
    private static async Task ExecuteOptimizationMutationsAsync(
        IReadOnlyList<string> actionKeys,
        bool apply,
        CancellationToken cancellationToken)
    {
        if (actionKeys.Count == 0)
            return;

        if (!WindowsOptimizationElevationBridge.IsAvailable)
        {
            LogElevationUnavailable(apply ? "optimization.apply" : "optimization.revert");
            throw new BridgeErrorException(
                ElevationRequiredErrorCode,
                "The optimization elevation executor is not registered; this operation requires elevation and the bridge host is not elevated.");
        }

        if (apply)
        {
            await WindowsOptimizationElevationBridge
                .ExecuteRecommendedAsync(actionKeys, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var key in actionKeys)
                await WindowsOptimizationElevationBridge
                    .ExecuteActionAsync(key, apply: false, cancellationToken).ConfigureAwait(false);
        }
    }

}
