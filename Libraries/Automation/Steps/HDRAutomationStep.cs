using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class HDRAutomationStep(HDRState state)
    : AbstractFeatureAutomationStep<HDRState>(state)
{
    public override IAutomationStep DeepCopy() => new HDRAutomationStep(State);
}
