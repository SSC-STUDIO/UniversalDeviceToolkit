using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class SpectrumKeyboardBacklightProfileAutomationStep(int state)
    : IAutomationStep<int>
{
    private readonly SpectrumKeyboardBacklightController _controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();

    private readonly int[] _allStates = Enumerable.Range(1, 6).ToArray();

    public int State { get; } = state;

    public Task<int[]> GetAllStatesAsync() => Task.FromResult(_allStates);

    public Task<bool> IsSupportedAsync() => _controller.IsSupportedAsync();

    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        if (!await _controller.IsSupportedAsync().ConfigureAwait(false))
            return;

        if (!_allStates.Contains(State))
            throw new InvalidOperationException(nameof(State));

        await _controller.SetProfileAsync(State).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    public IAutomationStep DeepCopy() => new SpectrumKeyboardBacklightProfileAutomationStep(State);
}
