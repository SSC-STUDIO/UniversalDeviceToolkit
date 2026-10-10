using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class IntegrationsSettingsTests : IDisposable
{
    public IntegrationsSettingsTests()
    {
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.Integrations);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.Integrations);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new IntegrationsSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new IntegrationsSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.HWiNFO.Should().BeFalse();
        store.CLI.Should().BeFalse();
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region HWiNFO Tests

    [Fact]
    public void HWiNFO_DefaultValue_ShouldBeFalse()
    {
        // Arrange
        var settings = new IntegrationsSettings();

        // Act & Assert
        settings.Store.HWiNFO.Should().BeFalse();
    }

    [Fact]
    public void HWiNFO_WhenSetToTrue_ShouldPersist()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.HWiNFO = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.HWiNFO.Should().BeTrue();
    }

    [Fact]
    public void HWiNFO_WhenSetToFalse_ShouldPersist()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.HWiNFO = true;
        settings.SynchronizeStore();
        settings.Store.HWiNFO = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.HWiNFO.Should().BeFalse();
    }

    #endregion

    #region CLI Tests

    [Fact]
    public void CLI_DefaultValue_ShouldBeFalse()
    {
        // Arrange
        var settings = new IntegrationsSettings();

        // Act & Assert
        settings.Store.CLI.Should().BeFalse();
    }

    [Fact]
    public void CLI_WhenSetToTrue_ShouldPersist()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.CLI = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.CLI.Should().BeTrue();
    }

    [Fact]
    public void CLI_WhenSetToFalse_ShouldPersist()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.CLI = true;
        settings.SynchronizeStore();
        settings.Store.CLI = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.CLI.Should().BeFalse();
    }

    #endregion

    #region Both Settings Tests

    [Fact]
    public void BothSettings_WhenSet_ShouldPersistBoth()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.HWiNFO = true;
        settings.Store.CLI = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.HWiNFO.Should().BeTrue();
        reloadedStore.CLI.Should().BeTrue();
    }

    [Fact]
    public void BothSettings_WhenOnlyOneEnabled_ShouldPersistCorrectly()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.HWiNFO = true;
        settings.Store.CLI = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.HWiNFO.Should().BeTrue();
        reloadedStore.CLI.Should().BeFalse();
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new IntegrationsSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.HWiNFO = true;
        settings.Store.CLI = false;

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.HWiNFO.Should().BeTrue();
        reloadedStore.CLI.Should().BeFalse();
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnNull()
    {
        // Arrange
        var settings = new IntegrationsSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns null when no file exists, Store property returns Default
        store.Should().BeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.HWiNFO.Should().BeFalse();
        settings.Store.CLI.Should().BeFalse();
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.CLI = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.CLI.Should().BeTrue();
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public void InvalidateCache_ShouldForceReload()
    {
        // Arrange
        var settings = new IntegrationsSettings();
        settings.Store.HWiNFO = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.Store;

        // Assert - Should reload from file
        store.HWiNFO.Should().BeTrue();
    }

    #endregion

    #region IntegrationsSettingsStore Tests

    [Fact]
    public void IntegrationsSettingsStore_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var store = new IntegrationsSettings.IntegrationsSettingsStore();

        // Assert
        store.Should().NotBeNull();
        store.HWiNFO.Should().BeFalse();
        store.CLI.Should().BeFalse();
    }

    [Fact]
    public void IntegrationsSettingsStore_WhenPropertiesSet_ShouldUpdate()
    {
        // Arrange
        var store = new IntegrationsSettings.IntegrationsSettingsStore
        {
            HWiNFO = true,
            CLI = false
        };

        // Act & Assert
        store.HWiNFO.Should().BeTrue();
        store.CLI.Should().BeFalse();
    }

    #endregion
}