using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Trait("Category", TestCategories.Unit)]
public class SettingsStoreModelsTests
{
    #region ApplicationSettingsStore Tests

    [Fact]
    public void ApplicationSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.Theme.Should().Be(default(Theme));
        store.ThemeStylePreset.Should().Be(ThemeStylePreset.Default);
        store.AccentColorSource.Should().Be(default(AccentColorSource));
        store.WindowBackdropStyle.Should().Be(WindowBackdropStyle.Windows);
        store.PowerModeMappingMode.Should().Be(PowerModeMappingMode.WindowsPowerMode);
        store.MinimizeToTray.Should().BeTrue();
        store.AnimationsEnabled.Should().BeTrue();
        store.AnimationSpeed.Should().Be(2.0);
        store.NavigationPaneExpanded.Should().BeTrue();
        store.NotificationPosition.Should().Be(NotificationPosition.BottomRight);
        store.NotificationDuration.Should().Be(NotificationDuration.Normal);
        store.PowerPlans.Should().NotBeNull();
        store.PowerModes.Should().NotBeNull();
    }

    [Fact]
    public void ApplicationSettingsStore_SetProperties_ShouldRetainValues()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore
        {
            Theme = Theme.Dark,
            ThemeStylePreset = ThemeStylePreset.Midnight,
            MinimizeToTray = false,
            AnimationSpeed = 3.0
        };
        store.Theme.Should().Be(Theme.Dark);
        store.ThemeStylePreset.Should().Be(ThemeStylePreset.Midnight);
        store.MinimizeToTray.Should().BeFalse();
        store.AnimationSpeed.Should().Be(3.0);
    }

    [Fact]
    public void ApplicationSettingsStore_PowerPlans_ShouldAcceptValues()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.PowerPlans[PowerModeState.Quiet] = Guid.NewGuid();
        store.PowerPlans[PowerModeState.Balance] = Guid.NewGuid();
        store.PowerPlans.Should().HaveCount(2);
    }

    [Fact]
    public void ApplicationSettingsStore_PowerModes_ShouldAcceptValues()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.PowerModes[PowerModeState.Quiet] = WindowsPowerMode.BestPowerEfficiency;
        store.PowerModes[PowerModeState.Performance] = WindowsPowerMode.BestPerformance;
        store.PowerModes.Should().HaveCount(2);
    }

    [Fact]
    public void ApplicationSettingsStore_AccentColor_ShouldAcceptRGBColor()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore
        {
            AccentColor = new RGBColor(255, 33, 33)
        };
        store.AccentColor.Should().NotBeNull();
        store.AccentColor!.Value.R.Should().Be(255);
        store.AccentColor.Value.G.Should().Be(33);
        store.AccentColor.Value.B.Should().Be(33);
    }

    [Fact]
    public void ApplicationSettingsStore_NullWindowSize_ShouldBeAllowed()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.WindowSize.Should().BeNull();
    }

    #endregion

    #region ApplicationSettings Notifications Tests

    [Fact]
    public void Notifications_Defaults_ShouldHaveExpectedValues()
    {
        var n = new ApplicationSettings.Notifications();
        n.UpdateAvailable.Should().BeTrue();
        n.CapsNumLock.Should().BeFalse();
        n.FnLock.Should().BeFalse();
        n.TouchpadLock.Should().BeTrue();
        n.KeyboardBacklight.Should().BeTrue();
        n.CameraLock.Should().BeTrue();
        n.Microphone.Should().BeTrue();
        n.PowerMode.Should().BeFalse();
        n.RefreshRate.Should().BeTrue();
        n.ACAdapter.Should().BeFalse();
        n.SmartKey.Should().BeFalse();
        n.AutomationNotification.Should().BeTrue();
    }

    [Fact]
    public void Notifications_SetProperties_ShouldRetainValues()
    {
        var n = new ApplicationSettings.Notifications
        {
            CapsNumLock = true,
            FnLock = true,
            PowerMode = true
        };
        n.CapsNumLock.Should().BeTrue();
        n.FnLock.Should().BeTrue();
        n.PowerMode.Should().BeTrue();
    }

    #endregion

    #region OsdSettingsStore Tests

    [Fact]
    public void OsdSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.ShowOsd.Should().BeFalse();
        store.OsdRefreshInterval.Should().Be(1.0);
        store.SelectedStyleIndex.Should().Be(0);
        store.Items.Should().NotBeNull();
        store.Items.Should().NotBeEmpty();
        store.BackgroundOpacity.Should().Be(0.6);
        store.BackgroundColor.Should().Be("#1E1E1E");
        store.FontSize.Should().Be(12);
        store.CornerRadiusTop.Should().Be(6);
        store.CornerRadiusBottom.Should().Be(6);
        store.IsLocked.Should().BeFalse();
        store.PanelPositionX.Should().BeNull();
        store.PanelPositionY.Should().BeNull();
        store.BarPositionX.Should().BeNull();
        store.BarPositionY.Should().BeNull();
        store.TempThresholdWarning.Should().Be(75);
        store.TempThresholdCritical.Should().Be(90);
        store.UsageThresholdWarning.Should().Be(70);
    }

    [Fact]
    public void OsdSettingsStore_SetProperties_ShouldRetainValues()
    {
        var store = new OsdSettings.OsdSettingsStore
        {
            ShowOsd = true,
            OsdRefreshInterval = 2.0,
            BackgroundOpacity = 0.8,
            FontSize = 16,
            IsLocked = true
        };
        store.ShowOsd.Should().BeTrue();
        store.OsdRefreshInterval.Should().Be(2.0);
        store.BackgroundOpacity.Should().Be(0.8);
        store.FontSize.Should().Be(16);
        store.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void OsdSettingsStore_Items_ShouldAcceptCustomList()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.Items = new List<OsdItem> { OsdItem.Fps, OsdItem.CpuTemperature };
        store.Items.Should().HaveCount(2);
    }

    [Fact]
    public void OsdSettingsStore_PositionValues_ShouldBeNullable()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.PanelPositionX.Should().BeNull();
        store.PanelPositionY.Should().BeNull();
        store.BarPositionX.Should().BeNull();
        store.BarPositionY.Should().BeNull();
    }

    [Fact]
    public void OsdSettingsStore_PositionValues_SetValues_ShouldRetain()
    {
        var store = new OsdSettings.OsdSettingsStore
        {
            PanelPositionX = 100.0,
            PanelPositionY = 200.0,
            BarPositionX = 300.0,
            BarPositionY = 400.0
        };
        store.PanelPositionX.Should().Be(100.0);
        store.PanelPositionY.Should().Be(200.0);
        store.BarPositionX.Should().Be(300.0);
        store.BarPositionY.Should().Be(400.0);
    }

    #endregion

    #region UpdateCheckSettingsStore Tests

    [Fact]
    public void UpdateCheckSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new UpdateCheckSettings.UpdateCheckSettingsStore();
        store.LastUpdateCheckDateTime.Should().BeNull();
        store.UpdateCheckFrequency.Should().Be(default);
        store.UpdateRepositoryOwner.Should().BeNull();
        store.UpdateRepositoryName.Should().BeNull();
    }

    [Fact]
    public void UpdateCheckSettingsStore_SetProperties_ShouldRetainValues()
    {
        var now = DateTime.UtcNow;
        var store = new UpdateCheckSettings.UpdateCheckSettingsStore
        {
            LastUpdateCheckDateTime = now,
            UpdateCheckFrequency = UpdateCheckFrequency.PerWeek,
            UpdateRepositoryOwner = "test-owner",
            UpdateRepositoryName = "test-repo"
        };
        store.LastUpdateCheckDateTime.Should().Be(now);
        store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerWeek);
        store.UpdateRepositoryOwner.Should().Be("test-owner");
        store.UpdateRepositoryName.Should().Be("test-repo");
    }

    #endregion

    #region IntegrationsSettingsStore Tests

    [Fact]
    public void IntegrationsSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new IntegrationsSettings.IntegrationsSettingsStore();
        store.HWiNFO.Should().BeFalse();
        store.CLI.Should().BeFalse();
    }

    [Fact]
    public void IntegrationsSettingsStore_SetProperties_ShouldRetainValues()
    {
        var store = new IntegrationsSettings.IntegrationsSettingsStore
        {
            HWiNFO = true,
            CLI = true
        };
        store.HWiNFO.Should().BeTrue();
        store.CLI.Should().BeTrue();
    }

    #endregion

    #region PackageDownloaderSettingsStore Tests

    [Fact]
    public void PackageDownloaderSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new PackageDownloaderSettings.PackageDownloaderSettingsStore();
        store.DownloadPath.Should().BeNull();
        store.OnlyShowUpdates.Should().BeFalse();
        store.HiddenPackages.Should().NotBeNull();
        store.HiddenPackages.Should().BeEmpty();
    }

    [Fact]
    public void PackageDownloaderSettingsStore_SetProperties_ShouldRetainValues()
    {
        var store = new PackageDownloaderSettings.PackageDownloaderSettingsStore
        {
            DownloadPath = @"C:\Downloads",
            OnlyShowUpdates = true
        };
        store.DownloadPath.Should().Be(@"C:\Downloads");
        store.OnlyShowUpdates.Should().BeTrue();
    }

    [Fact]
    public void PackageDownloaderSettingsStore_HiddenPackages_ShouldAcceptValues()
    {
        var store = new PackageDownloaderSettings.PackageDownloaderSettingsStore();
        store.HiddenPackages.Add("pkg1");
        store.HiddenPackages.Add("pkg2");
        store.HiddenPackages.Should().HaveCount(2);
    }

    #endregion

    #region WindowSize Struct Tests

    [Fact]
    public void WindowSize_Constructor_ShouldSetProperties()
    {
        var ws = new WindowSize(1920.0, 1080.0);
        ws.Width.Should().Be(1920.0);
        ws.Height.Should().Be(1080.0);
    }

    [Fact]
    public void WindowSize_Equality_SameValues_ShouldBeEqual()
    {
        var a = new WindowSize(800.0, 600.0);
        var b = new WindowSize(800.0, 600.0);
        a.Should().Be(b);
    }

    [Fact]
    public void WindowSize_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new WindowSize(800.0, 600.0);
        var b = new WindowSize(1024.0, 768.0);
        a.Should().NotBe(b);
    }

    [Fact]
    public void WindowSize_ZeroValues_ShouldWork()
    {
        var ws = new WindowSize(0, 0);
        ws.Width.Should().Be(0);
        ws.Height.Should().Be(0);
    }

    #endregion

    #region BatteryHealthAlertStore Tests

    [Fact]
    public void BatteryHealthAlertStore_Normalize_ShouldClampInvalidThresholds()
    {
        var store = new BatteryHealthAlertSettings.BatteryHealthAlertStore
        {
            LowHealthThreshold = 250,
            CriticalHealthThreshold = -5,
            TemperatureThresholdC = 200
        };

        store.Normalize();

        store.LowHealthThreshold.Should().Be(BatteryHealthAlertSettings.DefaultLowHealthThreshold);
        store.CriticalHealthThreshold.Should().Be(Math.Max(0, BatteryHealthAlertSettings.DefaultLowHealthThreshold - 20));
        store.TemperatureThresholdC.Should().Be(0);
    }

    #endregion
}
