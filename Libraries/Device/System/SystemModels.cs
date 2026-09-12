using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace UniversalDeviceToolkit.Lib;

[method: JsonConstructor]
public readonly struct ProcessInfo(string name, string? executablePath) : IComparable
{
    public static ProcessInfo FromPath(string path) => new(Path.GetFileNameWithoutExtension(path), path);

    public string Name { get; } = name;

    public string? ExecutablePath { get; } = executablePath;

    public override string ToString() => $"{nameof(Name)}: {Name}, {nameof(ExecutablePath)}: {ExecutablePath}";

    #region Equality

    public int CompareTo(object? obj)
    {
        var other = obj is null ? default : (ProcessInfo)obj;
        var result = string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        return result != 0 ? result : string.Compare(ExecutablePath, other.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is ProcessInfo info && Name == info.Name && ExecutablePath == info.ExecutablePath;

    public override int GetHashCode() => HashCode.Combine(Name, ExecutablePath);

    public static bool operator ==(ProcessInfo left, ProcessInfo right) => left.Equals(right);

    public static bool operator !=(ProcessInfo left, ProcessInfo right) => !(left == right);

    public static bool operator <(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) < 0;

    public static bool operator <=(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) <= 0;

    public static bool operator >(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) > 0;

    public static bool operator >=(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) >= 0;

    #endregion
}

public readonly struct ShutdownInfo
{
    public string Status { get; init; }
    public int AbnormalCount { get; init; }
}
