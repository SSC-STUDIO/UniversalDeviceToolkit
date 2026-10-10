using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Hardware;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public sealed class DeviceSupportSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evaluate_WhenExactModelTiesWithBroadFamilyKeyword_ShouldPreferModelInEitherCatalogOrder(bool reverseOrder)
    {
        var legacy = new DevicePackDefinition
        {
            Id = "lenovo-legacy",
            DisplayName = "Lenovo Legacy",
            Vendor = "LENOVO",
            Families = ["Legion"],
            ModelKeywords = ["Legion"]
        };
        var pro = legacy with { Id = "lenovo-pro-5", DisplayName = "Lenovo Pro 5", ModelKeywords = ["R9000P"] };
        var identity = DeviceIdentity.Unknown("windows", "test") with { Vendor = "LENOVO", Model = "Lenovo LEGION R9000P ACH16" };

        var support = DeviceSupportMatcher.Evaluate(identity, reverseOrder ? [legacy, pro] : [pro, legacy]);

        support.DevicePackId.Should().Be("lenovo-pro-5");
    }

    [Theory]
    [InlineData("Lenovo LEGION R9000P ACH16")]
    [InlineData("Legion Y9000P 16IRX10")]
    public async Task Evaluate_WhenCatalogContainsBroadLegacyFamily_ShouldRetainExplicitPro5HardwareSupport(string model)
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var provider = new CatalogDeviceSupportProvider("model-regression-test", catalog);

        var availability = provider.Evaluate(new MachineInformation { Vendor = "LENOVO", Model = model, MachineType = "83ZZ" });

        availability.DevicePackId.Should().Be("lenovo-legion-pro-5");
        availability.IsSupported.Should().BeTrue();
        availability.EnabledFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Fact]
    public async Task Evaluate_WhenCatalogContainsSameExplicitModelInDifferentPacks_ShouldKeepAmbiguityInBasicMode()
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var provider = new CatalogDeviceSupportProvider("model-ambiguity-test", catalog);

        var availability = provider.Evaluate(new MachineInformation { Vendor = "LENOVO", Model = "Legion Slim 7 16APH8", MachineType = "83ZZ" });

        availability.DevicePackId.Should().Be(CatalogDeviceSupportProvider.GenericBasicPackId);
        availability.IsBasicMode.Should().BeTrue();
        availability.EnabledFeatures.Should().NotContain("lenovo-hardware-controls");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evaluate_WhenDifferentPacksHaveSamePrefix_ShouldUseBasicModeInEitherCatalogOrder(bool reverseOrder)
    {
        var first = new DevicePackDefinition
        {
            Id = "lenovo-pro-5",
            DisplayName = "Lenovo Pro 5",
            Vendor = "LENOVO",
            ModelPrefixes = ["16IRX"],
            EnabledFeatures = ["lenovo-hardware-controls"]
        };
        var second = first with { Id = "lenovo-pro-7", DisplayName = "Lenovo Pro 7" };
        var identity = DeviceIdentity.Unknown("windows", "test") with { Vendor = "LENOVO", Model = "16IRX9H" };

        var support = DeviceSupportMatcher.Evaluate(identity, reverseOrder ? [second, first] : [first, second]);

        support.DevicePackId.Should().Be(DeviceSupportMatcher.GenericBasicPackId);
        support.IsHardwareControlAvailable.Should().BeFalse();
        support.Reason.Should().Contain("unambiguous");
    }

    [Fact]
    public void Evaluate_WhenMachineTypeResolvesPrefixTie_ShouldKeepExactModelSupport()
    {
        var pack = new DevicePackDefinition
        {
            Id = "lenovo-pro-5",
            DisplayName = "Lenovo Pro 5",
            Vendor = "LENOVO",
            ModelPrefixes = ["16IRX"],
            MachineTypes = ["83DF"]
        };
        var identity = DeviceIdentity.Unknown("windows", "test") with
        {
            Vendor = "LENOVO",
            Model = "16IRX9H",
            MachineType = "83DF"
        };

        var support = DeviceSupportMatcher.Evaluate(identity,
        [
            pack with { Id = "lenovo-pro-7", MachineTypes = ["83DE"] },
            pack
        ]);

        support.DevicePackId.Should().Be("lenovo-pro-5");
    }

    [Theory]
    [InlineData("Legion 7", "lenovo-series-7")]
    [InlineData("Legion 7i", "lenovo-series-7")]
    [InlineData("Legion 70", DeviceSupportMatcher.GenericBasicPackId)]
    public void Evaluate_WhenNumericModelSeriesHasExtraDigits_ShouldNotMatchAnotherSeries(string model, string expectedPack)
    {
        var identity = DeviceIdentity.Unknown("windows", "test") with { Vendor = "LENOVO", Model = model };

        var support = DeviceSupportMatcher.Evaluate(identity,
        [
            new DevicePackDefinition
            {
                Id = "lenovo-series-7",
                DisplayName = "Lenovo Series 7",
                Vendor = "LENOVO",
                ModelKeywords = ["Legion 7"]
            }
        ]);

        support.DevicePackId.Should().Be(expectedPack);
    }

    [Theory]
    [InlineData("Default string")]
    [InlineData("System manufacturer")]
    [InlineData("To Be Filled By O.E.M.")]
    public async Task Evaluate_WhenPrimaryManufacturerIsPlaceholder_ShouldUseIdentifyingManufacturer(string placeholder)
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var provider = new CatalogDeviceSupportProvider("safety-test", catalog);

        var availability = provider.Evaluate(new MachineInformation
        {
            Vendor = placeholder,
            Model = "21MLCTO1WW",
            MachineType = "21ML",
            Hardware = new HardwareInventory
            {
                ComputerSystem = new() { Manufacturer = "LENOVO", Model = "21MLCTO1WW" }
            }
        });

        availability.DevicePackId.Should().Be("lenovo-thinkpad-basic");
        availability.IsBasicMode.Should().BeTrue();
    }

    [Fact]
    public async Task Evaluate_WhenPrimaryManufacturerIsGenuine_ShouldKeepItOverBaseBoardManufacturer()
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var provider = new CatalogDeviceSupportProvider("safety-test", catalog);

        var availability = provider.Evaluate(new MachineInformation
        {
            Vendor = "Acme",
            Model = "21ML",
            MachineType = "21ML",
            Hardware = new HardwareInventory { BaseBoard = new() { Manufacturer = "LENOVO" } }
        });

        availability.DevicePackId.Should().Be(CatalogDeviceSupportProvider.GenericBasicPackId);
        availability.IsBasicMode.Should().BeTrue();
    }
}
