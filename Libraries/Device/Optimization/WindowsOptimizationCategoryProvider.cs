using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Optimization;

public class WindowsOptimizationCategoryProvider
{
    private readonly WindowsOptimizationService _service;
    private readonly WindowsCleanupService _cleanupService;

    public WindowsOptimizationCategoryProvider(WindowsOptimizationService service, WindowsCleanupService cleanupService)
    {
        _service = service;
        _cleanupService = cleanupService;
    }

    public IReadOnlyList<WindowsOptimizationCategoryDefinition> BuildCategories() =>
    [
        CreateExplorerCategory(),
        CreatePerformanceCategory(),
        CreateServicesCategory(),
        CreateCleanupCacheCategory(),
        CreateCleanupSystemFilesCategory(),
        CreateCleanupSystemComponentsCategory(),
        CreateCleanupPerformanceCategory(),
        CreateCleanupLargeFilesCategory(),
        CreateCleanupCustomCategory()
    ];

    private WindowsOptimizationCategoryDefinition CreateExplorerCategory() =>
        new(
            "explorer",
            "WindowsOptimization_Category_Explorer_Title",
            "WindowsOptimization_Category_Explorer_Description",
            [
                _service.CreateRegistryAction(
                    "explorer.taskbar",
                    "WindowsOptimization_Action_ExplorerTaskbar_Title",
                    "WindowsOptimization_Action_ExplorerTaskbar_Description",
                    WindowsOptimizationDefinitions.ExplorerTaskbarTweaks),
                new WindowsOptimizationActionDefinition(
                    "explorer.startMenu",
                    "WindowsOptimization_Action_ExplorerStartMenu_Title",
                    "WindowsOptimization_Action_ExplorerStartMenu_Description",
                    _service.ExecuteStartMenuDisableAsync,
                    Recommended: false,
                    IsAppliedAsync: ct => Task.FromResult(_service.AreStartMenuTweaksApplied()),
                    RollbackAsync: _service.RevertStartMenuDisableAsync),
                _service.CreateRegistryAction(
                    "explorer.responsiveness",
                    "WindowsOptimization_Action_ExplorerResponsiveness_Title",
                    "WindowsOptimization_Action_ExplorerResponsiveness_Description",
                    WindowsOptimizationDefinitions.ExplorerResponsivenessTweaks),
                _service.CreateRegistryAction(
                    "explorer.visibility",
                    "WindowsOptimization_Action_ExplorerVisibility_Title",
                    "WindowsOptimization_Action_ExplorerVisibility_Description",
                    WindowsOptimizationDefinitions.ExplorerVisibilityTweaks),
                _service.CreateRegistryAction(
                    "explorer.suggestions",
                    "WindowsOptimization_Action_ExplorerSuggestions_Title",
                    "WindowsOptimization_Action_ExplorerSuggestions_Description",
                    WindowsOptimizationDefinitions.ExplorerSuggestionsTweaks),
            ]);

    private WindowsOptimizationCategoryDefinition CreatePerformanceCategory() =>
        new(
            "performance",
            "WindowsOptimization_Category_Performance_Title",
            "WindowsOptimization_Category_Performance_Description",
            [
                _service.CreateRegistryAction(
                    "performance.multimedia",
                    "WindowsOptimization_Action_PerformanceMultimedia_Title",
                    "WindowsOptimization_Action_PerformanceMultimedia_Description",
                    WindowsOptimizationDefinitions.MultimediaTweaks),
                _service.CreateRegistryAction(
                    "performance.memory",
                    "WindowsOptimization_Action_PerformanceMemory_Title",
                    "WindowsOptimization_Action_PerformanceMemory_Description",
                    WindowsOptimizationDefinitions.MemoryTweaks),
                _service.CreateRegistryAction(
                    "performance.notifications",
                    "WindowsOptimization_Action_PerformanceNotifications_Title",
                    "WindowsOptimization_Action_PerformanceNotifications_Description",
                    WindowsOptimizationDefinitions.NotificationTweaks,
                    recommended: false),
                _service.CreateRegistryAction(
                    "performance.telemetry",
                    "WindowsOptimization_Action_PerformanceTelemetry_Title",
                    "WindowsOptimization_Action_PerformanceTelemetry_Description",
                    WindowsOptimizationDefinitions.TelemetryTweaks),
                _service.CreateCommandAction(
                    "performance.powerPlan",
                    "WindowsOptimization_Action_PerformancePowerPlan_Title",
                    "WindowsOptimization_Action_PerformancePowerPlan_Description",
                    WindowsOptimizationDefinitions.PowerPlanCommands,
                    isAppliedAsync: _service.IsHighPerformancePowerPlanActiveAsync,
                    rollbackAsync: _service.RevertPowerPlanAsync)
            ]);

