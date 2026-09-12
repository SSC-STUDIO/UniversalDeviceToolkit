using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.System;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public class ExternalDisplayDisconnectedAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    [JsonIgnore] public string DisplayName => Resource.ExternalDisplayDisconnectedAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.ExternalMonitorDisconnected };
        return Task.FromResult(result);
    }

    public async Task<bool> IsMatchingState()
    {
        return await Task.Run(() => ExternalDisplays.Get().Length < 1).ConfigureAwait(false);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.ExternalDisplayConnected = false;

    public IAutomationPipelineTrigger DeepCopy() => new ExternalDisplayDisconnectedAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is ExternalDisplayDisconnectedAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
