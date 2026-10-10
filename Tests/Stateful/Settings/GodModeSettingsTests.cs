using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class GodModeSettingsTests : IDisposable
{
    public GodModeSettingsTests()
    {
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.GodMode);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.GodMode);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new GodModeSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new GodModeSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.ActivePresetId.Should().Be(Guid.Empty);
        store.Presets.Should().NotBeNull();
        store.Presets.Should().BeEmpty();
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new GodModeSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region ActivePresetId Tests

    [Fact]
    public void ActivePresetId_DefaultValue_ShouldBeEmptyGuid()
    {
        // Arrange
        var settings = new GodModeSettings();

        // Act & Assert
        settings.Store.ActivePresetId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ActivePresetId_WhenSet_ShouldPersist()
    {
        // Arrange
        var settings = new GodModeSettings();
        var presetId = Guid.NewGuid();
        settings.Store.ActivePresetId = presetId;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.ActivePresetId.Should().Be(presetId);
    }

    #endregion

    #region Presets Tests

    [Fact]
    public void Presets_DefaultValue_ShouldBeEmptyDictionary()
    {
        // Arrange
        var settings = new GodModeSettings();

        // Act & Assert
        settings.Store.Presets.Should().NotBeNull();
        settings.Store.Presets.Should().BeEmpty();
    }

    [Fact]
    public void Presets_WhenAdded_ShouldPersist()
    {
        // Arrange
        var settings = new GodModeSettings();
        var presetId = Guid.NewGuid();
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            Name = "Test Preset",
            FanFullSpeed = false
        };

        settings.Store.Presets[presetId] = preset;
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Presets.Should().ContainKey(presetId);
        reloadedStore.Presets[presetId].Name.Should().Be("Test Preset");
        reloadedStore.Presets[presetId].FanFullSpeed.Should().BeFalse();
    }

    [Fact]
    public void Presets_WhenMultipleAdded_ShouldPersistAll()
    {
        // Arrange
        var settings = new GodModeSettings();
        var preset1Id = Guid.NewGuid();
        var preset2Id = Guid.NewGuid();
        var preset3Id = Guid.NewGuid();

        settings.Store.Presets[preset1Id] = new GodModeSettings.GodModeSettingsStore.Preset { Name = "Preset 1" };
        settings.Store.Presets[preset2Id] = new GodModeSettings.GodModeSettingsStore.Preset { Name = "Preset 2" };
        settings.Store.Presets[preset3Id] = new GodModeSettings.GodModeSettingsStore.Preset { Name = "Preset 3" };

        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.Presets.Should().HaveCount(3);
        reloadedStore.Presets.Should().ContainKeys(preset1Id, preset2Id, preset3Id);
    }

    #endregion

    #region Preset Class Tests

    [Fact]
    public void Preset_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var preset = new GodModeSettings.GodModeSettingsStore.Preset();

        // Assert
        preset.Should().NotBeNull();
        preset.Name.Should().BeEmpty();
        preset.FanFullSpeed.Should().BeNull();
        preset.FanTable.Should().BeNull();
    }

    [Fact]
    public void Preset_WhenPropertiesSet_ShouldUpdate()
    {
        // Arrange
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            Name = "Custom Preset",
            CPULongTermPowerLimit = new StepperValue(50, 5, 80, 1, new int[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80 }, 45),
            FanFullSpeed = true
        };

        // Act & Assert
        preset.Name.Should().Be("Custom Preset");
        preset.CPULongTermPowerLimit.Should().NotBeNull();
        // Access the inner Value property of the StepperValue struct
        preset.CPULongTermPowerLimit!.Value.Value.Should().Be(50);
        preset.FanFullSpeed.Should().BeTrue();
    }

    [Fact]
    public void Preset_WithAllProperties_ShouldStoreCorrectly()
    {
        // Arrange
        var preset = new GodModeSettings.GodModeSettingsStore.Preset
        {
            Name = "Full Preset",
            CPULongTermPowerLimit = new StepperValue(50, 5, 80, 1, new int[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80 }, 45),
            CPUShortTermPowerLimit = new StepperValue(60, 10, 90, 1, new int[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 }, 55),
            CPUTemperatureLimit = new StepperValue(85, 60, 100, 1, new int[] { 60, 65, 70, 75, 80, 85, 90, 95, 100 }, 80),
            GPUPowerBoost = new StepperValue(10, 0, 20, 1, new int[] { 0, 5, 10, 15, 20 }, 10),
            GPUTemperatureLimit = new StepperValue(87, 70, 95, 1, new int[] { 70, 75, 80, 85, 90, 95 }, 85),
            FanFullSpeed = false,
            MinValueOffset = -5,
            MaxValueOffset = 5
        };

        // Act & Assert
        preset.Name.Should().Be("Full Preset");
        preset.CPULongTermPowerLimit.Should().NotBeNull();
        preset.CPUShortTermPowerLimit.Should().NotBeNull();
        preset.CPUTemperatureLimit.Should().NotBeNull();
        preset.GPUPowerBoost.Should().NotBeNull();
        preset.GPUTemperatureLimit.Should().NotBeNull();
        preset.FanFullSpeed.Should().BeFalse();
        preset.MinValueOffset.Should().Be(-5);
        preset.MaxValueOffset.Should().Be(5);
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new GodModeSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new GodModeSettings();
        var presetId = Guid.NewGuid();
        settings.Store.ActivePresetId = presetId;
        settings.Store.Presets[presetId] = new GodModeSettings.GodModeSettingsStore.Preset { Name = "Test" };

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.ActivePresetId.Should().Be(presetId);
        reloadedStore.Presets.Should().ContainKey(presetId);
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnNull()
    {
        // Arrange
        var settings = new GodModeSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns null when no file exists, Store property returns Default
        store.Should().BeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.Presets.Should().BeEmpty();
        settings.Store.ActivePresetId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new GodModeSettings();
        settings.Store.ActivePresetId = Guid.NewGuid();
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.ActivePresetId.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public void InvalidateCache_ShouldForceReload()
    {
        // Arrange
        var settings = new GodModeSettings();
        settings.Store.ActivePresetId = Guid.NewGuid();
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.Store;

        // Assert - Should reload from file
        store.ActivePresetId.Should().NotBe(Guid.Empty);
    }

    #endregion
}