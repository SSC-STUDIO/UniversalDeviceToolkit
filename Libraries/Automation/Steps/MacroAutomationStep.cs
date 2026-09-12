using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Macro;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public class MacroAutomationStep(MacroAutomationStepState state) : IAutomationStep<MacroAutomationStepState>
{
    private readonly IMacroController _controller = IoCContainer.Resolve<IMacroController>();

    public MacroAutomationStepState State { get; set; } = state;

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<MacroAutomationStepState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<MacroAutomationStepState>());

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        _controller.SetEnabled(State is MacroAutomationStepState.On);
        return Task.CompletedTask;
    }

    public IAutomationStep DeepCopy() => new MacroAutomationStep(State);

}
