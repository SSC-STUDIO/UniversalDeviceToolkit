using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Automation.Steps;

namespace UniversalDeviceToolkit.Lib.Automation.Utils;

/// <summary>
/// Relocalizes known default pipeline display names that were baked into
/// <c>automation.json</c> as culture-specific strings at first install.
/// </summary>
public static class PipelineNameLocalizer
{
    /// <summary>Stable key for the built-in Deactivate GPU quick action (not shown in UI).</summary>
    public const string DeactivateGpuQuickActionStableName = "__udt.quickAction.deactivateGpu";

    private static readonly string ResourceKey = nameof(Resource.DeactivateGpuQuickAction_Title);

    /// <summary>
    /// Cultures used to recognize historical baked-in titles (expand if new ships appear).
    /// </summary>
    private static readonly string[] RecognitionCultures =
    [
        "", // neutral / invariant Resource.resx
        "en",
        "en-US",
        "de",
        "de-DE",
        "zh-Hans",
        "zh-CN",
        "zh-Hant",
        "zh-TW",
        "fr",
        "es",
        "ja",
        "ru",
        "pt-BR",
        "pt",
        "pl",
        "it",
        "nl",
        "uk",
        "tr",
        "vi",
        "cs",
        "sk",
        "hu",
        "ro",
        "bg",
        "el",
        "ar",
        "lv",
    ];

    /// <summary>
    /// Returns a culture-appropriate display name for a stored pipeline name.
    /// Unknown / user-renamed names are returned unchanged.
    /// </summary>
    public static string? LocalizeStoredName(string? storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            return storedName;

        if (string.Equals(storedName, DeactivateGpuQuickActionStableName, StringComparison.Ordinal) ||
            IsKnownDeactivateGpuTitle(storedName))
        {
            // Never leak the stable storage key into UI even if the satellite is missing.
            return ResolveDeactivateGpuTitle();
        }

        return storedName;
    }

    /// <summary>
    /// Current-culture title for the built-in Deactivate GPU quick action, with English fallback.
    /// </summary>
    public static string ResolveDeactivateGpuTitle()
    {
        var title = Resource.DeactivateGpuQuickAction_Title;
        if (!string.IsNullOrWhiteSpace(title) &&
            !string.Equals(title, DeactivateGpuQuickActionStableName, StringComparison.Ordinal))
            return title;

        try
        {
            title = Resource.ResourceManager.GetString(ResourceKey, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch
        {
            // ignore and use hard-coded English
        }

        return "Deactivate GPU";
    }

    /// <summary>
    /// True when <paramref name="storedName"/> matches the built-in Deactivate GPU title
    /// in any known culture (or the stable key).
    /// </summary>
    public static bool IsKnownDeactivateGpuTitle(string? storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName))
            return false;

        if (string.Equals(storedName, DeactivateGpuQuickActionStableName, StringComparison.Ordinal))
            return true;

        foreach (var title in EnumerateKnownDeactivateGpuTitles())
        {
            if (string.Equals(storedName, title, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Remaps baked-in default names on loaded pipelines to the stable key so future
    /// UI language switches stay correct. Returns true if any pipeline was changed.
    /// </summary>
    public static bool MigrateBakedDefaultNames(IEnumerable<AutomationPipeline>? pipelines)
    {
        if (pipelines is null)
            return false;

        var changed = false;
        foreach (var pipeline in pipelines)
        {
            if (pipeline is null)
                continue;

            if (IsDefaultDeactivateGpuPipeline(pipeline) &&
                !string.Equals(pipeline.Name, DeactivateGpuQuickActionStableName, StringComparison.Ordinal))
            {
                pipeline.Name = DeactivateGpuQuickActionStableName;
                changed = true;
                continue;
            }

            if (IsKnownDeactivateGpuTitle(pipeline.Name) &&
                !string.Equals(pipeline.Name, DeactivateGpuQuickActionStableName, StringComparison.Ordinal))
            {
                pipeline.Name = DeactivateGpuQuickActionStableName;
                changed = true;
            }
        }

        return changed;
    }

    public static bool IsDefaultDeactivateGpuPipeline(AutomationPipeline pipeline)
    {
        if (pipeline.Trigger is not null)
            return false;

        if (pipeline.Steps is null || pipeline.Steps.Count != 1)
            return false;

        return pipeline.Steps[0] is DeactivateGPUAutomationStep;
    }

    private static IEnumerable<string> EnumerateKnownDeactivateGpuTitles()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rm = Resource.ResourceManager;

        // Always include the currently active title.
        TryAdd(seen, Resource.DeactivateGpuQuickAction_Title);

        foreach (var name in RecognitionCultures)
        {
            try
            {
                var culture = string.IsNullOrEmpty(name)
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.GetCultureInfo(name);
                TryAdd(seen, rm.GetString(ResourceKey, culture));
            }
            catch (CultureNotFoundException)
            {
                // Skip unknown culture ids on older OS builds.
            }
        }

        // Hard-coded fallbacks for the most common baked-in titles (in case satellites are absent).
        TryAdd(seen, "Deactivate GPU");
        TryAdd(seen, "Deaktiviere GPU");
        TryAdd(seen, "停用 GPU");
        TryAdd(seen, "休眠独立显卡");

        return seen;
    }

    private static void TryAdd(HashSet<string> set, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            set.Add(value);
    }
}
