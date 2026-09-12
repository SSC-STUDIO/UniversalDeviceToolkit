using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Automation.Resources;
using UniversalDeviceToolkit.Lib.Extensions;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

[method: JsonConstructor]
public class ProcessesStopRunningAutomationPipelineTrigger(ProcessInfo[]? processes)
    : IProcessesAutomationPipelineTrigger
{
    public string DisplayName => Resource.ProcessesStopRunningAutomationPipelineTrigger_DisplayName;

    public ProcessInfo[] Processes { get; } = processes ?? [];

    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not ProcessAutomationEvent { Type: ProcessEventInfoType.Stopped } e)
            return false;

        if (!Processes.Contains(e.ProcessInfo) && !Processes.Select(p => p.Name).Contains(e.ProcessInfo.Name))
            return false;

        return await Task.Run(() => Processes.SelectMany(p => Process.GetProcessesByName(p.Name)).IsEmpty()).ConfigureAwait(false);
    }

    public async Task<bool> IsMatchingState()
    {
        return await Task.Run(() => Processes.SelectMany(p => Process.GetProcessesByName(p.Name)).IsEmpty()).ConfigureAwait(false);
    }

    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.ProcessesStarted = false;
        environment.Processes = Processes;
    }

    public IAutomationPipelineTrigger DeepCopy() => new ProcessesStopRunningAutomationPipelineTrigger(Processes);

    public IProcessesAutomationPipelineTrigger DeepCopy(ProcessInfo[] processes) => new ProcessesStopRunningAutomationPipelineTrigger(processes);

    public override bool Equals(object? obj)
    {
        return obj is ProcessesStopRunningAutomationPipelineTrigger t && Processes.SequenceEqual(t.Processes);
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        Processes.ForEach(p => hc.Add(p));
        return hc.ToHashCode();
    }

    public override string ToString() => $"{nameof(Processes)}: {string.Join(", ", Processes)}";
}
