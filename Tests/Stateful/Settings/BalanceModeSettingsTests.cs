using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class BalanceModeSettingsTests : IDisposable
{
    public BalanceModeSettingsTests()
    {
        SettingsCleanupHelper.UseIsolatedAppData();
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.BalanceMode);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.BalanceMode);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new BalanceModeSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new BalanceModeSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.AIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new BalanceModeSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region AIModeEnabled Tests

    [Fact]
    public void AIModeEnabled_DefaultValue_ShouldBeFalse()
    {
        // Arrange
        var settings = new BalanceModeSettings();

        // Act & Assert
        settings.Store.AIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void AIModeEnabled_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new BalanceModeSettings();
        settings.Store.AIModeEnabled = true;
        settings.SynchronizeStore();

        // Act - Invalidate cache and reload
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.AIModeEnabled.Should().BeTrue();
    }

    [Fact]
    public void AIModeEnabled_WhenSetToFalse_ShouldPersist()
    {
        // Arrange
        var settings = new BalanceModeSettings();
        settings.Store.AIModeEnabled = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Verify it was saved
        settings.Store.AIModeEnabled.Should().BeTrue();

        // Now set to false
        settings.Store.AIModeEnabled = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.AIModeEnabled.Should().BeFalse();
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new BalanceModeSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new BalanceModeSettings();
        settings.Store.AIModeEnabled = true;

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.AIModeEnabled.Should().BeTrue();
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnNull()
    {
        // Arrange
        var settings = new BalanceModeSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns null when no file exists, Store property returns Default
        store.Should().BeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.AIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new BalanceModeSettings();
        settings.Store.AIModeEnabled = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.AIModeEnabled.Should().BeTrue();
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public void InvalidateCache_ShouldForceReload()
    {
        // Arrange
        var settings = new BalanceModeSettings();
        settings.Store.AIModeEnabled = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.Store;

        // Assert - Should reload from file
        store.AIModeEnabled.Should().BeTrue();
    }

    #endregion

    #region BalanceModeSettingsStore Tests

    [Fact]
    public void BalanceModeSettingsStore_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var store = new BalanceModeSettings.BalanceModeSettingsStore();

        // Assert
        store.Should().NotBeNull();
        store.AIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void BalanceModeSettingsStore_WhenAIModeEnabledSet_ShouldUpdate()
    {
        // Arrange
        var store = new BalanceModeSettings.BalanceModeSettingsStore();

        // Act
        store.AIModeEnabled = true;

        // Assert
        store.AIModeEnabled.Should().BeTrue();
    }

    #endregion

    [Fact]
    public void Store_DefaultValues_ShouldBeCorrect()
    {
        var settings = new BalanceModeSettings();

        settings.Store.AIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void Store_CanBeModified()
    {
        var settings = new BalanceModeSettings();

        settings.Store.AIModeEnabled = true;

        settings.Store.AIModeEnabled.Should().BeTrue();
    }
}
