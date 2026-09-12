using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.Features.Hybrid;

public class IGPUModeGamezoneFeature()
    : AbstractWmiFeature<IGPUModeState>(WMI.LenovoGameZoneData.GetIGPUModeStatusAsync, WMI.LenovoGameZoneData.SetIGPUModeStatusAsync, WMI.LenovoGameZoneData.IsSupportIGPUModeAsync);
