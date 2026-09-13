namespace UniversalDeviceToolkit.Windows;

internal sealed record WindowPlacement(int Left, int Top, int Width, int Height, uint Dpi = 0)
{
    internal static WindowPlacement Fit(WindowPlacement? saved, int left, int top, int width, int height, uint dpi)
    {
        var scale = dpi / 96.0;
        // Earlier versions saved the unscaled default as physical pixels.
        var useDefault = saved == null || (saved.Dpi == 0 && saved.Width == 1180 && saved.Height == 780);
        var previous = saved ?? new WindowPlacement(0, 0, 1180, 780);
        var sizeScale = saved is { Dpi: > 0 } ? (double)dpi / saved.Dpi : 1;
        var desiredWidth = useDefault ? 1180 * scale : previous.Width * sizeScale;
        var desiredHeight = useDefault ? 780 * scale : previous.Height * sizeScale;
        var fittedWidth = Math.Clamp((int)Math.Round(desiredWidth), Math.Min(width, (int)(800 * scale)), width);
        var fittedHeight = Math.Clamp((int)Math.Round(desiredHeight), Math.Min(height, (int)(600 * scale)), height);
        return new WindowPlacement(
            useDefault ? left + (width - fittedWidth) / 2 : Math.Clamp(previous.Left, left, left + width - fittedWidth),
            useDefault ? top + (height - fittedHeight) / 2 : Math.Clamp(previous.Top, top, top + height - fittedHeight),
            fittedWidth, fittedHeight, dpi);
    }
}