    private WindowsOptimizationCategoryDefinition CreateServicesCategory() =>
        new(
            "services",
            "WindowsOptimization_Category_Services_Title",
            "WindowsOptimization_Category_Services_Description",
            [
                _service.CreateServiceAction(
                    "services.diagnostics",
                    "WindowsOptimization_Action_ServicesDiagnostics_Title",
                    "WindowsOptimization_Action_ServicesDiagnostics_Description",
                    WindowsOptimizationDefinitions.DiagnosticsServices),
                _service.CreateServiceAction(
                    "services.sysmain",
                    "WindowsOptimization_Action_ServicesSysMain_Title",
                    "WindowsOptimization_Action_ServicesSysMain_Description",
                    WindowsOptimizationDefinitions.SysMainService),
                _service.CreateServiceAction(
                    "services.search",
                    "WindowsOptimization_Action_ServicesSearch_Title",
                    "WindowsOptimization_Action_ServicesSearch_Description",
                    WindowsOptimizationDefinitions.SearchService,
                    recommended: false),
                _service.CreateServiceAction(
                    "services.remoteRegistry",
                    "WindowsOptimization_Action_ServicesRemoteRegistry_Title",
                    "WindowsOptimization_Action_ServicesRemoteRegistry_Description",
                    WindowsOptimizationDefinitions.RemoteRegistryService),
                _service.CreateServiceAction(
                    "services.errorReporting",
                    "WindowsOptimization_Action_ServicesErrorReporting_Title",
                    "WindowsOptimization_Action_ServicesErrorReporting_Description",
                    WindowsOptimizationDefinitions.ErrorReportingService)
            ]);

    private WindowsOptimizationCategoryDefinition CreateCleanupCacheCategory() =>
        new(
            "cleanup.cache",
            "WindowsOptimization_Category_CleanupCache_Title",
            "WindowsOptimization_Category_CleanupCache_Description",
            [
                _service.CreateCommandAction(
                    "cleanup.browserCache",
                    "WindowsOptimization_Action_CleanupBrowserCache_Title",
                    "WindowsOptimization_Action_CleanupBrowserCache_Description",
                    WindowsOptimizationDefinitions.BrowserCacheCommands),
                _service.CreateCommandAction(
                    "cleanup.appLeftovers",
                    "WindowsOptimization_Action_CleanupAppLeftovers_Title",
                    "WindowsOptimization_Action_CleanupAppLeftovers_Description",
                    WindowsOptimizationDefinitions.AppLeftoverCommands),
                _service.CreateCommandAction(
                    "cleanup.thumbnailCache",
                    "WindowsOptimization_Action_CleanupThumbnailCache_Title",
                    "WindowsOptimization_Action_CleanupThumbnailCache_Description",
                    WindowsOptimizationDefinitions.ThumbnailCacheCommands),
                _service.CreateCommandAction(
                    "cleanup.remoteDesktopCache",
                    "WindowsOptimization_Action_CleanupRemoteDesktop_Title",
                    "WindowsOptimization_Action_CleanupRemoteDesktop_Description",
                    WindowsOptimizationDefinitions.RemoteDesktopCacheCommands),
                _service.CreateCommandAction(
                    "cleanup.shaderCache",
                    "WindowsOptimization_Action_CleanupShaderCache_Title",
                    "WindowsOptimization_Action_CleanupShaderCache_Description",
                    WindowsOptimizationDefinitions.ShaderCacheCommands),
                _service.CreateCommandAction(
                    "cleanup.instantMessaging",
                    "WindowsOptimization_Action_CleanupInstantMessaging_Title",
                    "WindowsOptimization_Action_CleanupInstantMessaging_Description",
                    WindowsOptimizationDefinitions.InstantMessagingCacheCommands),
                _service.CreateCommandAction(
                    "cleanup.devCaches",
                    "WindowsOptimization_Action_CleanupDevCaches_Title",
                    "WindowsOptimization_Action_CleanupDevCaches_Description",
                    WindowsOptimizationDefinitions.DevCacheCommands)
            ]);

