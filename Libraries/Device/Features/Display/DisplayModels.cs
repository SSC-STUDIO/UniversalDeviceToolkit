using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Drawing;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib;

public readonly struct Brightness(byte value)
{
    public byte Value { get; } = value;
}

public readonly struct DisplayAdvancedColorInfo(bool advancedColorSupported, bool advancedColorEnabled, bool wideColorEnforced, bool advancedColorForceDisabled)
{
    public bool AdvancedColorSupported { get; } = advancedColorSupported;
    public bool AdvancedColorEnabled { get; } = advancedColorEnabled;
    public bool WideColorEnforced { get; } = wideColorEnforced;
    public bool AdvancedColorForceDisabled { get; } = advancedColorForceDisabled;
}

[method: JsonConstructor]
public readonly struct DpiScale(int scale) : IDisplayName, IEquatable<DpiScale>
{
    public int Scale { get; } = scale;

    [JsonIgnore]
    public string DisplayName => string.Format(Resource.DpiScale_DisplayName_Format, Scale);

    #region Equality

    public override bool Equals(object? obj) => obj is DpiScale rate && Equals(rate);

    public bool Equals(DpiScale other) => Scale == other.Scale;

    public override int GetHashCode() => HashCode.Combine(Scale);

    public static bool operator ==(DpiScale left, DpiScale right) => left.Equals(right);

    public static bool operator !=(DpiScale left, DpiScale right) => !(left == right);

    #endregion
}

[method: JsonConstructor]
public readonly struct RefreshRate(int frequency) : IDisplayName, IEquatable<RefreshRate>
{
    public int Frequency { get; } = frequency;

    [JsonIgnore]
    public string DisplayName => string.Format(Resource.RefreshRate_DisplayName_Format, Frequency);

    public override string ToString() => $"{Frequency}Hz";

    #region Equality

    public override bool Equals(object? obj) => obj is RefreshRate rate && Equals(rate);

    public bool Equals(RefreshRate other) => Frequency == other.Frequency;

    public override int GetHashCode() => HashCode.Combine(Frequency);

    public static bool operator ==(RefreshRate left, RefreshRate right) => left.Equals(right);

    public static bool operator !=(RefreshRate left, RefreshRate right) => !(left == right);

    #endregion
}

[method: JsonConstructor]
public readonly struct Resolution(int width, int height) : IDisplayName, IEquatable<Resolution>, IComparable<Resolution>
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    [JsonIgnore]
    public string DisplayName => string.Format(Resource.Resolution_DisplayName_Format, Width, Height);

    public Resolution(Size size) : this(size.Width, size.Height) { }

    public override string ToString() => $"{Width}x{Height}";

    public int CompareTo(Resolution other)
    {
        var widthComparison = Width.CompareTo(other.Width);
        return widthComparison != 0
            ? widthComparison
            : Height.CompareTo(other.Height);
    }

    #region Conversion

    public static explicit operator Resolution(Size value) => new(value);

    public static implicit operator Size(Resolution data) => new(data.Width, data.Height);

    #endregion

    #region Equality

    public override bool Equals(object? obj) => obj is Resolution other && Equals(other);

    public bool Equals(Resolution other) => Width == other.Width && Height == other.Height;

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(Resolution left, Resolution right) => left.Equals(right);

    public static bool operator !=(Resolution left, Resolution right) => !(left == right);

    #endregion

}
