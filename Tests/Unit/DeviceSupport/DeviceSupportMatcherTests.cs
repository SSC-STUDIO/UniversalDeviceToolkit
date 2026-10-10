using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Hardware;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public sealed class DeviceSupportMatcherTests
{
    [Theory]
    [InlineData("83DF", "83DF")]
    [InlineData("83DFCTO1WW", "83DF")]
    [InlineData("LENOVO_MT_83DF_BU_idea_FM_Legion Y9000P IRX9", "83DF")]
    [InlineData("  83de  ", "83DE")]
    [InlineData("", null)]
    [InlineData("Legion Y9000P IRX9", null)]
    public void ExtractMachineTypeToken_ShouldReadLenovoSkuAndBareMtm(string value, string? expected)
    {
        DeviceSupportMatcher.ExtractMachineTypeToken(value).Should().Be(expected);
    }

    [Fact]
    public void Evaluate_WhenSkuContainsMachineType_ShouldPreferExactPack()
    {
        var identity = new DeviceIdentity(
            "windows",
            "X64",
            "LENOVO",
            "Legion Y9000P IRX9",
            "LENOVO_MT_83DF_BU_idea_FM_Legion Y9000P IRX9",
            "BIOS",
            "SN",
            "test")
        {
            MachineType = "LENOVO_MT_83DF_BU_idea_FM_Legion Y9000P IRX9"
        };

        var support = DeviceSupportMatcher.Evaluate(identity, SamplePacks());

        support.DevicePackId.Should().Be("lenovo-legion-pro-5");
    }

    [Fact]
    public void Evaluate_WhenMachineTypeHasCtoSuffix_ShouldStillMatchPack()
    {
        var identity = new DeviceIdentity(
            "windows",
            "X64",
            "LENOVO",
            "Legion Pro 5 16IRX9",
            "Legion Pro 5 16IRX9",
            "BIOS",
            "SN",
            "test")
        {
            MachineType = "83DFCTO1WW"
        };

        var support = DeviceSupportMatcher.Evaluate(identity, SamplePacks());

        support.DevicePackId.Should().Be("lenovo-legion-pro-5");
    }

    [Fact]
    public void Evaluate_WhenOnlyVendorCatchAllBasicExists_ShouldMatchUnknownModel()
    {
        var identity = new DeviceIdentity(
            "windows",
            "X64",
            "ASUSTeK COMPUTER INC.",
            "G614",
            "G614",
            "BIOS",
            "SN",
            "test");

        var support = DeviceSupportMatcher.Evaluate(identity,
        [
            new DevicePackDefinition
            {
                Id = "asus-basic",
                DisplayName = "ASUS Basic",
                Vendor = "ASUSTeK COMPUTER INC.",
                VendorAliases = ["ASUS"],
                Families = ["ROG"],
                ModelKeywords = ["ROG", "TUF"],
                EnabledFeatures = ["lenovo-hardware-controls"],
            },
            new DevicePackDefinition
            {
                Id = DeviceSupportMatcher.GenericBasicPackId,
                DisplayName = "Generic PC Basic",
                Vendor = "*",
            }
        ]);

        support.DevicePackId.Should().Be("asus-basic");
    }

    [Fact]
    public void Evaluate_WhenLineSpecificBasicDoesNotMatchModel_ShouldNotStealVendor()
    {
        var identity = new DeviceIdentity(
            "windows",
            "X64",
            "LENOVO",
            "Unknown Device",
            "Unknown Device",
            "BIOS",
            "SN",
            "test")
        {
            MachineType = "0000"
        };

        var support = DeviceSupportMatcher.Evaluate(identity,
        [
            new DevicePackDefinition
            {
                Id = "lenovo-chromebook-basic",
                DisplayName = "Lenovo Chromebook Basic",
                Vendor = "LENOVO",
                Families = ["Chromebook"],
                ModelKeywords = ["Chromebook"],
                HiddenFeatures = ["lenovo-hardware-controls"],
            },
            new DevicePackDefinition
            {
                Id = DeviceSupportMatcher.GenericBasicPackId,
                DisplayName = "Generic PC Basic",
                Vendor = "*",
            }
        ]);

        support.DevicePackId.Should().Be(DeviceSupportMatcher.GenericBasicPackId);
    }

    [Theory]
    [InlineData("LEN")]
    [InlineData("L")]
    [InlineData("LENOVOLOGY")]
    public void Evaluate_WhenVendorOnlySharesCharacters_ShouldNotMatchHardwarePack(string vendor)
    {
        var identity = DeviceIdentity.Unknown("windows", "test") with
        {
            Vendor = vendor,
            Model = "Legion Pro 7",
            MachineType = "83DE"
        };

        DeviceSupportMatcher.Evaluate(identity, SamplePacks()).DevicePackId
            .Should().Be(DeviceSupportMatcher.GenericBasicPackId);
    }

    [Theory]
    [InlineData("Lenovo Co., Ltd.")]
    [InlineData("LENOVO INCORPORATED")]
    [InlineData("Lenovo Corporation")]
    public void Evaluate_WhenVendorHasLegalSuffix_ShouldStillMatchHardwarePack(string vendor)
    {
        var identity = DeviceIdentity.Unknown("windows", "test") with
        {
            Vendor = vendor,
            Model = "Legion Pro 7",
            MachineType = "83DE"
        };

        DeviceSupportMatcher.Evaluate(identity, SamplePacks()).DevicePackId
            .Should().Be("lenovo-legion-pro-7");
    }

    [Theory]
    [InlineData("Notebook 16IRX9H", "lenovo-model-prefix")]
    [InlineData("16IRX9H", "lenovo-model-prefix")]
    [InlineData("Notebook NOT16IRX9H", DeviceSupportMatcher.GenericBasicPackId)]
    public void Evaluate_WhenModelPrefixOccursInsideAnotherToken_ShouldIgnoreIt(string model, string expectedPack)
    {
        var identity = DeviceIdentity.Unknown("windows", "test") with { Vendor = "LENOVO", Model = model };

        var support = DeviceSupportMatcher.Evaluate(identity,
        [
            new DevicePackDefinition
            {
                Id = "lenovo-model-prefix",
                DisplayName = "Lenovo Model Prefix",
                Vendor = "LENOVO",
                ModelPrefixes = ["16IRX"],
                EnabledFeatures = ["lenovo-hardware-controls"],
            }
        ]);

        support.DevicePackId.Should().Be(expectedPack);
    }

    private static DevicePackDefinition[] SamplePacks() =>
    [
        new()
        {
            Id = "lenovo-legion-pro-7",
            DisplayName = "Lenovo Legion Pro 7",
            Vendor = "LENOVO",
            Families = ["Legion"],
            ModelKeywords = ["Legion Pro 7", "Y9000K"],
            MachineTypes = ["83DE"],
            EnabledFeatures = ["lenovo-hardware-controls"],
        },
        new()
        {
            Id = "lenovo-legion-pro-5",
            DisplayName = "Lenovo Legion Pro 5",
            Vendor = "LENOVO",
            Families = ["Legion"],
            ModelKeywords = ["Legion Pro 5", "Y9000P"],
            MachineTypes = ["83DF"],
            EnabledFeatures = ["lenovo-hardware-controls"],
        },
        new()
        {
            Id = DeviceSupportMatcher.GenericBasicPackId,
            DisplayName = "Generic PC Basic",
            Vendor = "*",
        }
    ];
}
