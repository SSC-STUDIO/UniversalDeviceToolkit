using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Drawing;
using System.Text.Json.Serialization;
using Windows.Devices.Lights;

namespace UniversalDeviceToolkit.Lib;

[method: JsonConstructor]
public readonly struct RGBColor(byte r, byte g, byte b)
{
    public static readonly RGBColor Green = new(142, 255, 0);
    public static readonly RGBColor Pink = new(186, 0, 255);
    public static readonly RGBColor Purple = new(101, 0, 255);
    public static readonly RGBColor Red = new(255, 0, 0);
    public static readonly RGBColor Teal = new(0, 212, 255);
    public static readonly RGBColor White = new(255, 255, 255);

    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;

    #region Equality

    public override bool Equals(object? obj)
    {
        return obj is RGBColor color && R == color.R && G == color.G && B == color.B;
    }

    public override int GetHashCode() => (R, G, B).GetHashCode();

    public static bool operator ==(RGBColor left, RGBColor right) => left.Equals(right);

    public static bool operator !=(RGBColor left, RGBColor right) => !left.Equals(right);

    #endregion

    public override string ToString() => $"{nameof(R)}: {R}, {nameof(G)}: {G}, {nameof(B)}: {B}";
}

[method: JsonConstructor]
public readonly struct RGBKeyboardBacklightBacklightPresetDescription(
    RGBKeyboardBacklightEffect effect,
    RGBKeyboardBacklightSpeed speed,
    RGBKeyboardBacklightBrightness brightness,
    RGBColor zone1,
    RGBColor zone2,
    RGBColor zone3,
    RGBColor zone4)
{
    public static readonly RGBKeyboardBacklightBacklightPresetDescription Default = new(RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.High, RGBColor.White, RGBColor.White, RGBColor.White, RGBColor.White);

    public RGBKeyboardBacklightEffect Effect { get; } = effect;
    public RGBKeyboardBacklightSpeed Speed { get; } = speed;
    public RGBKeyboardBacklightBrightness Brightness { get; } = brightness;
    public RGBColor Zone1 { get; } = zone1;
    public RGBColor Zone2 { get; } = zone2;
    public RGBColor Zone3 { get; } = zone3;
    public RGBColor Zone4 { get; } = zone4;

    #region Equality

    public override bool Equals(object? obj)
    {
        return obj is RGBKeyboardBacklightBacklightPresetDescription settings &&
               Effect == settings.Effect &&
               Speed == settings.Speed &&
               Brightness == settings.Brightness &&
               Zone1.Equals(settings.Zone1) &&
               Zone2.Equals(settings.Zone2) &&
               Zone3.Equals(settings.Zone3) &&
               Zone4.Equals(settings.Zone4);
    }

    public override int GetHashCode() => HashCode.Combine(Effect, Speed, Brightness, Zone1, Zone2, Zone3, Zone4);

    public static bool operator ==(RGBKeyboardBacklightBacklightPresetDescription left, RGBKeyboardBacklightBacklightPresetDescription right) => left.Equals(right);

    public static bool operator !=(RGBKeyboardBacklightBacklightPresetDescription left, RGBKeyboardBacklightBacklightPresetDescription right) => !(left == right);

    #endregion

    public override string ToString() =>
        $"{nameof(Effect)}: {Effect}," +
        $" {nameof(Speed)}: {Speed}," +
        $" {nameof(Brightness)}: {Brightness}," +
        $" {nameof(Zone1)}: {Zone1}," +
        $" {nameof(Zone2)}: {Zone2}," +
        $" {nameof(Zone3)}: {Zone3}," +
        $" {nameof(Zone4)}: {Zone4}";
}

[method: JsonConstructor]
public readonly struct RGBKeyboardBacklightState(
    RGBKeyboardBacklightPreset selectedPreset,
    Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription> presets)
{
    public RGBKeyboardBacklightPreset SelectedPreset { get; } = selectedPreset;
    public Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription> Presets { get; } = presets;
}

public readonly struct SpectrumKeyboardBacklightEffect(
    SpectrumKeyboardBacklightEffectType type,
    SpectrumKeyboardBacklightSpeed speed,
    SpectrumKeyboardBacklightDirection direction,
    SpectrumKeyboardBacklightClockwiseDirection clockwiseDirection,
    RGBColor[] colors,
    ushort[] keys)
{
    public SpectrumKeyboardBacklightEffectType Type { get; } = type;
    public SpectrumKeyboardBacklightSpeed Speed { get; } = speed;
    public SpectrumKeyboardBacklightDirection Direction { get; } = direction;
    public SpectrumKeyboardBacklightClockwiseDirection ClockwiseDirection { get; } = clockwiseDirection;
    public RGBColor[] Colors { get; } = colors;
    public ushort[] Keys { get; } = type.IsAllLightsEffect() ? [] : keys;
}

public readonly struct KeyMap(int width, int height, ushort[,] keyCodes, ushort[] additionalKeyCodes)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public ushort[,] KeyCodes { get; } = keyCodes;
    public ushort[] AdditionalKeyCodes { get; } = additionalKeyCodes;
}

public readonly struct LampArrayInfo(string id, string displayName, LampArray lampArray)
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
    public LampArray LampArray { get; } = lampArray;
}
