using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Listeners;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public class SessionUnlockAutomationPipelineTrigger : ISessionUnlockPipelineTrigger
{
    [JsonIgnore]
    public string DisplayName => Resource.SessionUnlockAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is SessionLockUnlockAutomationEvent { Locked: false };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<SessionLockUnlockListener>();
        var result = listener.IsLocked;
        return Task.FromResult(result.HasValue && !result.Value);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.SessionLocked = false;

    public IAutomationPipelineTrigger DeepCopy() => new SessionUnlockAutomationPipelineTrigger();

    public override bool Equals(object? obj) => obj is SessionUnlockAutomationPipelineTrigger;

    public override int GetHashCode() => HashCode.Combine(DisplayName);
}
