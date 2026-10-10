using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class ApplicationSettingsStoreDefaultsTests
{
    [Fact]
    public void Notifications_ShouldHaveDefaultValues()
    {
        var settings = new ApplicationSettings();

        var notifications = settings.Store.Notifications;

        notifications.Should().NotBeNull();
        notifications.UpdateAvailable.Should().BeTrue();
        notifications.TouchpadLock.Should().BeTrue();
        notifications.KeyboardBacklight.Should().BeTrue();
        notifications.CameraLock.Should().BeTrue();
        notifications.Microphone.Should().BeTrue();
        notifications.RefreshRate.Should().BeTrue();
        notifications.AutomationNotification.Should().BeTrue();
    }

    [Fact]
    public void Notifications_ShouldHaveDefaultFalseValues()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        var notifications = store.Notifications;

        notifications.CapsNumLock.Should().BeFalse();
        notifications.FnLock.Should().BeFalse();
        notifications.PowerMode.Should().BeFalse();
        notifications.ACAdapter.Should().BeFalse();
        notifications.SmartKey.Should().BeFalse();
    }

    [Fact]
    public void CustomCleanupRules_ShouldDefaultToEmptyList()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        var rules = store.CustomCleanupRules;

        rules.Should().NotBeNull();
        rules.Should().BeEmpty();
    }

    [Fact]
    public void CustomCleanupRule_ShouldHaveDefaultValues()
    {
        var rule = new CustomCleanupRule();

        rule.DirectoryPath.Should().BeEmpty();
        rule.Extensions.Should().NotBeNull();
        rule.Extensions.Should().BeEmpty();
        rule.Recursive.Should().BeTrue();
    }

    [Fact]
    public void PowerModeMappingMode_ShouldDefaultToWindowsPowerMode()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.PowerModeMappingMode.Should().Be(PowerModeMappingMode.WindowsPowerMode);
    }

    [Fact]
    public void AppScale_ShouldDefaultToStandard()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.AppScale.Should().Be(AppScale.Standard);
    }

    [Fact]
    public void NotificationPosition_ShouldDefaultToBottomRight()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.NotificationPosition.Should().Be(NotificationPosition.BottomRight);
    }

    [Fact]
    public void NotificationDuration_ShouldDefaultToNormal()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.NotificationDuration.Should().Be(NotificationDuration.Normal);
    }

    [Fact]
    public void MinimizeToTray_ShouldDefaultToTrue()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.MinimizeToTray.Should().BeTrue();
    }

    [Fact]
    public void MinimizeOnClose_ShouldDefaultToFalse()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.MinimizeOnClose.Should().BeFalse();
    }

    [Fact]
    public void PluginExtensionsNavigation_ShouldBeRemovedFromLegacySettings()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.NavigationItemsVisibility.Should().NotContainKey("pluginExtensions");
    }

    [Fact]
    public void ExcludedRefreshRates_ShouldDefaultToEmptyList()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.ExcludedRefreshRates.Should().NotBeNull();
        store.ExcludedRefreshRates.Should().BeEmpty();
    }

    [Fact]
    public void SmartKeyActionLists_ShouldDefaultToEmptyLists()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.SmartKeySinglePressActionList.Should().NotBeNull();
        store.SmartKeySinglePressActionList.Should().BeEmpty();
        store.SmartKeyDoublePressActionList.Should().NotBeNull();
        store.SmartKeyDoublePressActionList.Should().BeEmpty();
    }

    [Fact]
    public void PowerPlansAndModes_ShouldDefaultToEmptyDictionaries()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();

        store.PowerPlans.Should().NotBeNull();
        store.PowerPlans.Should().BeEmpty();
        store.PowerModes.Should().NotBeNull();
        store.PowerModes.Should().BeEmpty();
    }

    [Fact]
    public void ApplicationSettings_EachInstanceIsIndependent()
    {
        var settings1 = new ApplicationSettings();
        var settings2 = new ApplicationSettings();

        settings1.Should().NotBeSameAs(settings2);
    }

    [Fact]
    public void LoadStore_ShouldReturnDefault_WhenFileNotFound()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "llt-settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var prevOverride = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, tempRoot);

            var settings = new ApplicationSettings();
            var store = settings.LoadStore();

            store.Should().NotBeNull();
            store!.Notifications.UpdateAvailable.Should().BeTrue();
            store.NotificationDuration.Should().Be(NotificationDuration.Normal);
        }
        finally
        {
            if (prevOverride is null)
                Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, null);
            else
                Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, prevOverride);

            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temp test directory
            }
        }
    }

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        var settings = new ApplicationSettings();

        Action act = () => settings.SynchronizeStore();

        act.Should().NotThrow();
    }

    [Fact]
    public void Deserialize_LegacyJson_MissingApplyAccentColorToTheme_ShouldDefaultToTrue()
    {
        // Arrange: JSON from an older version that does not contain ApplyAccentColorToTheme
        const string legacyJson = """
                                  {
                                    "Theme": "Dark",
                                    "MinimizeToTray": true,
                                    "AccentColorSource": "System",
                                    "NotificationDuration": "Normal"
                                  }
                                  """;

        var options = LltJson.CreateSettingsOptions();
        options.Converters.Add(new LegacyPowerPlanGuidJsonConverter());

        // Act
        var store = JsonSerializer.Deserialize<ApplicationSettings.ApplicationSettingsStore>(legacyJson, options);

        // Assert
        store.Should().NotBeNull();
        store!.ApplyAccentColorToTheme.Should().BeTrue("old configs missing this field must default to true for backward compatibility");
        store.ApplyAccentColorToSystem.Should().BeTrue("old configs missing the system-color field must default to true for backward compatibility");
    }
}
