using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class WinKeyAutomationStep(WinKeyState state)
    : AbstractFeatureAutomationStep<WinKeyState>(state)
{
    public override IAutomationStep DeepCopy() => new WinKeyAutomationStep(State);
}
