using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Settings;

[Trait("Category", TestCategories.Unit)]
public class SettingsModelAndExtensionEdgeTests
{
    #region Settings Store Defaults

    [Fact]
    public void RGBKeyboardSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new RGBKeyboardSettings.RGBKeyboardSettingsStore();
        store.State.Should().NotBeNull();
        store.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.One);
    }

    [Fact]
    public void OsdSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new OsdSettings.OsdSettingsStore();
        store.ShowOsd.Should().BeFalse();
        store.Items.Should().NotBeNull();
    }

    [Fact]
    public void UpdateCheckSettingsStore_FrequencyDefault_ShouldBePerHour()
    {
        var store = new UpdateCheckSettings.UpdateCheckSettingsStore();
        store.UpdateCheckFrequency.Should().Be(UpdateCheckFrequency.PerHour);
    }

    [Fact]
    public void UpdateCheckSettingsStore_LastCheckDefault_ShouldBeNull()
    {
        var store = new UpdateCheckSettings.UpdateCheckSettingsStore();
        store.LastUpdateCheckDateTime.Should().BeNull();
    }

    [Fact]
    public void PackageDownloaderSettingsStore_DownloadPathDefault_ShouldBeNull()
    {
        var store = new PackageDownloaderSettings.PackageDownloaderSettingsStore();
        store.DownloadPath.Should().BeNull();
    }

    [Fact]
    public void PackageDownloaderSettingsStore_OnlyShowUpdatesDefault_ShouldBeFalse()
    {
        var store = new PackageDownloaderSettings.PackageDownloaderSettingsStore();
        store.OnlyShowUpdates.Should().BeFalse();
    }

    [Fact]
    public void SpectrumKeyboardSettingsStore_LayoutDefault_ShouldBeNull()
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore();
        store.KeyboardLayout.Should().BeNull();
    }

    #endregion

    #region Settings Store Set/Get

    [Fact]
    public void RGBKeyboardSettingsStore_SetState_ShouldRetainValues()
    {
        var store = new RGBKeyboardSettings.RGBKeyboardSettingsStore
        {
            State = new RGBKeyboardBacklightState(
                RGBKeyboardBacklightPreset.Two,
                new Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription>())
        };
        store.State.SelectedPreset.Should().Be(RGBKeyboardBacklightPreset.Two);
    }

    [Fact]
    public void OsdSettingsStore_SetShowOsd_ShouldRetainValue()
    {
        var store = new OsdSettings.OsdSettingsStore { ShowOsd = true };
        store.ShowOsd.Should().BeTrue();
    }

    [Fact]
    public void PackageDownloaderSettingsStore_SetPath_ShouldRetainValue()
    {
        var store = new PackageDownloaderSettings.PackageDownloaderSettingsStore
        {
            DownloadPath = @"C:\Downloads"
        };
        store.DownloadPath.Should().Be(@"C:\Downloads");
    }

    [Fact]
    public void SpectrumKeyboardSettingsStore_SetLayout_ShouldRetainValue()
    {
        var store = new SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore
        {
            KeyboardLayout = KeyboardLayout.Jis
        };
        store.KeyboardLayout.Should().Be(KeyboardLayout.Jis);
    }

    [Fact]
    public void GPUOverclockSettingsStore_SetEnabled_ShouldRetainValue()
    {
        var store = new GPUOverclockSettings.GPUOverclockSettingsStore { Enabled = true };
        store.Enabled.Should().BeTrue();
    }

    #endregion

    #region Settings Normalization Edge Cases

    [Fact]
    public void BalanceModeSettingsStore_SetAIMode_ShouldRetainValue()
    {
        var store = new BalanceModeSettings.BalanceModeSettingsStore { AIModeEnabled = true };
        store.AIModeEnabled.Should().BeTrue();
    }

    [Fact]
    public void IntegrationsSettingsStore_SetValues_ShouldRetainValues()
    {
        var store = new IntegrationsSettings.IntegrationsSettingsStore { HWiNFO = true, CLI = true };
        store.HWiNFO.Should().BeTrue();
        store.CLI.Should().BeTrue();
    }

    #endregion

    #region ApplicationSettingsStore Additional Tests

    [Fact]
    public void ApplicationSettingsStore_MinimizeToTrayDefault_ShouldBeTrue()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.MinimizeToTray.Should().BeTrue();
    }

    [Fact]
    public void ApplicationSettingsStore_AnimationsEnabledDefault_ShouldBeTrue()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.AnimationsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ApplicationSettingsStore_AnimationSpeedDefault_ShouldBe2()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.AnimationSpeed.Should().Be(2.0);
    }

    [Fact]
    public void ApplicationSettingsStore_NavigationPaneExpandedDefault_ShouldBeTrue()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.NavigationPaneExpanded.Should().BeTrue();
    }

    [Fact]
    public void ApplicationSettingsStore_SetTheme_ShouldRetainValue()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore
        {
            Theme = Theme.Light
        };
        store.Theme.Should().Be(Theme.Light);
    }

    #endregion

    #region Time Struct Edge Cases

    [Fact]
    public void Time_Equality_SameValues_ShouldBeEqual()
    {
        var t1 = new Time(14, 30);
        var t2 = new Time(14, 30);
        t1.Should().Be(t2);
    }

    [Fact]
    public void Time_Inequality_ShouldDetectDifferentValues()
    {
        var t1 = new Time(14, 30);
        var t2 = new Time(15, 45);
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void Time_Hour_ShouldRetainValue()
    {
        new Time(23, 59).Hour.Should().Be(23);
    }

    [Fact]
    public void Time_Minute_ShouldRetainValue()
    {
        new Time(0, 0).Minute.Should().Be(0);
    }

    #endregion

    #region VersionExtensions Additional Tests

    [Fact]
    public void IsBeta_ExactZeroDotZeroDotOne_ShouldReturnTrue()
    {
        new Version(0, 0, 1, 0).IsBeta().Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 0, 99)]
    [InlineData(2, 3, 99)]
    public void IsBeta_Build99_ShouldReturnTrue(int major, int minor, int build)
    {
        new Version(major, minor, build).IsBeta().Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0, 0, 1, 5)]
    [InlineData(0, 0, 2)]
    [InlineData(1, 0, 1)]
    [InlineData(10, 0, 19041)]
    [InlineData(5, 2, 50)]
    public void IsBeta_NonBeta_ShouldReturnFalse(int major, int minor, int build, int revision = 0)
    {
        new Version(major, minor, build, revision).IsBeta().Should().BeFalse();
    }

    #endregion

    #region MathExtensions Additional Edge Cases

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(50, 1, 50)]
    [InlineData(250, 100, 300)]
    [InlineData(249, 100, 200)]
    public void RoundNearest_WithFactor_ShouldReturnExpected(int value, int factor, int expected)
    {
        MathExtensions.RoundNearest(value, factor).Should().Be(expected);
    }

    [Fact]
    public void RoundNearest_ZeroValue_ShouldReturnZero()
    {
        MathExtensions.RoundNearest(0, 5).Should().Be(0);
    }

    #endregion

    #region UintExtensions Additional Edge Cases

    [Theory]
    [InlineData(0x00000001u, 0, true)]
    [InlineData(0x00000002u, 1, true)]
    [InlineData(0x80000000u, 31, true)]
    [InlineData(0x00000000u, 0, false)]
    [InlineData(0x80000000u, 0, false)]
    public void GetNthBit_ShouldReturnCorrectBit(uint num, int n, bool expected)
    {
        num.GetNthBit(n).Should().Be(expected);
    }

    [Theory]
    [InlineData(0u, 0, true, 1u)]
    [InlineData(0u, 31, true, 0x80000000u)]
    [InlineData(0xFFFFFFFFu, 0, false, 0xFFFFFFFEu)]
    [InlineData(0xFFFFFFFFu, 31, false, 0x7FFFFFFFu)]
    public void SetNthBit_ShouldSetOrClear(uint num, int n, bool state, uint expected)
    {
        num.SetNthBit(n, state).Should().Be(expected);
    }

    [Fact]
    public void ReverseEndianness_RoundTrip_ShouldRestoreOriginal()
    {
        uint original = 0xDEADBEEF;
        original.ReverseEndianness().ReverseEndianness().Should().Be(original);
    }

    #endregion

    #region ListExtensions Edge Cases

    [Fact]
    public void ToArray_LargeList_ShouldCopyAllElements()
    {
        var list = new System.Collections.ArrayList();
        for (var i = 0; i < 100; i++) list.Add(i);
        var result = list.ToArray();
        result.Should().HaveCount(100);
        result[0].Should().Be(0);
        result[99].Should().Be(99);
    }

    #endregion

    #region EnumerableExtensions Edge Cases

    [Fact]
    public void Split_SingleElement_ShouldReturnSingleBatch()
    {
        var list = new List<int> { 1 };
        var result = list.Split(5);
        result.Should().HaveCount(1);
        result.First().Should().HaveCount(1);
    }

    [Fact]
    public void Split_ExactMultiple_ShouldReturnExactBatches()
    {
        var list = new List<int> { 1, 2, 3, 4, 5, 6 };
        var result = list.Split(3);
        result.Should().HaveCount(2);
        result.First().Should().HaveCount(3);
        result.Last().Should().HaveCount(3);
    }

    [Fact]
    public void Split_LargeSize_ShouldReturnSingleBatch()
    {
        var list = new List<int> { 1, 2, 3 };
        var result = list.Split(100);
        result.Should().HaveCount(1);
        result.First().Should().HaveCount(3);
    }

    #endregion

    #region DictionaryExtensions Edge Cases

    [Fact]
    public void AddRange_SingleItem_ShouldAddItem()
    {
        var dict = new Dictionary<int, string>();
        dict.AddRange(new Dictionary<int, string> { [1] = "a" });
        dict.Should().HaveCount(1);
        dict[1].Should().Be("a");
    }

    [Fact]
    public void AsReadOnlyDictionary_ModifyOriginal_ShouldReflectInReadOnly()
    {
        var dict = new Dictionary<string, int> { ["x"] = 1 };
        var ro = dict.AsReadOnlyDictionary();
        dict["y"] = 2;
        ro.Should().ContainKey("y");
        ro["y"].Should().Be(2);
    }

    #endregion
}