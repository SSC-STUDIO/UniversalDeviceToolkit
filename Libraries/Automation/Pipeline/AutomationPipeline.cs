using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline;

public class AutomationPipeline
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string? IconName { get; set; }

    public string? Name { get; set; }

    public IAutomationPipelineTrigger? Trigger { get; set; }

    public List<IAutomationStep> Steps { get; init; } = [];

    public bool IsExclusive { get; set; } = true;

    [JsonIgnore]
    public IEnumerable<IAutomationPipelineTrigger> AllTriggers
    {
        get
        {
            if (Trigger is not null && Trigger is not ICompositeAutomationPipelineTrigger)
                yield return Trigger;

            if (Trigger is ICompositeAutomationPipelineTrigger compositeTrigger)
                foreach (var trigger in compositeTrigger.Triggers)
                    yield return trigger;
        }
    }

    public AutomationPipeline() { }

    public AutomationPipeline(string name) => Name = name;

    public AutomationPipeline(IAutomationPipelineTrigger trigger) => Trigger = trigger;

    internal async Task RunAsync(List<AutomationPipeline> otherPipelines, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipeline interrupted.");
            return;
        }

        var context = new AutomationContext();
        var environment = new AutomationEnvironment();
        var stepExceptions = new List<Exception>();

        AllTriggers.ForEach(t => t.UpdateEnvironment(environment));

        foreach (var step in GetAllSteps(otherPipelines))
        {
            if (token.IsCancellationRequested)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Pipeline interrupted.");
                break;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Running step... [type={step.GetType().Name}]");

            try
            {
                await step.RunAsync(context, environment, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Step run failed. [name={step.GetType().Name}]", ex);

                stepExceptions.Add(ex);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Step completed successfully. [type={step.GetType().Name}]");
        }

        if (stepExceptions.Count != 0)
            throw new AggregateException(stepExceptions);
    }

    private IEnumerable<IAutomationStep> GetAllSteps(List<AutomationPipeline> pipelines)
        => GetAllSteps(pipelines, [Id]);

    private IEnumerable<IAutomationStep> GetAllSteps(List<AutomationPipeline> pipelines, HashSet<Guid> pipelineChain)
    {
        foreach (var step in Steps)
        {
            if (step is QuickActionAutomationStep qas && qas.PipelineId is Guid pipelineId)
            {
                var matchingPipeline = pipelines.FirstOrDefault(p => p.Id == pipelineId && p.AllTriggers.IsEmpty());
                if (matchingPipeline is null)
                {
                    yield return step;
                    continue;
                }

                if (!pipelineChain.Add(matchingPipeline.Id))
                    throw new InvalidOperationException($"Detected a Quick Action cycle while expanding pipeline '{matchingPipeline.Name ?? matchingPipeline.Id.ToString()}'.");

                try
                {
                    foreach (var matchingPipelineStep in matchingPipeline.GetAllSteps(pipelines, pipelineChain))
                        yield return matchingPipelineStep;
                }
                finally
                {
                    _ = pipelineChain.Remove(matchingPipeline.Id);
                }
            }

            yield return step;
        }
    }

    public AutomationPipeline DeepCopy() => new()
    {
        Id = Id,
        IconName = IconName,
        Name = Name,
        Trigger = Trigger?.DeepCopy(),
        Steps = Steps.Select(s => s.DeepCopy()).ToList(),
        IsExclusive = IsExclusive,
    };
}
