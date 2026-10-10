using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Trait("Category", TestCategories.Unit)]
public class OsdSettingsStoreTests
{
    [Fact]
    public void Defaults_ShowOsd_ShouldBeFalse()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.ShowOsd.Should().BeFalse();
    }

    [Fact]
    public void Defaults_OsdRefreshInterval_ShouldBeOne()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.OsdRefreshInterval.Should().Be(1);
    }

    [Fact]
    public void Defaults_SelectedStyleIndex_ShouldBeZero()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.SelectedStyleIndex.Should().Be(0);
    }

    [Fact]
    public void Defaults_Items_ShouldContainAllOsdItems()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.Items.Should().NotBeNull();
        store.Items.Should().HaveCount(Enum.GetValues<OsdItem>().Length);
    }

    [Fact]
    public void Defaults_BackgroundOpacity_ShouldBe0Point6()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.BackgroundColor.Should().Be("#1E1E1E");
        store.BackgroundOpacity.Should().Be(0.6);
    }

    [Fact]
    public void Defaults_FontSize_ShouldBe12()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.FontSize.Should().Be(12);
    }

    [Fact]
    public void Defaults_CornerRadius_ShouldBe6()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.CornerRadiusTop.Should().Be(6);
        store.CornerRadiusBottom.Should().Be(6);
    }

    [Fact]
    public void Defaults_IsLocked_ShouldBeFalse()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Defaults_Positions_ShouldBeNull()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.PanelPositionX.Should().BeNull();
        store.PanelPositionY.Should().BeNull();
        store.BarPositionX.Should().BeNull();
        store.BarPositionY.Should().BeNull();
    }

    [Fact]
    public void Defaults_Thresholds_ShouldHaveReasonableValues()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.TempThresholdWarning.Should().Be(75);
        store.TempThresholdCritical.Should().Be(90);
        store.UsageThresholdWarning.Should().Be(70);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.ShowOsd = true;
        store.ShowOsd.Should().BeTrue();
        store.FontSize = 16;
        store.FontSize.Should().Be(16);
        store.IsLocked = true;
        store.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Normalize_Colors_ShouldCanonicalizeOrUseDefaults()
    {
        var store = new OsdSettings.OsdSettingsStore
        {
            BackgroundColor = "</style><script>alert(1)</script>",
            CategoryColor = "#a1b2c3",
            LabelColor = "#12345G",
            ValueColor = "white",
            WarningColor = "#123",
            CriticalColor = string.Empty,
            SeparatorColor = "#A0b1C2"
        };

        OsdSettings.Normalize(store).Should().BeSameAs(store);

        store.BackgroundColor.Should().Be("#1E1E1E");
        store.CategoryColor.Should().Be("#A1B2C3");
        store.LabelColor.Should().Be("#ADFF2F");
        store.ValueColor.Should().Be("#FFFFFF");
        store.WarningColor.Should().Be("#FFFF00");
        store.CriticalColor.Should().Be("#FF0000");
        store.SeparatorColor.Should().Be("#A0B1C2");
    }

    [Fact]
    public void Normalize_NumericSettings_ShouldClampAndRejectNonFiniteValues()
    {
        var store = new OsdSettings.OsdSettingsStore
        {
            OsdRefreshInterval = double.NaN,
            SelectedStyleIndex = 42,
            BackgroundOpacity = double.PositiveInfinity,
            FontSize = int.MaxValue,
            CornerRadiusTop = int.MinValue,
            CornerRadiusBottom = int.MaxValue,
            PanelPositionX = double.NaN,
            PanelPositionY = 250_000,
            BarPositionX = -250_000,
            BarPositionY = 42.5,
            TempThresholdWarning = -1,
            TempThresholdCritical = int.MaxValue,
            UsageThresholdWarning = -1,
            UsageThresholdCritical = int.MaxValue,
            FpsThresholdCritical = -1,
            LowFpsDeltaThreshold = int.MaxValue,
            SnapThreshold = int.MaxValue
        };

        OsdSettings.Normalize(store).Should().BeSameAs(store);

        store.OsdRefreshInterval.Should().Be(1);
        store.SelectedStyleIndex.Should().Be(0);
        store.BackgroundOpacity.Should().Be(0.6);
        store.FontSize.Should().Be(24);
        store.CornerRadiusTop.Should().Be(0);
        store.CornerRadiusBottom.Should().Be(50);
        store.PanelPositionX.Should().BeNull();
        store.PanelPositionY.Should().Be(100_000);
        store.BarPositionX.Should().Be(-100_000);
        store.BarPositionY.Should().Be(42.5);
        store.TempThresholdWarning.Should().Be(0);
        store.TempThresholdCritical.Should().Be(110);
        store.UsageThresholdWarning.Should().Be(0);
        store.UsageThresholdCritical.Should().Be(100);
        store.FpsThresholdCritical.Should().Be(0);
        store.LowFpsDeltaThreshold.Should().Be(1000);
        store.SnapThreshold.Should().Be(100);
    }
}
