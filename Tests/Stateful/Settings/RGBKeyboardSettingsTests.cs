using System.Collections.Generic;
using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class RGBKeyboardSettingsTests : IDisposable
{
    public RGBKeyboardSettingsTests()
    {
        // Clean up any existing settings file before tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.RGBKeyboard);
    }

    public void Dispose()
    {
        // Clean up settings file after all tests
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.RGBKeyboard);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var settings = new RGBKeyboardSettings();

        // Assert
        settings.Should().NotBeNull();
    }

    #endregion

    #region Store Tests

    [Fact]
    public void Store_ShouldReturnDefaultWhenNotLoaded()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act
        var store = settings.Store;

        // Assert
        store.Should().NotBeNull();
        store.State.Should().NotBeNull();
        store.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Off);
        store.State.Presets.Should().NotBeNull();
        store.State.Presets.Should().HaveCount(4);
    }

    [Fact]
    public void Store_ShouldCacheValue()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();
        var firstStore = settings.Store;

        // Act
        var secondStore = settings.Store;

        // Assert
        secondStore.Should().BeSameAs(firstStore);
    }

    #endregion

    #region Default State Tests

    [Fact]
    public void DefaultState_ShouldHaveOffPreset()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act & Assert
        settings.Store.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Off);
    }

    [Fact]
    public void DefaultState_ShouldHaveFourPresets()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act & Assert
        settings.Store.State.Presets.Should().HaveCount(4);
        settings.Store.State.Presets.Should().ContainKeys(
            RGBKeyboardBacklightPreset.One,
            RGBKeyboardBacklightPreset.Two,
            RGBKeyboardBacklightPreset.Three,
            RGBKeyboardBacklightPreset.Four
        );
    }

    [Fact]
    public void DefaultPresetOne_ShouldHaveStaticEffectAndGreenColor()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act
        var presetOne = settings.Store.State.Presets[RGBKeyboardBacklightPreset.One];

        // Assert
        presetOne.Should().NotBeNull();
        presetOne.Effect.Should().Be(RGBKeyboardBacklightEffect.Static);
        presetOne.Speed.Should().Be(RGBKeyboardBacklightSpeed.Slowest);
        presetOne.Brightness.Should().Be(RGBKeyboardBacklightBrightness.Low);
        presetOne.Zone1.Should().Be(RGBColor.Green);
        presetOne.Zone2.Should().Be(RGBColor.Teal);
        presetOne.Zone3.Should().Be(RGBColor.Purple);
        presetOne.Zone4.Should().Be(RGBColor.Pink);
    }

    [Fact]
    public void DefaultPresetTwo_ShouldHaveStaticEffectAndRedColor()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act
        var presetTwo = settings.Store.State.Presets[RGBKeyboardBacklightPreset.Two];

        // Assert
        presetTwo.Should().NotBeNull();
        presetTwo.Effect.Should().Be(RGBKeyboardBacklightEffect.Static);
        presetTwo.Zone1.Should().Be(RGBColor.Red);
        presetTwo.Zone2.Should().Be(RGBColor.Red);
        presetTwo.Zone3.Should().Be(RGBColor.Red);
        presetTwo.Zone4.Should().Be(RGBColor.Red);
    }

    [Fact]
    public void DefaultPresetThree_ShouldHaveBreathEffectAndWhiteColor()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act
        var presetThree = settings.Store.State.Presets[RGBKeyboardBacklightPreset.Three];

        // Assert
        presetThree.Should().NotBeNull();
        presetThree.Effect.Should().Be(RGBKeyboardBacklightEffect.Breath);
        presetThree.Zone1.Should().Be(RGBColor.White);
        presetThree.Zone2.Should().Be(RGBColor.White);
        presetThree.Zone3.Should().Be(RGBColor.White);
        presetThree.Zone4.Should().Be(RGBColor.White);
    }

    [Fact]
    public void DefaultPresetFour_ShouldHaveSmoothEffectAndWhiteColor()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act
        var presetFour = settings.Store.State.Presets[RGBKeyboardBacklightPreset.Four];

        // Assert
        presetFour.Should().NotBeNull();
        presetFour.Effect.Should().Be(RGBKeyboardBacklightEffect.Smooth);
        presetFour.Zone1.Should().Be(RGBColor.White);
        presetFour.Zone2.Should().Be(RGBColor.White);
        presetFour.Zone3.Should().Be(RGBColor.White);
        presetFour.Zone4.Should().Be(RGBColor.White);
    }

    #endregion

    #region State Modification Tests

    [Fact]
    public void State_WhenPresetChanged_ShouldPersist()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();
        settings.Store.State = new RGBKeyboardBacklightState(
            RGBKeyboardBacklightPreset.One,
            settings.Store.State.Presets
        );
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.One);
    }

    [Fact]
    public void State_WhenPresetsModified_ShouldPersist()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();
        var blueColor = new RGBColor(0, 0, 255);
        var newPresets = new Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription>
        {
            { RGBKeyboardBacklightPreset.One, new(RGBKeyboardBacklightEffect.WaveRTL, RGBKeyboardBacklightSpeed.Fast, RGBKeyboardBacklightBrightness.High, blueColor, blueColor, blueColor, blueColor) }
        };
        settings.Store.State = new RGBKeyboardBacklightState(RGBKeyboardBacklightPreset.One, newPresets);
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.State.Presets[RGBKeyboardBacklightPreset.One].Effect.Should().Be(RGBKeyboardBacklightEffect.WaveRTL);
        reloadedStore.State.Presets[RGBKeyboardBacklightPreset.One].Zone1.Should().Be(blueColor);
    }

    #endregion

    #region SynchronizeStore Tests

    [Fact]
    public void SynchronizeStore_ShouldNotThrow()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act & Assert
        settings.SynchronizeStore();
    }

    [Fact]
    public void SynchronizeStore_AfterModifyingStore_ShouldSaveChanges()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();
        settings.Store.State = new RGBKeyboardBacklightState(
            RGBKeyboardBacklightPreset.Two,
            settings.Store.State.Presets
        );

        // Act
        settings.SynchronizeStore();
        settings.InvalidateCache();
        var reloadedStore = settings.Store;

        // Assert
        reloadedStore.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Two);
    }

    #endregion

    #region LoadStore Tests

    [Fact]
    public void LoadStore_WhenNoFile_ShouldReturnNull()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();

        // Act
        var store = settings.LoadStore();

        // Assert - LoadStore returns null when no file exists, Store property returns Default
        store.Should().BeNull();
        settings.Store.Should().NotBeNull();
        settings.Store.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Off);
        settings.Store.State.Presets.Should().HaveCount(4);
    }

    [Fact]
    public void LoadStore_AfterSynchronizeStore_ShouldLoadSavedData()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();
        settings.Store.State = new RGBKeyboardBacklightState(
            RGBKeyboardBacklightPreset.Three,
            settings.Store.State.Presets
        );
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.LoadStore();

        // Assert
        store.Should().NotBeNull();
        store!.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Three);
    }

    #endregion

    #region InvalidateCache Tests

    [Fact]
    public void InvalidateCache_ShouldForceReload()
    {
        // Arrange
        var settings = new RGBKeyboardSettings();
        settings.Store.State = new RGBKeyboardBacklightState(
            RGBKeyboardBacklightPreset.Four,
            settings.Store.State.Presets
        );
        settings.SynchronizeStore();
        settings.InvalidateCache();

        // Act
        var store = settings.Store;

        // Assert - Should reload from file
        store.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Four);
    }

    #endregion

    #region RGBKeyboardSettingsStore Tests

    [Fact]
    public void RGBKeyboardSettingsStore_ShouldInitialize()
    {
        // Arrange & Act
        var store = new RGBKeyboardSettings.RGBKeyboardSettingsStore();

        // Assert
        store.Should().NotBeNull();
        // State property auto-initializes to non-null in C# 10+ with required properties
        // or may have a default value
        store.State.Should().NotBeNull();
    }

    #endregion
}