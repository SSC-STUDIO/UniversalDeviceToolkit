using System;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Controllers.GodMode;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Utils;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class GodModePresetAutomationStep(Guid presetId)
    : IAutomationStep
{
    private readonly PowerModeFeature _feature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly GodModeController _controller = IoCContainer.Resolve<GodModeController>();

    public Guid PresetId { get; } = presetId;

    public async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return mi.Properties.SupportsGodMode;
    }

    public Task<GodModeState> GetStateAsync() => _controller.GetStateAsync();

    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        var state = await _controller.GetStateAsync().ConfigureAwait(false);
        if (!state.Presets.ContainsKey(PresetId))
            return;

        var newState = state with { ActivePresetId = PresetId };

        await _controller.SetStateAsync(newState).ConfigureAwait(false);

        if (await _feature.GetStateAsync().ConfigureAwait(false) == PowerModeState.GodMode)
            await _controller.ApplyStateAsync().ConfigureAwait(false);
    }

    public IAutomationStep DeepCopy() => new GodModePresetAutomationStep(PresetId);
}
