// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Collections.Generic;
using Windows.Devices.Lights;
using Windows.UI;

namespace UniversalDeviceToolkit.Lib.Utils.LampEffects;

public interface ILampEffect
{
    string Name { get; }
    Dictionary<string, object> Parameters { get; }

    Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps);
    void Reset();
}
