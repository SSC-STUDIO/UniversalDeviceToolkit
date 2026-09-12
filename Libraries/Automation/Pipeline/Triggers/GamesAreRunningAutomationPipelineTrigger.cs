using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Automation.Resources;

namespace UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;

public class GamesAreRunningAutomationPipelineTrigger : IGameAutomationPipelineTrigger
{
    public string DisplayName => Resource.GamesAreRunningAutomationPipelineTrigger_DisplayName;

    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is GameAutomationEvent { Running: true };
        return Task.FromResult(result);
    }

    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<GameAutoListener>();
        var result = listener.AreGamesRunning();
        return Task.FromResult(result);
    }

    public void UpdateEnvironment(AutomationEnvironment environment) => environment.GameRunning = true;

    public IAutomationPipelineTrigger DeepCopy() => new GamesAreRunningAutomationPipelineTrigger();
}
