using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class ApplicationSettingsTests : IDisposable
{
    public ApplicationSettingsTests()
    {
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.Application);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.Application);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new ApplicationSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.MinimizeToTray.Should().BeTrue();
        store.AnimationsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new ApplicationSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region PowerPlans Tests

    [Fact]
    public void PowerPlans_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new ApplicationSettings();
        var powerPlans = new System.Collections.Generic.Dictionary<PowerModeState, Guid>
        {
            { PowerModeState.Quiet, Guid.NewGuid() },
            { PowerModeState.Balance, Guid.NewGuid() },
            { PowerModeState.Performance, Guid.NewGuid() }
        };
        settings.Store.PowerPlans = powerPlans;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.PowerPlans.Should().NotBeNull();
        reloadedStore.PowerPlans.Count.Should().Be(3);
        reloadedStore.PowerPlans[PowerModeState.Quiet].Should().Be(powerPlans[PowerModeState.Quiet]);
    }

    #endregion

    #region MinimizeToTray Tests

    [Fact]
    public void MinimizeToTray_DefaultValue_ShouldBeTrue()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act & Assert
        settings.Store.MinimizeToTray.Should().BeTrue();
    }

    [Fact]
    public void MinimizeToTray_WhenSetToFalse_ShouldPersist()
    {
        // Arrange
        var settings = new ApplicationSettings();
        settings.Store.MinimizeToTray = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.MinimizeToTray.Should().BeFalse();
    }

    #endregion

    #region Notifications Tests

    [Fact]
    public void Notifications_DefaultValues_ShouldBeCorrect()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act & Assert
        settings.Store.Notifications.UpdateAvailable.Should().BeTrue();
        settings.Store.Notifications.CapsNumLock.Should().BeFalse();
        settings.Store.Notifications.KeyboardBacklight.Should().BeTrue();
    }

    [Fact]
    public void Notifications_WhenModified_ShouldPersist()
    {
        // Arrange
        var settings = new ApplicationSettings();
        settings.Store.Notifications.UpdateAvailable = false;
        settings.Store.Notifications.CapsNumLock = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Notifications.UpdateAvailable.Should().BeFalse();
        reloadedStore.Notifications.CapsNumLock.Should().BeTrue();
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new ApplicationSettings();
        settings.Store.MinimizeToTray = false;
        settings.Store.AnimationsEnabled = false;

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.MinimizeToTray.Should().BeFalse();
        reloadedStore.AnimationsEnabled.Should().BeFalse();
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnDefault()
    {
        // Arrange
        var settings = new ApplicationSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns Default when no file exists per ApplicationSettings override
        store.Should().NotBeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.MinimizeToTray.Should().BeTrue();
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new ApplicationSettings();
        settings.Store.MinimizeToTray = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.MinimizeToTray.Should().BeFalse();
    }

    #endregion
}