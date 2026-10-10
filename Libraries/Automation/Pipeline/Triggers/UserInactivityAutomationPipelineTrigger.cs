using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

[method: JsonConstructor]
public class UserInactivityAutomationPipelineTrigger(TimeSpan inactivityTimeSpan) : IUserInactivityPipelineTrigger
{
    public string DisplayName => InactivityTimeSpan == TimeSpan.Zero
        ? Resource.UserInactivityAutomationPipelineTrigger_DisplayName_Zero
        : Resource.UserInactivityAutomationPipelineTrigger_DisplayName;

    public TimeSpan InactivityTimeSpan { get; } = inactivityTimeSpan;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not UserInactivityAutomationEvent e)
            return Task.FromResult(false);

        var result = InactivityTimeSpan == e.InactivityTimeSpan;
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<UserInactivityAutoListener>();
        var result = InactivityTimeSpan == listener.InactivityTimeSpan;
        return Task.FromResult(result);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.UserActive = InactivityTimeSpan == TimeSpan.Zero;

    public IAutomationPipelineTrigger DeepCopy() => new UserInactivityAutomationPipelineTrigger(InactivityTimeSpan);

    public IUserInactivityPipelineTrigger DeepCopy(TimeSpan timeSpan) => new UserInactivityAutomationPipelineTrigger(timeSpan);

    public override bool Equals(object? obj) => obj is UserInactivityAutomationPipelineTrigger t && InactivityTimeSpan == t.InactivityTimeSpan;

    public override int GetHashCode() => InactivityTimeSpan.GetHashCode();

    public override string ToString() => $"{nameof(InactivityTimeSpan)}: {InactivityTimeSpan}";
}
