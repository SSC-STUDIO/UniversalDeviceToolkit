using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public sealed class VerifiedLenovoModelTests
{
    [Theory]
    [InlineData("21ML", "lenovo-thinkpad-basic")]
    [InlineData("21MM", "lenovo-thinkpad-basic")]
    [InlineData("21MC", "lenovo-thinkpad-basic")]
    [InlineData("21MD", "lenovo-thinkpad-basic")]
    [InlineData("21MN", "lenovo-thinkpad-basic")]
    [InlineData("21MQ", "lenovo-thinkpad-basic")]
    [InlineData("21KC", "lenovo-thinkpad-basic")]
    [InlineData("21KD", "lenovo-thinkpad-basic")]
    [InlineData("21MS", "lenovo-thinkbook")]
    [InlineData("21MW", "lenovo-thinkbook")]
    public async Task Evaluate_WhenOnlyVerifiedMachineTypeIsAvailable_ShouldKeepBusinessLaptopInBasicMode(
        string machineType,
        string expectedPack)
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var provider = new CatalogDeviceSupportProvider("verified-model-test", catalog);

        var availability = provider.Evaluate(new MachineInformation
        {
            Vendor = "LENOVO",
            Model = machineType + "CTO1WW",
            MachineType = machineType + "CTO1WW"
        });

        availability.DevicePackId.Should().Be(expectedPack);
        availability.IsBasicMode.Should().BeTrue();
        availability.EnabledFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "power-modes", "fan-curve"]);
    }

    [Theory]
    [InlineData("21ML")]
    [InlineData("21MS")]
    public async Task Evaluate_WhenOtherVendorReusesMachineType_ShouldNotSelectLenovoPack(string machineType)
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var provider = new CatalogDeviceSupportProvider("verified-model-test", catalog);

        var availability = provider.Evaluate(new MachineInformation
        {
            Vendor = "Acme",
            Model = machineType,
            MachineType = machineType
        });

        availability.DevicePackId.Should().Be(CatalogDeviceSupportProvider.GenericBasicPackId);
        availability.IsBasicMode.Should().BeTrue();
    }

    [Fact]
    public async Task SharedCatalog_ShouldMatchBuiltInCatalogWithoutRetiredFeatures()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "resources", "device-packs.json");
        var definitions = JsonSerializer.Deserialize<DevicePack[]>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();

        definitions.Should().NotBeNull();
        definitions.Should().BeEquivalentTo(catalog.DevicePacks, options => options.WithStrictOrdering());
    }
}
