using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class DisplayBrightnessAutomationStep(int brightness)
    : IAutomationStep
{
    private readonly Lazy<DisplayBrightnessController> _controllerLazy = new(AutomationServiceResolver.ResolveRequired<DisplayBrightnessController>);
    private DisplayBrightnessController _controller => _controllerLazy.Value;
    public int Brightness { get; } = brightness;

    public Task<bool> IsSupportedAsync() => Task.FromResult(IoCContainer.TryResolve<DisplayBrightnessController>() is not null);

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        return _controller.SetBrightnessAsync(Brightness);
    }

    IAutomationStep IAutomationStep.DeepCopy() => new DisplayBrightnessAutomationStep(Brightness);
}
