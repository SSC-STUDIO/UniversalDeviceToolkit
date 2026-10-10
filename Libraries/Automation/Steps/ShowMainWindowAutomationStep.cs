using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Messaging.Messages;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public class ShowMainWindowAutomationStep : IAutomationStep
{
    [JsonConstructor]
    public ShowMainWindowAutomationStep() { }

    public Task<bool> IsSupportedAsync() => Task.FromResult(AutomationWindowVisibility.IsBridged);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        AutomationWindowVisibility.Request(MainWindowVisibilityAction.Show);
        return Task.CompletedTask;
    }

    IAutomationStep IAutomationStep.DeepCopy() => new ShowMainWindowAutomationStep();
}
