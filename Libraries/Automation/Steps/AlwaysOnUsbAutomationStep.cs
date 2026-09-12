using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class AlwaysOnUsbAutomationStep(AlwaysOnUSBState state)
    : AbstractFeatureAutomationStep<AlwaysOnUSBState>(state)
{
    public override IAutomationStep DeepCopy() => new AlwaysOnUsbAutomationStep(State);
}
