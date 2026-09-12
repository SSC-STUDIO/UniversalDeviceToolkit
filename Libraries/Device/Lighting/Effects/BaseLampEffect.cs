// Derived from Lenovo Legion Toolkit.
// Original project copyright: Copyright (C) Bartosz Cichecki and contributors.
// Upstream sync copyright: Copyright (C) 2026 UniversalDeviceToolkit-Team.
// Modifications copyright: Copyright (C) 2026 Universal Device Toolkit Contributors.

using System;
using System.Collections.Generic;
using Windows.Devices.Lights;
using Windows.UI;

namespace UniversalDeviceToolkit.Lib.Utils.LampEffects;

public abstract class BaseLampEffect : ILampEffect
{
    public abstract string Name { get; }
    public Dictionary<string, object> Parameters { get; } = new();

    public abstract Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps);

    public virtual void Reset() { }

    protected static Color LerpColor(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t)
        );
    }
    protected static Color HsvToRgb(double h, double s, double v)
    {
        h = h % 360;
        if (h < 0) h += 360;

        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromArgb(255,
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    protected static double EaseInOut(double t)
    {
        return t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    }

    protected static double EaseIn(double t)
    {
        return t * t;
    }

    protected static double EaseOut(double t)
    {
        return 1 - (1 - t) * (1 - t);
    }

    protected static double SineWave(double t)
    {
        return (Math.Sin(t * Math.PI * 2) + 1) / 2;
    }
}
