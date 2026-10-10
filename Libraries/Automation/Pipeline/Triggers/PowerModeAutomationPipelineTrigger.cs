using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Features;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

[method: JsonConstructor]
public class PowerModeAutomationPipelineTrigger(PowerModeState powerModeState) : IPowerModeAutomationPipelineTrigger
{
    public string DisplayName => Resource.PowerModeAutomationPipelineTrigger_DisplayName;

    public PowerModeState PowerModeState { get; } = powerModeState;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not PowerModeAutomationEvent e)
            return Task.FromResult(false);

        var result = e.PowerModeState == PowerModeState;
        return Task.FromResult(result);
    }

    public async Task<bool> IsMatchingState()
    {
        var feature = IoCContainer.Resolve<PowerModeFeature>();
        return await feature.GetStateAsync().ConfigureAwait(false) == PowerModeState;
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.PowerMode = PowerModeState;

    public IAutomationPipelineTrigger DeepCopy() => new PowerModeAutomationPipelineTrigger(PowerModeState);

    public IPowerModeAutomationPipelineTrigger DeepCopy(PowerModeState powerModeState) => new PowerModeAutomationPipelineTrigger(powerModeState);

    public override bool Equals(object? obj)
    {
        return obj is PowerModeAutomationPipelineTrigger t && PowerModeState == t.PowerModeState;
    }

    public override int GetHashCode() => HashCode.Combine(PowerModeState);

    public override string ToString() => $"{nameof(PowerModeState)}: {PowerModeState}";
}
