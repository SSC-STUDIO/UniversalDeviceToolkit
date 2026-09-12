using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class DeviceSupportModelsTests
{
    #region DevicePack Tests

    [Fact]
    public void DevicePack_Init_ShouldSetAllProperties()
    {
        var pack = new DevicePack
        {
            Id = "lenovo-legion-y9000p",
            DisplayName = "Lenovo Legion Y9000P",
            Vendor = "Lenovo",
            VendorAliases = new List<string> { "LenovoPC" },
            Families = new List<string> { "Legion" },
            ModelPrefixes = new List<string> { "82JD", "82JU" },
            ModelKeywords = new List<string> { "Y9000P" },
            MachineTypes = new List<string> { "RTCN5M" },
            EnabledFeatures = new List<string> { "GodMode", "FnLock" },
            HiddenFeatures = new List<string> { "TestFeature" }
        };

        pack.Id.Should().Be("lenovo-legion-y9000p");
        pack.DisplayName.Should().Be("Lenovo Legion Y9000P");
        pack.Vendor.Should().Be("Lenovo");
        pack.VendorAliases.Should().Contain("LenovoPC");
        pack.Families.Should().Contain("Legion");
        pack.ModelPrefixes.Should().HaveCount(2);
        pack.ModelKeywords.Should().Contain("Y9000P");
        pack.MachineTypes.Should().Contain("RTCN5M");
        pack.EnabledFeatures.Should().Contain("GodMode");
        pack.HiddenFeatures.Should().Contain("TestFeature");
    }

    [Fact]
    public void DevicePack_DefaultCollections_ShouldBeEmpty()
    {
        var pack = new DevicePack { Id = "test", DisplayName = "Test", Vendor = "V" };
        pack.VendorAliases.Should().BeEmpty();
        pack.Families.Should().BeEmpty();
        pack.ModelPrefixes.Should().BeEmpty();
        pack.ModelKeywords.Should().BeEmpty();
        pack.MachineTypes.Should().BeEmpty();
        pack.EnabledFeatures.Should().BeEmpty();
        pack.HiddenFeatures.Should().BeEmpty();
    }

    #endregion

    #region DeviceFeatureAvailability Tests

    [Fact]
    public void DeviceFeatureAvailability_Supported_ShouldBeTrue()
    {
        var avail = new DeviceFeatureAvailability
        {
            IsSupported = true,
            DevicePackId = "test-pack",
            EnabledFeatures = new List<string> { "FnLock" },
            HiddenFeatures = new List<string>()
        };

        avail.IsSupported.Should().BeTrue();
        avail.IsBasicMode.Should().BeFalse();
        avail.DevicePackId.Should().Be("test-pack");
        avail.EnabledFeatures.Should().Contain("FnLock");
    }

    [Fact]
    public void DeviceFeatureAvailability_NotSupported_IsBasicMode()
    {
        var avail = new DeviceFeatureAvailability { IsSupported = false };
        avail.IsBasicMode.Should().BeTrue();
    }

    [Fact]
    public void DeviceFeatureAvailability_Default_ShouldHaveEmptyCollections()
    {
        var avail = new DeviceFeatureAvailability();
        avail.IsSupported.Should().BeFalse();
        avail.DevicePackId.Should().BeNull();
        avail.EnabledFeatures.Should().BeEmpty();
        avail.HiddenFeatures.Should().BeEmpty();
    }

    #endregion

    #region DeviceSupportCatalog Tests

    [Fact]
    public void DeviceSupportCatalog_DefaultValues_ShouldBeCorrect()
    {
        var catalog = new DeviceSupportCatalog();
        catalog.SchemaVersion.Should().Be(1);
        catalog.AppVersion.Should().Be("0.0.0");
        catalog.DevicePacks.Should().BeEmpty();
    }

    [Fact]
    public void DeviceSupportCatalog_Init_ShouldSetAllProperties()
    {
        var packs = new List<DevicePack>
        {
            new() { Id = "pack1", DisplayName = "Pack 1", Vendor = "V" }
        };

        var catalog = new DeviceSupportCatalog
        {
            SchemaVersion = 2,
            AppVersion = "1.0.0",
            DevicePacks = packs
        };

        catalog.SchemaVersion.Should().Be(2);
        catalog.AppVersion.Should().Be("1.0.0");
        catalog.DevicePacks.Should().HaveCount(1);
    }

    #endregion
}
