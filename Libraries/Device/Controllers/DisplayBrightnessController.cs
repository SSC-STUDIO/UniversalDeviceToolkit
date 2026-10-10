using System;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.System.Management;

namespace UniversalDeviceToolkit.Lib.Controllers;

public class DisplayBrightnessController
{
    public Task SetBrightnessAsync(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        return WMI.WmiMonitorBrightnessMethods.WmiSetBrightness(brightness, 1);
    }
}
