using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class WhiteKeyboardBacklightAutomationStep(WhiteKeyboardBacklightState state)
    : AbstractFeatureAutomationStep<WhiteKeyboardBacklightState>(state)
{
    public override IAutomationStep DeepCopy() => new WhiteKeyboardBacklightAutomationStep(State);
}
