using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class InstantBootAutomationStep(InstantBootState state)
    : AbstractFeatureAutomationStep<InstantBootState>(state)
{
    public override IAutomationStep DeepCopy() => new InstantBootAutomationStep(State);
}
