using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class WindowPlacementTests
{
    [Fact]
    public void OverlayAfterMonitorMove_UsesCurrentAnchorAndDpiInsteadOfStaleSavedPosition()
    {
        var current = new WindowPlacement(2200, 180, 320, 96, 96);
        var work = new WindowPlacement(1920, 0, 2560, 1400);
        var placement = new OverlayWindowPlacement(320, 96, 20, 120, 100, false);

        Assert.Equal((2200, 180), placement.GetAnchor(current, preservePosition: true));
        Assert.Equal(new WindowPlacement(2200, 180, 480, 144, 144),
            placement.Fit(current, work, 144, preservePosition: true));
        Assert.Equal((120, 100), placement.GetAnchor(current, preservePosition: false));
    }

    [Fact]
    public void OverlayContentResizeWhileSavingPosition_KeepsCurrentMonitorAndSnapsToItsEdges()
    {
        var current = new WindowPlacement(-1270, 52, 320, 96);
        var work = new WindowPlacement(-1280, 40, 1280, 980);
        var placement = new OverlayWindowPlacement(920, 54, 20, 120, 100, true);

        Assert.Equal((-1270, 52), placement.GetAnchor(current, preservePosition: true));
        Assert.Equal(new WindowPlacement(-1280, 40, 1150, 68, 120),
            placement.Fit(current, work, 120, preservePosition: true));
    }

    [Fact]
    public void OverlayRestore_KeepsLayoutDefaultsAndClampsDisconnectedMonitorCoordinates()
    {
        var work = new WindowPlacement(0, 40, 1920, 1000);
        var current = new WindowPlacement(100, 100, 320, 96);
        var placement = new OverlayWindowPlacement(920, 54, 20, null, null, true);

        Assert.Equal(new WindowPlacement(500, 40, 920, 54, 96), placement.Fit(current, work, 96, preservePosition: false));
        placement = placement with { Left = 3000, Top = -1000 };
        Assert.Equal(new WindowPlacement(1000, 40, 920, 54, 96), placement.Fit(current, work, 96, preservePosition: false));
    }

    [Fact]
    public void OverlayBounds_RestoreSmallLayoutsOnAnotherMonitor()
    {
        var metrics = new WindowMetrics(320, 96, 24, 24);
        var saved = new WindowPlacement(2200, 100, 320, 96, 96);
        Assert.True(WindowPlacement.IsRestorable(saved, metrics, overlay: true));
        Assert.False(WindowPlacement.IsRestorable(saved, metrics));
        Assert.Equal(saved, WindowPlacement.Fit(saved, 1920, 0, 1920, 1040, 96, metrics));
    }

    [Theory]
    [InlineData(24, 24, true)]
    [InlineData(23, 24, false)]
    [InlineData(24, 23, false)]
    public void OverlayBounds_RespectTheirOwnMinimumSize(int width, int height, bool expected)
    {
        Assert.Equal(expected, WindowPlacement.IsRestorable(new WindowPlacement(0, 0, width, height),
            new WindowMetrics(320, 96, 24, 24), overlay: true));
    }

    [Fact]
    public void ApplicationBounds_KeepTheirPreviousRestoreThreshold()
    {
        Assert.False(WindowPlacement.IsRestorable(null));
        Assert.True(WindowPlacement.IsRestorable(new WindowPlacement(0, 0, 640, 480)));
        Assert.False(WindowPlacement.IsRestorable(new WindowPlacement(0, 0, 639, 480)));
        Assert.False(WindowPlacement.IsRestorable(new WindowPlacement(0, 0, 640, 479)));
    }

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

    [Theory]
    [InlineData(96, 760, 520)]
    [InlineData(144, 1140, 780)]
    [InlineData(192, 1520, 1040)]
    public void InstallerDialog_IsSmallerThanTheApplicationWindow(uint dpi, int width, int height)
    {
        var bounds = WindowPlacement.Fit(null, 0, 0, 3840, 2160, dpi, WindowMetrics.Installer);
        Assert.Equal(width, bounds.Width);
        Assert.Equal(height, bounds.Height);
        Assert.True(bounds.Width < WindowPlacement.Fit(null, 0, 0, 3840, 2160, dpi).Width);
        Assert.True(bounds.Height < WindowPlacement.Fit(null, 0, 0, 3840, 2160, dpi).Height);
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
