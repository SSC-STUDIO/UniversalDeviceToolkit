namespace UniversalDeviceToolkit.Windows;

/// <summary>Design size at 96 DPI and the smallest size the window may keep.</summary>
internal readonly record struct WindowMetrics(int DesignWidth, int DesignHeight, int MinWidth, int MinHeight)
{
    /// <summary>Main application window. Unchanged by the installer dialog.</summary>
    internal static WindowMetrics Application { get; } = new(1180, 780, 800, 600);

    /// <summary>
    /// Setup dialog. Wide enough for the brand column plus language, device,
    /// and feature pages, short of a full application window.
    /// </summary>
    internal static WindowMetrics Installer { get; } = new(760, 520, 760, 500);
}

internal sealed record WindowPlacement(int Left, int Top, int Width, int Height, uint Dpi = 0)
{
    internal static WindowPlacement Fit(WindowPlacement? saved, int left, int top, int width, int height, uint dpi, WindowMetrics metrics = default)
    {
        if (metrics.DesignWidth <= 0 || metrics.DesignHeight <= 0) metrics = WindowMetrics.Application;
        var scale = dpi / 96.0;
        // Earlier versions saved the unscaled default as physical pixels.
        var useDefault = saved == null || (saved.Dpi == 0 && saved.Width == metrics.DesignWidth && saved.Height == metrics.DesignHeight);
        var previous = saved ?? new WindowPlacement(0, 0, metrics.DesignWidth, metrics.DesignHeight);
        var sizeScale = saved is { Dpi: > 0 } ? (double)dpi / saved.Dpi : 1;
        var desiredWidth = useDefault ? metrics.DesignWidth * scale : previous.Width * sizeScale;
        var desiredHeight = useDefault ? metrics.DesignHeight * scale : previous.Height * sizeScale;
        var fittedWidth = Math.Clamp((int)Math.Round(desiredWidth), Math.Min(width, (int)(metrics.MinWidth * scale)), width);
        var fittedHeight = Math.Clamp((int)Math.Round(desiredHeight), Math.Min(height, (int)(metrics.MinHeight * scale)), height);
        return new WindowPlacement(
            useDefault ? left + (width - fittedWidth) / 2 : Math.Clamp(previous.Left, left, left + width - fittedWidth),
            useDefault ? top + (height - fittedHeight) / 2 : Math.Clamp(previous.Top, top, top + height - fittedHeight),
            fittedWidth, fittedHeight, dpi);
    }
}
