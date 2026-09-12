using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using System.Text.Json.Serialization;
using WindowsDisplayAPI.Exceptions;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class ResolutionAutomationStep(Resolution state)
    : AbstractFeatureAutomationStep<Resolution>(state)
{
    public override Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
return RetryHelper.RetryAsync(() => base.RunAsync(context, environment, token),
            5,
            TimeSpan.FromSeconds(1),
            ex => ex is ModeChangeException,
            null,
            nameof(ResolutionAutomationStep));
    }

    public override IAutomationStep DeepCopy() => new ResolutionAutomationStep(State);
}
