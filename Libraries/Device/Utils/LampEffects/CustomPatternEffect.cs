// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Collections.Generic;
using Windows.Devices.Lights;
using Windows.UI;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib.Utils.LampEffects;

public class CustomPatternEffect : ILampEffect
{
    private readonly Dictionary<int, Color> _colors = new();

    public string Name => Resource.LampEffect_CustomPattern;
    public Dictionary<string, object> Parameters { get; } = new();

    public Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        return _colors.TryGetValue(lampIndex, out var color) ? color : Color.FromArgb(0, 0, 0, 0);
    }

    public void Reset()
    {
    }

    public void SetColor(int index, Color color)
    {
        _colors[index] = color;
    }
    
    public void Clear()
    {
        _colors.Clear();
    }
}
