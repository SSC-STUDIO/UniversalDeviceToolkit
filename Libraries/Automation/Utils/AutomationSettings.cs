using System.Collections.Generic;
using System.Text.Json;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Serialization;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.Automation.Utils;

public class AutomationSettings() : AbstractSettings<AutomationSettings.AutomationSettingsStore>("automation.json")
{
    public class AutomationSettingsStore
    {
        public bool IsEnabled { get; set; }

        public List<AutomationPipeline> Pipelines { get; set; } = [];
    }

    protected override AutomationSettingsStore Default => new()
    {
        Pipelines =
        {
            new AutomationPipeline
            {
                Trigger = new ACAdapterConnectedAutomationPipelineTrigger(),
                Steps = { new PowerModeAutomationStep(PowerModeState.Balance) },
            },
            new AutomationPipeline
            {
                Trigger = new ACAdapterDisconnectedAutomationPipelineTrigger(),
                Steps = { new PowerModeAutomationStep(PowerModeState.Quiet) },
            },
            new AutomationPipeline
            {
                // Stable key — display name is resolved via PipelineNameLocalizer at UI time.
                Name = PipelineNameLocalizer.DeactivateGpuQuickActionStableName,
                Steps = { new DeactivateGPUAutomationStep(DeactivateGPUAutomationStepState.KillApps) },
            },
        },
    };

    protected override void ConfigureJsonSerializerOptions(JsonSerializerOptions options)
    {
        options.Converters.Add(new AutomationPipelineTriggerJsonConverter());
        options.Converters.Add(new AutomationStepJsonConverter());
    }

    public override AutomationSettingsStore? LoadStore()
    {
        var store = base.LoadStore();
        if (store?.Pipelines is null)
            return store;

        if (PipelineNameLocalizer.MigrateBakedDefaultNames(store.Pipelines))
        {
            // Persist stable keys so language switches stop re-showing old baked titles.
            try
            {
                SynchronizeStore();
            }
            catch
            {
                // Display still works via LocalizeStoredName even if write fails.
            }
        }

        return store;
    }
}
