using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.Features;

public class TouchpadLockFeature()
    : AbstractWmiFeature<TouchpadLockState>(WMI.LenovoGameZoneData.GetTPStatusStatusAsync, WMI.LenovoGameZoneData.SetTPStatusAsync, WMI.LenovoGameZoneData.IsSupportDisableTPAsync);
