namespace UniversalDeviceToolkit.Lib.DeviceSupport;

public interface IInstalledDeviceSupportProvider
{
    void SetInstalledCatalog(DeviceSupportCatalog? catalog);

    /// <summary>
    /// User-confirmed pack from first-run device setup. When set, Evaluate prefers this pack
    /// over auto-detect so manual corrections stick across launches.
    /// </summary>
    void SetPreferredDevicePackId(string? packId);
}
