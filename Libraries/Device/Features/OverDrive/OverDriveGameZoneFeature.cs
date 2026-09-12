using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.Features.OverDrive;

public class OverDriveGameZoneFeature()
    : AbstractWmiFeature<OverDriveState>(WMI.LenovoGameZoneData.GetODStatusAsync, WMI.LenovoGameZoneData.SetODStatusAsync, WMI.LenovoGameZoneData.IsSupportODAsync);
