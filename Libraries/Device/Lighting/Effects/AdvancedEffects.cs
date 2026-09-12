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

public class MeteorEffect : BaseLampEffect
{
    public override string Name => Resource.LampEffect_Meteor;
    private readonly Random _random = new();
    public MeteorEffect(Color color, int meteorCount = 5, double speed = 0.5)
    {
        Parameters["Color"] = color;
        Parameters["MeteorCount"] = meteorCount;
        Parameters["Speed"] = speed;
        Parameters["Meteors"] = new List<Meteor>();
    }

    public override void Reset()
    {
        Parameters["Meteors"] = new List<Meteor>();
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var color = (Color)Parameters["Color"];
        var meteorCount = (int)Parameters["MeteorCount"];
        var speed = (double)Parameters["Speed"];
        var meteors = (List<Meteor>)Parameters["Meteors"];

        while (meteors.Count < meteorCount)
        {
            meteors.Add(new Meteor
            {
                StartTime = time + _random.NextDouble() * 2.0,
                StartY = _random.NextDouble() * 0.2,
                Speed = speed * (0.8 + _random.NextDouble() * 0.4)
            });
        }

        double maxIntensity = 0;
        foreach (var meteor in meteors)
        {
            if (time < meteor.StartTime) continue;

            var elapsed = time - meteor.StartTime;
            var meteorX = elapsed * meteor.Speed;

            if (meteorX > 0.6)
            {
                meteor.StartTime = time + _random.NextDouble() * 1.0;
                meteor.StartY = _random.NextDouble() * 0.2;
                continue;
            }

            var dx = lampInfo.Position.X - meteorX;
            var dy = lampInfo.Position.Y - meteor.StartY;

            if (dx is > -0.15 and < 0.05 && Math.Abs(dy) < 0.05)
            {
                var intensity = 0.0;
                
                if (dx < 0)
                {
                    intensity = 1.0 - Math.Abs(dx) / 0.15;
                    intensity *= 1.0 - Math.Abs(dy) / 0.05;
                }
                else
                {
                    intensity = 1.0 - dx / 0.05;
                    intensity *= 1.0 - Math.Abs(dy) / 0.05;
                }
                
                intensity = Math.Clamp(intensity, 0, 1);
                intensity = Math.Pow(intensity, 2.0);
                maxIntensity = Math.Max(maxIntensity, intensity);
            }
        }

        return Color.FromArgb(255,
            (byte)(color.R * maxIntensity),
            (byte)(color.G * maxIntensity),
            (byte)(color.B * maxIntensity));
    }

    private class Meteor
    {
        public double StartTime { get; set; }
        public double StartY { get; set; }
        public double Speed { get; set; }
    }
}

public class RippleEffect : BaseLampEffect
{
    public override string Name => Resource.LampEffect_Ripple;
    private readonly Random _random = new();

    public RippleEffect(Color color, double period = 1.5, int rippleCount = 3)
    {
        Parameters["Color"] = color;
        Parameters["Period"] = period;
        Parameters["RippleCount"] = rippleCount;
        Parameters["Ripples"] = new List<Ripple>();
    }

    public override void Reset()
    {
        Parameters["Ripples"] = new List<Ripple>();
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var color = (Color)Parameters["Color"];
        var period = (double)Parameters["Period"];
        var rippleCount = (int)Parameters["RippleCount"];
        var ripples = (List<Ripple>)Parameters["Ripples"];

        if (ripples.Count == 0 || time - ripples[^1].StartTime > period)
        {
            if (ripples.Count >= rippleCount)
                ripples.RemoveAt(0);

            ripples.Add(new Ripple
            {
                StartTime = time,
                CenterX = _random.NextDouble() * 0.45,
                CenterY = _random.NextDouble() * 0.15
            });
        }

        double maxIntensity = 0;
        foreach (var ripple in ripples)
        {
            var elapsed = time - ripple.StartTime;
            var radius = elapsed * 0.2;

            var dx = lampInfo.Position.X - ripple.CenterX;
            var dy = lampInfo.Position.Y - ripple.CenterY;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            var diff = Math.Abs(distance - radius);
            if (diff < 0.03)
            {
                var intensity = 1.0 - diff / 0.03;
                intensity *= Math.Max(0, 1.0 - elapsed / 2.0);
                maxIntensity = Math.Max(maxIntensity, intensity);
            }
        }

        return Color.FromArgb(255,
            (byte)(color.R * maxIntensity),
            (byte)(color.G * maxIntensity),
            (byte)(color.B * maxIntensity));
    }

    private class Ripple
    {
        public double StartTime { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
    }
}

public class SparkleEffect : BaseLampEffect
{
    public override string Name => Resource.LampEffect_Sparkle;
    private readonly Random _random = new();

    public SparkleEffect(Color color, double density = 0.1)
    {
        Parameters["Color"] = color;
        Parameters["Density"] = density;
        Parameters["Sparkles"] = new Dictionary<int, double>();
    }

