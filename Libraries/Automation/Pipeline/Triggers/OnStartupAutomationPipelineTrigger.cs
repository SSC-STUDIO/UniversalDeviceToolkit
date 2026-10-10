using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public class OnStartupAutomationPipelineTrigger : IOnStartupAutomationPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.OnStartupAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        return Task.FromResult(automationEvent is StartupAutomationEvent);
    }

    public Task<bool> IsMatchingState() => Task.FromResult(false);

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.Startup = true;

    public IAutomationPipelineTrigger DeepCopy() => new OnStartupAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is OnStartupAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
