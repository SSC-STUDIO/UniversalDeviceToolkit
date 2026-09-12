using System;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class OSDAndSpectrumEnumTests
{
    [Fact]
    public void OsdItem_ShouldHaveTwentyThreeValues()
    {
        Enum.GetValues<OsdItem>().Should().HaveCount(23);
    }

    [Fact]
    public void OsdItem_Fps_ShouldBeZero() => ((int)OsdItem.Fps).Should().Be(0);

    [Fact]
    public void OsdItem_PchFan_ShouldBeLast()
    {
        var values = Enum.GetValues<OsdItem>();
        values.Last().Should().Be(OsdItem.PchFan);
    }

    [Theory]
    [InlineData(OsdItem.Fps, 0)]
    [InlineData(OsdItem.LowFps, 1)]
    [InlineData(OsdItem.FrameTime, 2)]
    [InlineData(OsdItem.CpuFrequency, 3)]
    [InlineData(OsdItem.CpuPCoreFrequency, 4)]
    [InlineData(OsdItem.CpuECoreFrequency, 5)]
    [InlineData(OsdItem.CpuUtilization, 6)]
    [InlineData(OsdItem.CpuTemperature, 7)]
    [InlineData(OsdItem.CpuPower, 8)]
    [InlineData(OsdItem.CpuFan, 9)]
    [InlineData(OsdItem.GpuFrequency, 10)]
    [InlineData(OsdItem.GpuUtilization, 11)]
    [InlineData(OsdItem.GpuTemperature, 12)]
    [InlineData(OsdItem.GpuVramUtilization, 13)]
    [InlineData(OsdItem.GpuVramTemperature, 14)]
    [InlineData(OsdItem.GpuPower, 15)]
    [InlineData(OsdItem.GpuFan, 16)]
    [InlineData(OsdItem.MemoryUtilization, 17)]
    [InlineData(OsdItem.MemoryTemperature, 18)]
    [InlineData(OsdItem.Disk1Temperature, 19)]
    [InlineData(OsdItem.Disk2Temperature, 20)]
    [InlineData(OsdItem.PchTemperature, 21)]
    [InlineData(OsdItem.PchFan, 22)]
    public void OsdItem_ShouldHaveExpectedValues(OsdItem item, int expectedValue)
    {
        ((int)item).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(OsdState.Hidden, 0)]
    [InlineData(OsdState.Show, 1)]
    [InlineData(OsdState.Toggle, 2)]
    public void OsdState_ShouldHaveExpectedValues(OsdState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(HardwareSensorsState.Off, 0)]
    [InlineData(HardwareSensorsState.On, 1)]
    public void HardwareSensorsState_ShouldHaveExpectedValues(HardwareSensorsState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(WinKeyState.Off, 0)]
    [InlineData(WinKeyState.On, 1)]
    public void WinKeyState_ShouldHaveExpectedValues(WinKeyState state, int expectedValue)
    {
        ((int)state).Should().Be(expectedValue);
    }

    [Fact]
    public void WinKeyChanged_ShouldHaveOneValue()
    {
        Enum.GetValues<WinKeyChanged>().Should().HaveCount(1);
    }

    [Fact]
    public void WinKeyChanged_None_ShouldBeZero() => ((int)WinKeyChanged.None).Should().Be(0);

    [Theory]
    [InlineData(SpecialKey.FnR2, 0x0041002A)]
    [InlineData(SpecialKey.SpectrumBacklightOff, 24)]
    [InlineData(SpecialKey.SpectrumBacklight1, 25)]
    [InlineData(SpecialKey.SpectrumBacklight2, 26)]
    [InlineData(SpecialKey.SpectrumBacklight3, 38)]
    [InlineData(SpecialKey.SpectrumPreset1, 32)]
    [InlineData(SpecialKey.SpectrumPreset2, 33)]
    [InlineData(SpecialKey.SpectrumPreset3, 34)]
    [InlineData(SpecialKey.SpectrumPreset4, 35)]
    [InlineData(SpecialKey.SpectrumPreset5, 36)]
    [InlineData(SpecialKey.SpectrumPreset6, 37)]
    [InlineData(SpecialKey.FnN, 42)]
    [InlineData(SpecialKey.FnF4, 62)]
    [InlineData(SpecialKey.FnF8, 63)]
    [InlineData(SpecialKey.WhiteBacklightOff, 64)]
    [InlineData(SpecialKey.WhiteBacklight1, 65)]
    [InlineData(SpecialKey.WhiteBacklight2, 66)]
    public void SpecialKey_ShouldHaveExpectedValues(SpecialKey key, int expectedValue)
    {
        ((int)key).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(RGBKeyboardBacklightPreset.Off, -1)]
    [InlineData(RGBKeyboardBacklightPreset.One, 0)]
    [InlineData(RGBKeyboardBacklightPreset.Two, 1)]
    [InlineData(RGBKeyboardBacklightPreset.Three, 2)]
    [InlineData(RGBKeyboardBacklightPreset.Four, 3)]
    public void RGBKeyboardBacklightPreset_ShouldHaveExpectedValues(RGBKeyboardBacklightPreset preset, int expectedValue)
    {
        ((int)preset).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(RGBKeyboardBacklightSpeed.Slowest, 0)]
    [InlineData(RGBKeyboardBacklightSpeed.Slow, 1)]
    [InlineData(RGBKeyboardBacklightSpeed.Fast, 2)]
    [InlineData(RGBKeyboardBacklightSpeed.Fastest, 3)]
    public void RGBKeyboardBacklightSpeed_ShouldHaveExpectedValues(RGBKeyboardBacklightSpeed speed, int expectedValue)
    {
        ((int)speed).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(SpectrumKeyboardBacklightBrightness.Off, 0)]
    [InlineData(SpectrumKeyboardBacklightBrightness.Low, 1)]
    [InlineData(SpectrumKeyboardBacklightBrightness.Medium, 2)]
    [InlineData(SpectrumKeyboardBacklightBrightness.High, 3)]
    public void SpectrumKeyboardBacklightBrightness_ShouldHaveExpectedValues(SpectrumKeyboardBacklightBrightness brightness, int expectedValue)
    {
        ((int)brightness).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(SpectrumKeyboardBacklightClockwiseDirection.None, 0)]
    [InlineData(SpectrumKeyboardBacklightClockwiseDirection.Clockwise, 1)]
    [InlineData(SpectrumKeyboardBacklightClockwiseDirection.CounterClockwise, 2)]
    public void SpectrumKeyboardBacklightClockwiseDirection_ShouldHaveExpectedValues(SpectrumKeyboardBacklightClockwiseDirection dir, int expectedValue)
    {
        ((int)dir).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(SpectrumLayout.KeyboardOnly, 0)]
    [InlineData(SpectrumLayout.KeyboardAndFront, 1)]
    [InlineData(SpectrumLayout.Full, 2)]
    [InlineData(SpectrumLayout.FullAlternative, 3)]
    public void SpectrumLayout_ShouldHaveExpectedValues(SpectrumLayout layout, int expectedValue)
    {
        ((int)layout).Should().Be(expectedValue);
    }

    [Fact]
    public void FanCurveEntry_ExportToJson_WithNullThresholds_ShouldContainNullThresholds()
    {
        var entry = new FanCurveEntry();
        var json = entry.ExportToJson();
        json.Should().Contain("RampUpThresholds");
        json.Should().Contain("RampDownThresholds");
        json.Should().Contain("null");
    }

    [Fact]
    public void FanCurveEntry_ExportToJson_WithThresholds_ShouldContainThresholds()
    {
        var entry = new FanCurveEntry
        {
            RampUpThresholds = new[] { 45, 55 },
            RampDownThresholds = new[] { 75, 65 }
        };
        var json = entry.ExportToJson();
        json.Should().Contain("RampUpThresholds");
        json.Should().Contain("RampDownThresholds");
    }

    [Fact]
    public void GetDisplayName_Bmp_ShouldReturnBmp()
    {
        var result = BootLogoFormat.Bmp.GetDisplayName();
        result.Should().Be("Bmp");
    }
}