    public override void Reset()
    {
        Parameters["Sparkles"] = new Dictionary<int, double>();
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var color = (Color)Parameters["Color"];
        var density = (double)Parameters["Density"];
        var sparkles = (Dictionary<int, double>)Parameters["Sparkles"];

        if (!sparkles.ContainsKey(lampIndex) && _random.NextDouble() < density * 0.01)
        {
            sparkles[lampIndex] = time;
        }

        if (sparkles.TryGetValue(lampIndex, out var startTime))
        {
            var elapsed = time - startTime;
            if (elapsed > 0.5)
            {
                sparkles.Remove(lampIndex);
                return Color.FromArgb(255, 0, 0, 0);
            }

            var intensity = 1.0 - elapsed / 0.5;
            intensity = EaseOut(intensity);

            return Color.FromArgb(255,
                (byte)(color.R * intensity),
                (byte)(color.G * intensity),
                (byte)(color.B * intensity));
        }

        return Color.FromArgb(255, 0, 0, 0);
    }
}

public class GradientEffect : BaseLampEffect
{
    public override string Name => Resource.LampEffect_Gradient;

    public GradientEffect(Color[] colors, GradientDirection direction = GradientDirection.LeftToRight, bool animated = false, double period = 5.0)
    {
        Parameters["Colors"] = colors;
        Parameters["Direction"] = direction;
        Parameters["Animated"] = animated;
        Parameters["Period"] = period;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var colors = (Color[])Parameters["Colors"];
        var direction = (GradientDirection)Parameters["Direction"];
        var animated = (bool)Parameters["Animated"];
        var period = (double)Parameters["Period"];

        if (colors.Length == 0) return Color.FromArgb(255, 0, 0, 0);
        if (colors.Length == 1) return colors[0];

        double position = direction switch
        {
            GradientDirection.LeftToRight => lampInfo.Position.X / 2200.0,
            GradientDirection.RightToLeft => 1.0 - lampInfo.Position.X / 2200.0,
            GradientDirection.TopToBottom => lampInfo.Position.Y / 800.0,
            GradientDirection.BottomToTop => 1.0 - lampInfo.Position.Y / 800.0,
            _ => lampInfo.Position.X / 2200.0
        };

        if (animated)
        {
            position = (position + time / period) % 1.0;
        }

        var segmentSize = 1.0 / (colors.Length - 1);
        var segmentIndex = (int)(position / segmentSize);
        segmentIndex = Math.Clamp(segmentIndex, 0, colors.Length - 2);

        var segmentPosition = (position - segmentIndex * segmentSize) / segmentSize;
        return LerpColor(colors[segmentIndex], colors[segmentIndex + 1], segmentPosition);
    }
}

public enum GradientDirection
{
    LeftToRight,
    RightToLeft,
    TopToBottom,
    BottomToTop
}

public class RainbowWaveEffect : BaseLampEffect
{
    public override string Name => Resource.LampEffect_RainbowWave;

    public RainbowWaveEffect(double speed = 1.0, double scale = 2.0, GradientDirection direction = GradientDirection.LeftToRight)
    {
        Parameters["Speed"] = speed;
        Parameters["Scale"] = scale;
        Parameters["Direction"] = direction;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var speed = (double)Parameters["Speed"];
        var scale = (double)Parameters["Scale"];
        var direction = (GradientDirection)Parameters["Direction"];

        double pos = direction switch
        {
            GradientDirection.LeftToRight => lampInfo.Position.X,
            GradientDirection.RightToLeft => -lampInfo.Position.X,
            GradientDirection.TopToBottom => lampInfo.Position.Y,
            GradientDirection.BottomToTop => -lampInfo.Position.Y,
            _ => lampInfo.Position.X
        };
        
        var hueVal = (-time * speed * 0.2 + pos * scale);
        var hue = (hueVal % 1.0 + 1.0) % 1.0 * 360.0;

        return HsvToRgb(hue, 1.0, 1.0);
    }
}

public class SpiralRainbowEffect : BaseLampEffect
{
    public override string Name => Resource.LampEffect_SpiralRainbow;

    public SpiralRainbowEffect(double speed = 1.0, double spiralDensity = 5.0)
    {
        Parameters["Speed"] = speed;
        Parameters["SpiralDensity"] = spiralDensity;
    }

    public override Color GetColorForLamp(int lampIndex, double time, LampInfo lampInfo, int totalLamps)
    {
        var speed = (double)Parameters["Speed"];
        var density = (double)Parameters["SpiralDensity"];

        double cx = 0.225; 
        double cy = 0.100;

        double dx = lampInfo.Position.X - cx;
        double dy = lampInfo.Position.Y - cy;

        double angle = Math.Atan2(dy, dx) / (2 * Math.PI);
        double dist = Math.Sqrt(dx * dx + dy * dy);

        double hueVal = angle + time * speed * 0.2 + dist * density * 2.0;
        double hue = (hueVal % 1.0 + 1.0) % 1.0 * 360.0;

        return HsvToRgb(hue, 1.0, 1.0);
    }
}
