using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

public class TurnOnWiFiAutomationStep : IAutomationStep
{
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        WiFi.TurnOn();
        return Task.CompletedTask;
    }

    public IAutomationStep DeepCopy() => new TurnOnWiFiAutomationStep();
}
