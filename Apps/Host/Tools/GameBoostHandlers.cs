using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.GameDetection;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Bridges GameBoostService for intelligent game detection, process priority,
/// CPU affinity optimization, and background process suppression.
/// </summary>
public static class GameBoostHandlers
{
    private static GameBoostService Service => IoCContainer.Resolve<GameBoostService>();
    private static GameBoostSettings Settings => IoCContainer.Resolve<GameBoostSettings>();

    public static void Register(BridgeRpcServer rpc)
    {
        var service = Service;
        service.StatusChanged += (_, status) =>
        {
            try
            {
                rpc.Publish("gameBoost.statusChanged", MapStatus(status));
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to publish gameBoost.statusChanged event: {ex.Message}");
            }
        };

        rpc.RegisterHandler("gameBoost.getStatus", async _ =>
        {
            await Task.CompletedTask;
            return BridgeResult.Ok(MapStatus(service.GetStatus()));
        });

        rpc.RegisterHandler("gameBoost.getConfig", async _ =>
        {
            var store = await Settings.LoadStoreAsync().ConfigureAwait(false)
                ?? new GameBoostSettings.GameBoostSettingsStore();
            return BridgeResult.Ok(MapConfig(store));
        });

        rpc.RegisterHandler("gameBoost.saveConfig", async (request, _) =>
        {
            if (!request.Parameters.TryGetProperty("config", out var configProp))
                return BridgeResult.Error(-32602, "Missing 'config' parameter.");

            var store = await Settings.LoadStoreAsync().ConfigureAwait(false)
                ?? new GameBoostSettings.GameBoostSettingsStore();

            if (configProp.TryGetProperty("autoGameBoost", out var autoProp) &&
                (autoProp.ValueKind == JsonValueKind.True || autoProp.ValueKind == JsonValueKind.False))
                store.AutoGameBoost = autoProp.GetBoolean();

            if (configProp.TryGetProperty("boostGamePriority", out var boostPrioProp) &&
                (boostPrioProp.ValueKind == JsonValueKind.True || boostPrioProp.ValueKind == JsonValueKind.False))
                store.BoostGamePriority = boostPrioProp.GetBoolean();

            if (configProp.TryGetProperty("optimizeCpuAffinity", out var optAffinityProp) &&
                (optAffinityProp.ValueKind == JsonValueKind.True || optAffinityProp.ValueKind == JsonValueKind.False))
                store.OptimizeCpuAffinity = optAffinityProp.GetBoolean();

            if (configProp.TryGetProperty("suppressBackgroundProcesses", out var supBgProp) &&
                (supBgProp.ValueKind == JsonValueKind.True || supBgProp.ValueKind == JsonValueKind.False))
                store.SuppressBackgroundProcesses = supBgProp.GetBoolean();

            if (configProp.TryGetProperty("muteNotifications", out var muteProp) &&
                (muteProp.ValueKind == JsonValueKind.True || muteProp.ValueKind == JsonValueKind.False))
                store.MuteNotifications = muteProp.GetBoolean();

            if (configProp.TryGetProperty("gamePowerPlanGuid", out var planProp))
                store.GamePowerPlanGuid = planProp.ValueKind == JsonValueKind.String ? planProp.GetString() : null;

            if (configProp.TryGetProperty("customGameProcesses", out var gamesProp) &&
                gamesProp.ValueKind == JsonValueKind.Array)
            {
                store.CustomGameProcesses.Clear();
                foreach (var item in gamesProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name && !string.IsNullOrWhiteSpace(name))
                        store.CustomGameProcesses.Add(name.Trim());
                }
            }

            if (configProp.TryGetProperty("backgroundWhitelist", out var wlProp) &&
                wlProp.ValueKind == JsonValueKind.Array)
            {
                store.BackgroundWhitelist.Clear();
                foreach (var item in wlProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name && !string.IsNullOrWhiteSpace(name))
                        store.BackgroundWhitelist.Add(name.Trim());
                }
            }

            var currentStore = Settings.Store;
            currentStore.AutoGameBoost = store.AutoGameBoost;
            currentStore.BoostGamePriority = store.BoostGamePriority;
            currentStore.OptimizeCpuAffinity = store.OptimizeCpuAffinity;
            currentStore.SuppressBackgroundProcesses = store.SuppressBackgroundProcesses;
            currentStore.MuteNotifications = store.MuteNotifications;
            currentStore.GamePowerPlanGuid = store.GamePowerPlanGuid;
            currentStore.CustomGameProcesses = [.. store.CustomGameProcesses];
            currentStore.BackgroundWhitelist = [.. store.BackgroundWhitelist];

            await Settings.SynchronizeStoreAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { saved = true });
        });

        rpc.RegisterHandler("gameBoost.boostNow", async _ =>
        {
            var success = await service.OptimizeForegroundGameAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { success, status = MapStatus(service.GetStatus()) });
        });

        rpc.RegisterHandler("gameBoost.revertNow", async _ =>
        {
            await service.RevertOptimizationsAsync().ConfigureAwait(false);
            return BridgeResult.Ok(new { success = true, status = MapStatus(service.GetStatus()) });
        });
    }

    private static object MapStatus(GameBoostService.BoostStatus status)
    {
        return new
        {
            isBoosting = status.IsBoosting,
            activeGameProcess = status.ActiveGameProcess,
            activeGameProcessId = status.ActiveGameProcessId,
            boostedProcesses = status.BoostedProcesses,
            suppressedProcessesCount = status.SuppressedProcessesCount
        };
    }

    private static object MapConfig(GameBoostSettings.GameBoostSettingsStore store)
    {
        return new
        {
            autoGameBoost = store.AutoGameBoost,
            boostGamePriority = store.BoostGamePriority,
            optimizeCpuAffinity = store.OptimizeCpuAffinity,
            suppressBackgroundProcesses = store.SuppressBackgroundProcesses,
            muteNotifications = store.MuteNotifications,
            gamePowerPlanGuid = store.GamePowerPlanGuid,
            customGameProcesses = store.CustomGameProcesses,
            backgroundWhitelist = store.BackgroundWhitelist
        };
    }
}
