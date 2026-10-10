using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class SpeakerAutomationStep(SpeakerState state)
    : AbstractFeatureAutomationStep<SpeakerState>(state)
{
    public override IAutomationStep DeepCopy() => new SpeakerAutomationStep(State);
}
