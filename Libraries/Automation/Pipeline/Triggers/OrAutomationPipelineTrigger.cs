using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

[method: JsonConstructor]
public sealed class OrAutomationPipelineTrigger(IAutomationPipelineTrigger[] triggers) : ICompositeAutomationPipelineTrigger
{
    public string DisplayName => string.Join($" {Resources.Resource.OrAutomationPipelineTrigger_Separator} ", Triggers.Select(trigger => trigger.DisplayName));

    public IAutomationPipelineTrigger[] Triggers { get; } = triggers ?? Array.Empty<IAutomationPipelineTrigger>();

    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        foreach (var trigger in Triggers)
        {
            if (await trigger.IsMatchingEvent(automationEvent).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    public async Task<bool> IsMatchingState()
    {
        foreach (var trigger in Triggers)
        {
            if (await trigger.IsMatchingState().ConfigureAwait(false))
                return true;
        }

        return false;
    }

    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        foreach (var trigger in Triggers)
            trigger.UpdateEnvironment(environment);
    }

    public IAutomationPipelineTrigger DeepCopy() => new OrAutomationPipelineTrigger(Triggers.Select(trigger => trigger.DeepCopy()).ToArray());
}