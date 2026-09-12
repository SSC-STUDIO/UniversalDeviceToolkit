using UniversalDeviceToolkit.Lib.Automation.Resources;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation;

[method: JsonConstructor]
public readonly struct Delay(int delaySeconds) : IDisplayName
{
    public int DelaySeconds { get; } = delaySeconds;

    public string DisplayName => string.Format(DelaySeconds == 1 ? Resource.Delay_Second : Resource.Delay_Second_Many, DelaySeconds);
}
