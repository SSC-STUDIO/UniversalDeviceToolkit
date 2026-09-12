using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Listeners;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public class DisplayOnAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.DisplayOnAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.MonitorOn };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<NativeWindowsMessageListener>();
        var result = listener.IsMonitorOn;
        return Task.FromResult(result);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.DisplayOn = true;

    public IAutomationPipelineTrigger DeepCopy() => new DisplayOnAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is DisplayOnAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
