using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib;

public readonly struct WindowSize(double width, double height)
{
    public double Width { get; } = width;
    public double Height { get; } = height;
}

/// <summary>
/// Persisted main-window restore bounds (WPF DIPs) plus whether the window was maximized.
/// Minimized is intentionally never persisted — only normal bounds and the maximized flag.
/// </summary>
public readonly struct WindowPlacement(double left, double top, double width, double height, bool isMaximized)
{
    /// <summary>Gets the left edge position of the window in device-independent pixels.</summary>
    public double Left { get; } = left;
    /// <summary>Gets the top edge position of the window in device-independent pixels.</summary>
    public double Top { get; } = top;
    /// <summary>Gets the width of the window in device-independent pixels.</summary>
    public double Width { get; } = width;
    /// <summary>Gets the height of the window in device-independent pixels.</summary>
    public double Height { get; } = height;
    /// <summary>Gets a value indicating whether the window was maximized when the placement was captured.</summary>
    public bool IsMaximized { get; } = isMaximized;
}
