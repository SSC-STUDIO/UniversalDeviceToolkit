using System;
using WindowsDisplayAPI;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class DisplayPossibleSettingExtensions
{
    public static bool IsTooSmall(this DisplayPossibleSetting dps) => Math.Max(dps.Resolution.Width, dps.Resolution.Height) < 1000;
}
