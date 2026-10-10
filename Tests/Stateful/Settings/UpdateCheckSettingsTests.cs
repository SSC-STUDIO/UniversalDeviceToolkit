using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class UpdateCheckSettingsTests : IDisposable
{
    public UpdateCheckSettingsTests()
    {
        SettingsCleanupHelper.UseIsolatedAppData();
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.UpdateCheck);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.UpdateCheck);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new UpdateCheckSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.LastUpdateCheckDateTime.Should().BeNull();
        store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerDay);
        store.UpdateRepositoryOwner.Should().BeNull();
        store.UpdateRepositoryName.Should().BeNull();
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region LastUpdateCheckDateTime Tests

    [Fact]
    public void LastUpdateCheckDateTime_DefaultValue_ShouldBeNull()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act & Assert
        settings.Store.LastUpdateCheckDateTime.Should().BeNull();
    }

    [Fact]
    public void LastUpdateCheckDateTime_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        var checkTime = DateTime.UtcNow;
        settings.Store.LastUpdateCheckDateTime = checkTime;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.LastUpdateCheckDateTime.Should().NotBeNull();
        reloadedStore.LastUpdateCheckDateTime.Should().BeCloseTo(checkTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LastUpdateCheckDateTime_WhenSetToNull_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.LastUpdateCheckDateTime = DateTime.UtcNow;
        settings.SynchronizeStore();
        settings.Store.LastUpdateCheckDateTime = null;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.LastUpdateCheckDateTime.Should().BeNull();
    }

    #endregion

    #region UpdateCheckFrequency Tests

    [Fact]
    public void UpdateCheckFrequency_DefaultValue_ShouldBePerDay()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act & Assert
        settings.Store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerDay);
    }

    [Fact]
    public void UpdateCheckFrequency_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateCheckFrequency = UpdateCheckFrequency.PerWeek;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerWeek);
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.PerHour)]
    [InlineData(UpdateCheckFrequency.PerThreeHours)]
    [InlineData(UpdateCheckFrequency.PerTwelveHours)]
    [InlineData(UpdateCheckFrequency.PerDay)]
    [InlineData(UpdateCheckFrequency.PerWeek)]
    [InlineData(UpdateCheckFrequency.PerMonth)]
    public void UpdateCheckFrequency_AllValues_ShouldPersist(UpdateCheckFrequency frequency)
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateCheckFrequency = frequency;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateCheckFrequency.Should().Be(frequency);
    }

    #endregion

    #region UpdateRepositoryOwner Tests

    [Fact]
    public void UpdateRepositoryOwner_DefaultValue_ShouldBeNull()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act & Assert
        settings.Store.UpdateRepositoryOwner.Should().BeNull();
    }

    [Fact]
    public void UpdateRepositoryOwner_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryOwner = "CustomOwner";
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryOwner.Should().Be("CustomOwner");
    }

    [Fact]
    public void UpdateRepositoryOwner_WhenSetToNull_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryOwner = "TestOwner";
        settings.SynchronizeStore();
        settings.Store.UpdateRepositoryOwner = null;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryOwner.Should().BeNull();
    }

    [Fact]
    public void UpdateRepositoryOwner_WhenSetToEmpty_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryOwner = "";
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryOwner.Should().Be("");
    }

    #endregion

    #region UpdateRepositoryName Tests

    [Fact]
    public void UpdateRepositoryName_DefaultValue_ShouldBeNull()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act & Assert
        settings.Store.UpdateRepositoryName.Should().BeNull();
    }

    [Fact]
    public void UpdateRepositoryName_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryName = "CustomRepo";
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryName.Should().Be("CustomRepo");
    }

    [Fact]
    public void UpdateRepositoryName_WhenSetToNull_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryName = "TestRepo";
        settings.SynchronizeStore();
        settings.Store.UpdateRepositoryName = null;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryName.Should().BeNull();
    }

    #endregion

    #region Custom Repository Tests

    [Fact]
    public void CustomRepository_WhenBothSet_ShouldPersist()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryOwner = "MyOwner";
        settings.Store.UpdateRepositoryName = "MyRepo";
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryOwner.Should().Be("MyOwner");
        reloadedStore.UpdateRepositoryName.Should().Be("MyRepo");
    }

    [Fact]
    public void CustomRepository_WhenOnlyOwnerSet_ShouldPersistOwnerOnly()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateRepositoryOwner = "OnlyOwner";
        settings.Store.UpdateRepositoryName = null;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.UpdateRepositoryOwner.Should().Be("OnlyOwner");
        reloadedStore.UpdateRepositoryName.Should().BeNull();
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.LastUpdateCheckDateTime = DateTime.UtcNow;
        settings.Store.UpdateCheckFrequency = UpdateCheckFrequency.PerWeek;
        settings.Store.UpdateRepositoryOwner = "TestOwner";

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.LastUpdateCheckDateTime.Should().NotBeNull();
        reloadedStore.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerWeek);
        reloadedStore.UpdateRepositoryOwner.Should().Be("TestOwner");
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnNull()
    {
        // Arrange
        var settings = new UpdateCheckSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns null when no file exists, Store property returns Default
        store.Should().BeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.LastUpdateCheckDateTime.Should().BeNull();
        settings.Store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerDay);
        settings.Store.UpdateRepositoryOwner.Should().BeNull();
        settings.Store.UpdateRepositoryName.Should().BeNull();
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateCheckFrequency = UpdateCheckFrequency.PerMonth;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerMonth);
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public void InvalidateCache_ShouldForceReload()
    {
        // Arrange
        var settings = new UpdateCheckSettings();
        settings.Store.UpdateCheckFrequency = UpdateCheckFrequency.PerHour;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.Store;

        // Assert - Should reload from file
        store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerHour);
    }

    #endregion

    #region UpdateCheckSettingsStore Tests

    [Fact]
    public void UpdateCheckSettingsStore_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var store = new UpdateCheckSettings.UpdateCheckSettingsStore();

        // Assert - Default constructor uses default enum value (0 = PerHour)
        store.Should().NotBeNull();
        store.LastUpdateCheckDateTime.Should().BeNull();
        store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerHour);
        store.UpdateRepositoryOwner.Should().BeNull();
        store.UpdateRepositoryName.Should().BeNull();
    }

    [Fact]
    public void UpdateCheckSettingsStore_WhenPropertiesSet_ShouldUpdate()
    {
        // Arrange
        var store = new UpdateCheckSettings.UpdateCheckSettingsStore
        {
            LastUpdateCheckDateTime = DateTime.UtcNow,
            UpdateCheckFrequency = UpdateCheckFrequency.PerWeek,
            UpdateRepositoryOwner = "Owner",
            UpdateRepositoryName = "Repo"
        };

        // Act & Assert
        store.LastUpdateCheckDateTime.Should().NotBeNull();
        store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerWeek);
        store.UpdateRepositoryOwner.Should().Be("Owner");
        store.UpdateRepositoryName.Should().Be("Repo");
    }

    #endregion
}
