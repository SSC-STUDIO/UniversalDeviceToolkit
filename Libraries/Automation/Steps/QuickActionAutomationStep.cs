using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class QuickActionAutomationStep(Guid? pipelineId)
    : IAutomationStep
{
    public Guid? PipelineId { get; } = pipelineId;

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => Task.CompletedTask;

    IAutomationStep IAutomationStep.DeepCopy() => new QuickActionAutomationStep(PipelineId);
}