    private WindowsOptimizationCategoryDefinition CreateCleanupSystemFilesCategory() =>
        new(
            "cleanup.systemFiles",
            "WindowsOptimization_Category_CleanupSystemFiles_Title",
            "WindowsOptimization_Category_CleanupSystemFiles_Description",
            [
                _service.CreateCommandAction(
                    "cleanup.tempFiles",
                    "WindowsOptimization_Action_CleanupTempFiles_Title",
                    "WindowsOptimization_Action_CleanupTempFiles_Description",
                    WindowsOptimizationDefinitions.TempCommands),
                _service.CreateCommandAction(
                    "cleanup.logs",
                    "WindowsOptimization_Action_CleanupLogs_Title",
                    "WindowsOptimization_Action_CleanupLogs_Description",
                    WindowsOptimizationDefinitions.SystemLogCommands),
                new WindowsOptimizationActionDefinition(
                    "cleanup.registry",
                    "WindowsOptimization_Action_CleanupRegistry_Title",
                    "WindowsOptimization_Action_CleanupRegistry_Description",
                    ct => _cleanupService.ExecuteRegistryCleanupAsync(ct),
                    Recommended: false),
                _service.CreateCommandAction(
                    "cleanup.crashDumps",
                    "WindowsOptimization_Action_CleanupCrashDumps_Title",
                    "WindowsOptimization_Action_CleanupCrashDumps_Description",
                    WindowsOptimizationDefinitions.CrashDumpCommands),
                _service.CreateCommandAction(
                    "cleanup.recycleBin",
                    "WindowsOptimization_Action_CleanupRecycleBin_Title",
                    "WindowsOptimization_Action_CleanupRecycleBin_Description",
                    WindowsOptimizationDefinitions.RecycleBinCommands),
                _service.CreateCommandAction(
                    "cleanup.defender",
                    "WindowsOptimization_Action_CleanupDefender_Title",
                    "WindowsOptimization_Action_CleanupDefender_Description",
                    WindowsOptimizationDefinitions.DefenderCommands,
                    recommended: false)
            ]);

    private WindowsOptimizationCategoryDefinition CreateCleanupSystemComponentsCategory() =>
        new(
            "cleanup.systemComponents",
            "WindowsOptimization_Category_CleanupSystemComponents_Title",
            "WindowsOptimization_Category_CleanupSystemComponents_Description",
            [
                _service.CreateCommandAction(
                    "cleanup.windowsUpdate",
                    "WindowsOptimization_Action_CleanupWindowsUpdate_Title",
                    "WindowsOptimization_Action_CleanupWindowsUpdate_Description",
                    WindowsOptimizationDefinitions.WindowsUpdateCacheCommands),
                _service.CreateCommandAction(
                    "cleanup.componentStore",
                    "WindowsOptimization_Action_CleanupComponentStore_Title",
                    "WindowsOptimization_Action_CleanupComponentStore_Description",
                    WindowsOptimizationDefinitions.ComponentStoreCommands),
                _service.CreateCommandAction(
                    "cleanup.dotnetNative",
                    "WindowsOptimization_Action_CleanupDotNet_Title",
                    "WindowsOptimization_Action_CleanupDotNet_Description",
                    WindowsOptimizationDefinitions.DotnetNativeImageCommands,
                    recommended: false)
            ]);

    private WindowsOptimizationCategoryDefinition CreateCleanupPerformanceCategory() =>
        new(
            "cleanup.performance",
            "WindowsOptimization_Category_CleanupPerformance_Title",
            "WindowsOptimization_Category_CleanupPerformance_Description",
            [
                _service.CreateCommandAction(
                    "cleanup.prefetch",
                    "WindowsOptimization_Action_CleanupPrefetch_Title",
                    "WindowsOptimization_Action_CleanupPrefetch_Description",
                    WindowsOptimizationDefinitions.PrefetchCommands,
                    recommended: false)
            ]);

    private WindowsOptimizationCategoryDefinition CreateCleanupLargeFilesCategory() =>
        new(
            "cleanup.largeFiles",
            "WindowsOptimization_Category_CleanupLargeFiles_Title",
            "WindowsOptimization_Category_CleanupLargeFiles_Description",
            [
                new WindowsOptimizationActionDefinition(
                    "cleanup.largeFiles",
                    "WindowsOptimization_Action_CleanupLargeFiles_Title",
                    "WindowsOptimization_Action_CleanupLargeFiles_Description",
                    ct => Task.CompletedTask,
                    Recommended: false)
            ]);

    private WindowsOptimizationCategoryDefinition CreateCleanupCustomCategory() =>
        new(
            "cleanup.custom",
            "WindowsOptimization_Category_CleanupCustom_Title",
            "WindowsOptimization_Category_CleanupCustom_Description",
            [
                new WindowsOptimizationActionDefinition(
                    WindowsOptimizationService.CustomCleanupActionKey,
                    "WindowsOptimization_Action_CleanupCustom_Title",
                    "WindowsOptimization_Action_CleanupCustom_Description",
                    ct => _cleanupService.ExecuteCustomCleanupAsync(ct),
                    Recommended: false)
            ]);
}
