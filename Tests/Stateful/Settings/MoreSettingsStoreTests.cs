using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Collection(TestCollections.Settings)]
[Trait("Category", TestCategories.Unit)]
public class FanCurveSettingsStoreTests
{
    [Fact]
    public void FanCurveSettingsStore_Defaults_ShouldHaveEmptyEntries()
    {
        var store = new FanCurveSettings.FanCurveSettingsStore();
        store.Entries.Should().NotBeNull();
        store.Entries.Should().BeEmpty();
    }

    [Fact]
    public void FanCurveSettingsStore_SetEntries_ShouldWork()
    {
        var entry = new FanCurveEntry();
        var store = new FanCurveSettings.FanCurveSettingsStore
        {
            Entries = new List<FanCurveEntry> { entry }
        };
        store.Entries.Should().HaveCount(1);
    }
}

[Trait("Category", TestCategories.Unit)]
public class SpectrumKeyboardSettingsStoreTests
{
    [Fact]
    public void SpectrumKeyboardSettingsStore_Defaults_ShouldBeNull()
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore();
        store.KeyboardLayout.Should().BeNull();
    }

    [Fact]
    public void SpectrumKeyboardSettingsStore_SetLayout_ShouldWork()
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore
        {
            KeyboardLayout = KeyboardLayout.Ansi
        };
        store.KeyboardLayout.Should().Be(KeyboardLayout.Ansi);
    }

    [Theory]
    [InlineData(KeyboardLayout.Ansi)]
    [InlineData(KeyboardLayout.Iso)]
    [InlineData(KeyboardLayout.Jis)]
    public void SpectrumKeyboardSettingsStore_VariousLayouts_ShouldAccept(KeyboardLayout layout)
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore
        {
            KeyboardLayout = layout
        };
        store.KeyboardLayout.Should().Be(layout);
    }

    [Fact]
    public void Defaults_KeyboardLayout_ShouldBeNull()
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore();
        store.KeyboardLayout.Should().BeNull();
    }

    [Fact]
    public void Property_ShouldBeSettable()
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore();
        store.KeyboardLayout = KeyboardLayout.Ansi;
        store.KeyboardLayout.Should().Be(KeyboardLayout.Ansi);
    }
}


