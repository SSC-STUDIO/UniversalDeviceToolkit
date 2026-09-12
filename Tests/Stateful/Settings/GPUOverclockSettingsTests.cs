using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class GPUOverclockSettingsTests : IDisposable
{
    public GPUOverclockSettingsTests()
    {
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.GPUOverclock);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.GPUOverclock);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new GPUOverclockSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new GPUOverclockSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.Enabled.Should().BeFalse();
        store.Info.Should().NotBeNull();
        store.Info.Should().Be(GPUOverclockInfo.Zero);
        store.ActiveProfileId.Should().Be(Guid.Empty);
        store.Profiles.Should().NotBeNull();
        store.Profiles.Should().BeEmpty();
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region Enabled Tests

    [Fact]
    public void Enabled_DefaultValue_ShouldBeFalse()
    {
        // Arrange
        var settings = new GPUOverclockSettings();

        // Act & Assert
        settings.Store.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enabled_WhenSetToTrue_ShouldPersist()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        settings.Store.Enabled = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Enabled_WhenSetToFalse_ShouldPersist()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        settings.Store.Enabled = true;
        settings.SynchronizeStore();
        settings.Store.Enabled = false;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Enabled.Should().BeFalse();
    }

    #endregion

    #region Info Tests

    [Fact]
    public void Info_DefaultValue_ShouldBeZero()
    {
        // Arrange
        var settings = new GPUOverclockSettings();

        // Act & Assert
        settings.Store.Info.Should().Be(GPUOverclockInfo.Zero);
    }

    [Fact]
    public void Info_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        var overclockInfo = new GPUOverclockInfo(100, 200);
        settings.Store.Info = overclockInfo;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Info.Should().NotBe(GPUOverclockInfo.Zero);
        reloadedStore.Info.CoreDeltaMhz.Should().Be(100);
        reloadedStore.Info.MemoryDeltaMhz.Should().Be(200);
    }

    [Fact]
    public void Profiles_WhenAdded_ShouldPersist()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        var profileId = Guid.NewGuid();
        settings.Store.ActiveProfileId = profileId;
        settings.Store.Profiles[profileId] = new GPUOverclockSettings.GPUOverclockSettingsStore.Profile
        {
            Name = "Gaming",
            Info = new GPUOverclockInfo(120, 300)
        };

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.ActiveProfileId.Should().Be(profileId);
        reloadedStore.Profiles.Should().ContainKey(profileId);
        reloadedStore.Profiles[profileId].Name.Should().Be("Gaming");
        reloadedStore.Profiles[profileId].Info.CoreDeltaMhz.Should().Be(120);
        reloadedStore.Profiles[profileId].Info.MemoryDeltaMhz.Should().Be(300);
    }

    [Fact]
    public void Profile_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var profile = new GPUOverclockSettings.GPUOverclockSettingsStore.Profile();

        // Assert
        profile.Name.Should().Be(GPUOverclockSettings.DefaultProfileName);
        profile.Info.Should().Be(GPUOverclockInfo.Zero);
    }

    [Fact]
    public void GetUniqueProfileName_WhenNameAlreadyExists_ShouldAppendIncrementingSuffix()
    {
        var existingId = Guid.NewGuid();
        var profiles = new Dictionary<Guid, GPUOverclockSettings.GPUOverclockSettingsStore.Profile>
        {
            [existingId] = new() { Name = "Gaming" },
            [Guid.NewGuid()] = new() { Name = "Gaming (2)" }
        };

        var name = GPUOverclockController.GetUniqueProfileName("Gaming", profiles);

        name.Should().Be("Gaming (3)");
    }

    [Fact]
    public void GetUniqueProfileName_WhenRenamingSameProfile_ShouldKeepName()
    {
        var activeProfileId = Guid.NewGuid();
        var profiles = new Dictionary<Guid, GPUOverclockSettings.GPUOverclockSettingsStore.Profile>
        {
            [activeProfileId] = new() { Name = "Gaming" },
            [Guid.NewGuid()] = new() { Name = "Balanced" }
        };

        var name = GPUOverclockController.GetUniqueProfileName("Gaming", profiles, activeProfileId);

        name.Should().Be("Gaming");
    }

    [Fact]
    public void NormalizeProfileName_WhenNameIsBlank_ShouldUseDefaultProfileName()
    {
        GPUOverclockController.NormalizeProfileName("   ").Should().Be(GPUOverclockSettings.DefaultProfileName);
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new GPUOverclockSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        settings.Store.Enabled = true;
        settings.Store.Info = new GPUOverclockInfo(50, 100);

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Enabled.Should().BeTrue();
        reloadedStore.Info.CoreDeltaMhz.Should().Be(50);
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnNull()
    {
        // Arrange
        var settings = new GPUOverclockSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns null when no file exists, Store property returns Default
        store.Should().BeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.Enabled.Should().BeFalse();
        settings.Store.Info.Should().Be(GPUOverclockInfo.Zero);
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        settings.Store.Enabled = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.Enabled.Should().BeTrue();
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public void InvalidateCache_ShouldForceReload()
    {
        // Arrange
        var settings = new GPUOverclockSettings();
        settings.Store.Enabled = true;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.Store;

        // Assert - Should reload from file
        store.Enabled.Should().BeTrue();
    }

    #endregion

    #region GPUOverclockSettingsStore Tests

    [Fact]
    public void GPUOverclockSettingsStore_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var store = new GPUOverclockSettings.GPUOverclockSettingsStore();

        // Assert
        store.Should().NotBeNull();
        store.Enabled.Should().BeFalse();
        store.Info.Should().Be(GPUOverclockInfo.Zero);
        store.Profiles.Should().BeEmpty();
    }

    [Fact]
    public void GPUOverclockSettingsStore_WhenPropertiesSet_ShouldUpdate()
    {
        // Arrange
        var store = new GPUOverclockSettings.GPUOverclockSettingsStore
        {
            Enabled = true,
            Info = new GPUOverclockInfo(150, 250),
            ActiveProfileId = Guid.NewGuid()
        };

        // Act & Assert
        store.Enabled.Should().BeTrue();
        store.Info.CoreDeltaMhz.Should().Be(150);
        store.Info.MemoryDeltaMhz.Should().Be(250);
        store.ActiveProfileId.Should().NotBe(Guid.Empty);
    }

    #endregion
}
