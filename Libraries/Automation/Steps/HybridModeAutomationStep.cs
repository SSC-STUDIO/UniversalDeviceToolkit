using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class HybridModeAutomationStep(HybridModeState state)
    : AbstractFeatureAutomationStep<HybridModeState>(state)
{
    public override IAutomationStep DeepCopy() => new HybridModeAutomationStep(State);
}
