using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.CLI.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Network;

[Trait("Category", TestCategories.Unit)]
public class PipeNameAndAdditionalEdgeCaseTests
{
    #region Constants.GetPipeName Tests

    [Fact]
    public void GetPipeName_Null_ShouldReturnDefault()
    {
        Constants.GetPipeName(null).Should().Be(Constants.DEFAULT_PIPE_NAME);
    }

    [Fact]
    public void GetPipeName_EmptyString_ShouldReturnDefault()
    {
        Constants.GetPipeName("").Should().Be(Constants.DEFAULT_PIPE_NAME);
    }

    [Fact]
    public void GetPipeName_Whitespace_ShouldReturnDefault()
    {
        Constants.GetPipeName("   ").Should().Be(Constants.DEFAULT_PIPE_NAME);
    }

    [Fact]
    public void GetPipeName_ValidPath_ShouldReturnDifferentName()
    {
        var result = Constants.GetPipeName(@"C:\Users\Test\AppData\Local\UDT");
        result.Should().NotBe(Constants.DEFAULT_PIPE_NAME);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetPipeName_SamePathTwice_ShouldReturnSameName()
    {
        var path = @"C:\Users\Test\AppData\Local\UDT";
        var a = Constants.GetPipeName(path);
        var b = Constants.GetPipeName(path);
        a.Should().Be(b);
    }

    [Fact]
    public void GetPipeName_DifferentPaths_ShouldReturnDifferentNames()
    {
        var a = Constants.GetPipeName(@"C:\Path\A");
        var b = Constants.GetPipeName(@"C:\Path\B");
        a.Should().NotBe(b);
    }

    [Fact]
    public void GetPipeName_WithBaseName_ShouldApplySameIsolationSuffixToBothBases()
    {
        var path = @"C:\Users\Test\AppData\Local\UDT";
        var legacy = Constants.GetPipeName(path, Constants.DEFAULT_PIPE_NAME);
        var preferred = Constants.GetPipeName(path, Constants.PREFERRED_PIPE_NAME);

        legacy.Should().StartWith(Constants.DEFAULT_PIPE_NAME + "-");
        preferred.Should().StartWith(Constants.PREFERRED_PIPE_NAME + "-");

        var legacySuffix = legacy[Constants.DEFAULT_PIPE_NAME.Length..];
        var preferredSuffix = preferred[Constants.PREFERRED_PIPE_NAME.Length..];
        preferredSuffix.Should().Be(legacySuffix);
    }

    [Fact]
    public void GetServerPipeNames_WithIsolation_ShouldSuffixBothNames()
    {
        var path = @"C:\Users\Test\AppData\Local\UDT";
        var names = Constants.GetServerPipeNames(path);

        names.Should().HaveCount(2);
        names[0].Should().StartWith(Constants.DEFAULT_PIPE_NAME + "-");
        names[1].Should().StartWith(Constants.PREFERRED_PIPE_NAME + "-");
    }

    [Fact]
    public void GetClientPipeNames_WithIsolation_ShouldPreferUdtThenLegacy()
    {
        var path = @"C:\Users\Test\AppData\Local\UDT";
        var names = Constants.GetClientPipeNames(path);

        names.Should().HaveCount(2);
        names[0].Should().StartWith(Constants.PREFERRED_PIPE_NAME + "-");
        names[1].Should().StartWith(Constants.DEFAULT_PIPE_NAME + "-");
    }

    #endregion

    #region Additional NotificationType Coverage

    [Theory]
    [InlineData(NotificationType.ACAdapterConnectedLowWattage)]
    [InlineData(NotificationType.AutomationNotification)]
    public void NotificationType_AdditionalValues_ShouldBeDefined(NotificationType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void NotificationType_AllValues_ShouldBeDefined()
    {
        foreach (var value in Enum.GetValues<NotificationType>())
            Enum.IsDefined(value).Should().BeTrue();
    }

    #endregion

    #region Additional RGBColor Edge Cases

    [Fact]
    public void RGBColor_StaticGreen_ShouldHaveExpectedValues()
    {
        RGBColor.Green.R.Should().Be(142);
        RGBColor.Green.G.Should().Be(255);
        RGBColor.Green.B.Should().Be(0);
    }

    [Fact]
    public void RGBColor_StaticRed_ShouldHaveExpectedValues()
    {
        RGBColor.Red.R.Should().Be(255);
        RGBColor.Red.G.Should().Be(0);
        RGBColor.Red.B.Should().Be(0);
    }

    [Fact]
    public void RGBColor_StaticPink_ShouldHaveExpectedValues()
    {
        RGBColor.Pink.R.Should().Be(186);
        RGBColor.Pink.G.Should().Be(0);
        RGBColor.Pink.B.Should().Be(255);
    }

    [Fact]
    public void RGBColor_StaticTeal_ShouldHaveExpectedValues()
    {
        RGBColor.Teal.R.Should().Be(0);
        RGBColor.Teal.G.Should().Be(212);
        RGBColor.Teal.B.Should().Be(255);
    }

    [Fact]
    public void RGBColor_StaticWhite_ShouldHaveExpectedValues()
    {
        RGBColor.White.R.Should().Be(255);
        RGBColor.White.G.Should().Be(255);
        RGBColor.White.B.Should().Be(255);
    }

    [Fact]
    public void RGBColor_StaticPurple_ShouldHaveExpectedValues()
    {
        RGBColor.Purple.R.Should().Be(101);
        RGBColor.Purple.G.Should().Be(0);
        RGBColor.Purple.B.Should().Be(255);
    }

    [Fact]
    public void RGBColor_Custom_ShouldRetainValues()
    {
        var color = new RGBColor(10, 20, 30);
        color.R.Should().Be(10);
        color.G.Should().Be(20);
        color.B.Should().Be(30);
    }

    [Fact]
    public void RGBColor_MaxValues_ShouldWork()
    {
        var color = new RGBColor(byte.MaxValue, byte.MaxValue, byte.MaxValue);
        color.R.Should().Be(255);
        color.G.Should().Be(255);
        color.B.Should().Be(255);
    }

    [Fact]
    public void RGBColor_Equality_SameValues_ShouldBeEqual()
    {
        var a = new RGBColor(100, 150, 200);
        var b = new RGBColor(100, 150, 200);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void RGBColor_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new RGBColor(100, 150, 200);
        var b = new RGBColor(100, 150, 201);
        a.Equals(b).Should().BeFalse();
    }

    #endregion

    #region Additional SensorData Edge Cases

    [Fact]
    public void SensorData_Empty_Static_ShouldBeSameAsDefault()
    {
        var empty = SensorData.Empty;
        empty.Utilization.Should().Be(-1);
        empty.Voltage.Should().Be(0);
    }

    [Fact]
    public void SensorData_WithMinMax_PreservesAllOriginalFields()
    {
        var original = new SensorData(
            utilization: 50, maxUtilization: 100,
            coreClock: 3000, maxCoreClock: 5000,
            memoryClock: 1200, maxMemoryClock: 2000,
            temperature: 70, maxTemperature: 100,
            wattage: 65, voltage: 1.2,
            fanSpeed: 2500, maxFanSpeed: 5000);

        var result = original.WithMinMax(0.5, 1.5, 40, 90);

        result.Utilization.Should().Be(50);
        result.MaxUtilization.Should().Be(100);
        result.CoreClock.Should().Be(3000);
        result.MaxCoreClock.Should().Be(5000);
        result.MemoryClock.Should().Be(1200);
        result.MaxMemoryClock.Should().Be(2000);
        result.Temperature.Should().Be(70);
        result.Wattage.Should().Be(65);
        result.Voltage.Should().Be(1.2);
        result.FanSpeed.Should().Be(2500);
        result.MaxFanSpeed.Should().Be(5000);
    }

    #endregion

    #region Additional WindowSize Tests

    [Fact]
    public void WindowSize_Constructor_ShouldSetProperties()
    {
        var size = new WindowSize(1920.0, 1080.0);
        size.Width.Should().Be(1920.0);
        size.Height.Should().Be(1080.0);
    }

    [Fact]
    public void WindowSize_ZeroValues_ShouldWork()
    {
        var size = new WindowSize(0.0, 0.0);
        size.Width.Should().Be(0.0);
        size.Height.Should().Be(0.0);
    }

    #endregion

    #region Additional GPUOverclockInfo Edge Cases

    [Fact]
    public void GPUOverclockInfo_Equality_BoxedSameType_ShouldWork()
    {
        var a = new GPUOverclockInfo(100, 200);
        object b = new GPUOverclockInfo(100, 200);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void GPUOverclockInfo_GetHashCode_DifferentValues_ShouldDiffer()
    {
        var a = new GPUOverclockInfo(100, 200);
        var b = new GPUOverclockInfo(101, 201);
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    #endregion

    #region Additional DpiScale Edge Cases

    [Fact]
    public void DpiScale_Equality_BoxedSameType_ShouldWork()
    {
        var a = new DpiScale(100);
        object b = new DpiScale(100);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void DpiScale_Equality_BoxedDifferentType_ShouldReturnFalse()
    {
        var a = new DpiScale(100);
        a.Equals("not a DpiScale").Should().BeFalse();
    }

    #endregion

    #region Additional RefreshRate Edge Cases

    [Fact]
    public void RefreshRate_Equality_BoxedSameType_ShouldWork()
    {
        var a = new RefreshRate(60);
        object b = new RefreshRate(60);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void RefreshRate_Equality_BoxedDifferentType_ShouldReturnFalse()
    {
        var a = new RefreshRate(60);
        a.Equals("not a RefreshRate").Should().BeFalse();
    }

    #endregion

    #region Additional Resolution Edge Cases

    [Fact]
    public void Resolution_Equality_BoxedSameType_ShouldWork()
    {
        var a = new Resolution(1920, 1080);
        object b = new Resolution(1920, 1080);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Resolution_Equality_BoxedDifferentType_ShouldReturnFalse()
    {
        var a = new Resolution(1920, 1080);
        a.Equals("not a Resolution").Should().BeFalse();
    }

    [Fact]
    public void Resolution_CompareTo_SameValues_ShouldBeZero()
    {
        var a = new Resolution(1920, 1080);
        var b = new Resolution(1920, 1080);
        a.CompareTo(b).Should().Be(0);
    }

    #endregion

    #region Additional HardwareId Edge Cases

    [Fact]
    public void HardwareId_Empty_ShouldHaveEmptyStrings()
    {
        HardwareId.Empty.Vendor.Should().BeNullOrEmpty();
        HardwareId.Empty.Device.Should().BeNullOrEmpty();
    }

    [Fact]
    public void HardwareId_Equality_BoxedSameType_ShouldWork()
    {
        var a = new HardwareId("8086", "1234");
        object b = new HardwareId("8086", "1234");
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void HardwareId_Equality_BoxedDifferentType_ShouldReturnFalse()
    {
        var a = new HardwareId("8086", "1234");
        a.Equals("not a HardwareId").Should().BeFalse();
    }

    #endregion

    #region Additional Time Edge Cases

    [Fact]
    public void Time_Midnight_ShouldWork()
    {
        var time = new Time(0, 0);
        time.Hour.Should().Be(0);
        time.Minute.Should().Be(0);
    }

    [Fact]
    public void Time_MaxValues_ShouldWork()
    {
        var time = new Time(23, 59);
        time.Hour.Should().Be(23);
        time.Minute.Should().Be(59);
    }

    #endregion
}

