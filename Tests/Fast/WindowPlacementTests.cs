using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class WindowPlacementTests
{
    [Theory]
    [InlineData(96, 1180, 780)]
    [InlineData(144, 1770, 1170)]
    [InlineData(192, 2360, 1560)]
    public void DefaultSize_ScalesWithDisplay(uint dpi, int width, int height)
    {
        var bounds = WindowPlacement.Fit(null, 0, 0, 3840, 2160, dpi);
        Assert.Equal(width, bounds.Width);
        Assert.Equal(height, bounds.Height);
        Assert.Equal((3840 - width) / 2, bounds.Left);
    }

    [Fact]
    public void LegacyDefault_IsMigratedButCustomSizeIsRetained()
    {
        var oldDefault = new WindowPlacement(120, 100, 1180, 780);
        Assert.Equal(1770, WindowPlacement.Fit(oldDefault, 0, 0, 2560, 1560, 144).Width);
        var custom = new WindowPlacement(200, 120, 1500, 1000);
        Assert.Equal(custom with { Dpi = 144 }, WindowPlacement.Fit(custom, 0, 0, 2560, 1560, 144));
    }

    [Fact]
    public void DisconnectedMonitor_BoundsFitSmallerWorkArea()
    {
        var saved = new WindowPlacement(3000, -1000, 2000, 1500, 144);
        var bounds = WindowPlacement.Fit(saved, -1280, 40, 1280, 680, 144);
        Assert.Equal(new WindowPlacement(-1280, 40, 1280, 680, 144), bounds);
    }

    [Fact]
    public void SavedSize_KeepsLogicalSizeWhenDpiChanges()
    {
        var saved = new WindowPlacement(100, 100, 1500, 1000, 144);
        var bounds = WindowPlacement.Fit(saved, 0, 0, 1920, 1040, 96);
        Assert.Equal(1000, bounds.Width);
        Assert.Equal(667, bounds.Height);
    }
}
