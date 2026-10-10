using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

public class GSyncFeature()
    : AbstractWmiFeature<GSyncState>(WMI.LenovoGameZoneData.GetGSyncStatusAsync, WMI.LenovoGameZoneData.SetGSyncStatusAsync, WMI.LenovoGameZoneData.IsSupportGSyncAsync),
      IGSyncFeature
{
}
