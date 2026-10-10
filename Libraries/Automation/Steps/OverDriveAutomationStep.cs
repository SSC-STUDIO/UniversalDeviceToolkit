using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class OverDriveAutomationStep(OverDriveState state)
    : AbstractFeatureAutomationStep<OverDriveState>(state)
{
    public override IAutomationStep DeepCopy() => new OverDriveAutomationStep(State);
}
