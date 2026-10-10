using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class BackendSyncModelsTests
{
    #region AmdWmiCommand Tests

    [Fact]
    public void AmdWmiCommand_Init_ShouldSetAllProperties()
    {
        var cmd = new AmdWmiCommand { Name = "TestCmd", Id = 0xDEADBEEF, IsSet = true };
        cmd.Name.Should().Be("TestCmd");
        cmd.Id.Should().Be(0xDEADBEEF);
        cmd.IsSet.Should().BeTrue();
    }

    [Fact]
    public void AmdWmiCommand_Default_ShouldBeDefault()
    {
        var cmd = new AmdWmiCommand();
        cmd.Name.Should().BeNull();
        cmd.Id.Should().Be(0);
        cmd.IsSet.Should().BeFalse();
    }

    [Fact]
    public void AmdWmiCommand_ToString_ShouldFormatHexId()
    {
        var cmd = new AmdWmiCommand { Name = "MyCmd", Id = 255, IsSet = false };
        cmd.ToString().Should().Contain("0x000000FF");
        cmd.ToString().Should().Contain("MyCmd");
    }

    #endregion

    #region FanSpeedTable Tests

    [Fact]
    public void FanSpeedTable_Constructor_ShouldSetAllFields()
    {
        var table = new FanSpeedTable(1200, 2400, 3600);
        table.CpuFanSpeed.Should().Be(1200);
        table.GpuFanSpeed.Should().Be(2400);
        table.PchFanSpeed.Should().Be(3600);
    }

    [Fact]
    public void FanSpeedTable_ZeroValues_ShouldWork()
    {
        var table = new FanSpeedTable(0, 0, 0);
        table.CpuFanSpeed.Should().Be(0);
        table.GpuFanSpeed.Should().Be(0);
        table.PchFanSpeed.Should().Be(0);
    }

    #endregion

    #region HidDeviceConfig Tests

    [Fact]
    public void HidDeviceConfig_Constructor_ShouldSetAllFields()
    {
        var cfg = new HidDeviceConfig(0x1234, 0x5678, 0x0001, 0x0002, "Test Device");
        cfg.VendorId.Should().Be(0x1234);
        cfg.ProductId.Should().Be(0x5678);
        cfg.UsagePage.Should().Be(0x0001);
        cfg.Usage.Should().Be(0x0002);
        cfg.DisplayName.Should().Be("Test Device");
    }

    #endregion

    #region OverclockingProfile Tests

    [Fact]
    public void OverclockingProfile_Init_ShouldSetFields()
    {
        var profile = new OverclockingProfile
        {
            FMax = 3000,
            CoreValues = new List<double?> { 1.2, null, 1.1 }
        };
        profile.FMax.Should().Be(3000);
        profile.CoreValues.Should().HaveCount(3);
        profile.CoreValues[1].Should().BeNull();
    }

    [Fact]
    public void OverclockingProfile_NullFMax_ShouldBeNull()
    {
        var profile = new OverclockingProfile();
        profile.FMax.Should().BeNull();
        profile.CoreValues.Should().BeNull();
    }

    #endregion

    #region ShutdownInfo Tests

    [Fact]
    public void ShutdownInfo_Init_ShouldSetFields()
    {
        var info = new ShutdownInfo { Status = "OK", AbnormalCount = 3 };
        info.Status.Should().Be("OK");
        info.AbnormalCount.Should().Be(3);
    }

    [Fact]
    public void ShutdownInfo_Default_ShouldHaveDefaults()
    {
        var info = new ShutdownInfo();
        info.Status.Should().BeNull();
        info.AbnormalCount.Should().Be(0);
    }

    #endregion

    #region KeyMap Tests

    [Fact]
    public void KeyMap_Constructor_ShouldSetAllFields()
    {
        var keyCodes = new ushort[2, 3] { { 1, 2, 3 }, { 4, 5, 6 } };
        var additional = new ushort[] { 7, 8 };
        var map = new KeyMap(3, 2, keyCodes, additional);

        map.Width.Should().Be(3);
        map.Height.Should().Be(2);
        map.KeyCodes.Should().BeSameAs(keyCodes);
        map.AdditionalKeyCodes.Should().BeSameAs(additional);
    }

    #endregion
}
