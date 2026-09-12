using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Listeners;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public class LidOpenedAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.LidOpenedAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.LidOpened };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<NativeWindowsMessageListener>();
        var result = listener.IsLidOpen;
        return Task.FromResult(result);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.LidOpen = true;

    public IAutomationPipelineTrigger DeepCopy() => new LidOpenedAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is LidOpenedAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
