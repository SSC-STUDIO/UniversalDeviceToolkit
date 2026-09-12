using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.Network;

public sealed class NetworkAccelerationSettings()
    : AbstractSettings<NetworkAccelerationConfig>(NetworkAccelerationDefaults.SettingsFileName)
{
    protected override NetworkAccelerationConfig Default => NetworkAccelerationConfig.CreateDefault();
}
