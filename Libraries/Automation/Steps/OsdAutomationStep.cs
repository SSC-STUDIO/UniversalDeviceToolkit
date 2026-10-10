using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public class OsdAutomationStep(OsdState state)
    : IAutomationStep<OsdState>
{
    public OsdState State { get; } = state;

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<OsdState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<OsdState>());

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        MessagingCenter.Publish(new OsdChangedMessage(State));
        return Task.CompletedTask;
    }

    IAutomationStep IAutomationStep.DeepCopy() => new OsdAutomationStep(State);
}
