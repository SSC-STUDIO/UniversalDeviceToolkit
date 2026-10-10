using System;
using System.Linq;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class PowerModeAutomationStep(PowerModeState state)
    : AbstractFeatureAutomationStep<PowerModeState>(state)
{
    public override async Task<bool> IsSupportedAsync()
    {
        if (!await base.IsSupportedAsync().ConfigureAwait(false))
            return false;

        try
        {
            var (_, machineInformation) = await Compatibility.IsCompatibleAsync().ConfigureAwait(false);
            var isSupportedLegionMachine = Compatibility.IsSupportedLegionMachine(machineInformation);
            
            if (!isSupportedLegionMachine)
                return false;

            // For backward compatibility, when SupportedPowerModes is null, allow Quiet, Balance, and Performance
            if (machineInformation.SupportedPowerModes is null || machineInformation.SupportedPowerModes.Length == 0)
            {
                // Quiet, Balance, and Performance are always available when SupportedPowerModes is null (backward compatibility)
                // GodMode requires explicit support check
                return State is PowerModeState.Quiet or PowerModeState.Balance or PowerModeState.Performance;
            }

            return machineInformation.SupportedPowerModes.Contains(State);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Exception occurred while checking PowerModeAutomationStep support.", ex);
            return false;
        }
    }

    public override IAutomationStep DeepCopy() => new PowerModeAutomationStep(State);
}
