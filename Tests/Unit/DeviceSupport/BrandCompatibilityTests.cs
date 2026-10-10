using FluentAssertions;
using UniversalDeviceToolkit.Lib.Branding;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
public class BrandCompatibilityTests
{
    [Fact]
    public void ProductDisplayName_IsNonEmpty()
    {
        BrandCompatibility.ProductDisplayName.Should().NotBeNullOrWhiteSpace();
        BrandCompatibility.ProductDisplayName.Should().Be("Universal Device Toolkit");
    }

    [Fact]
    public void ProductCompactName_IsNonEmpty()
    {
        BrandCompatibility.ProductCompactName.Should().NotBeNullOrWhiteSpace();
        BrandCompatibility.ProductCompactName.Should().Be("UniversalDeviceToolkit");
    }

    [Fact]
    public void LegacyProductDisplayName_IsNonEmpty()
    {
        BrandCompatibility.LegacyProductDisplayName.Should().NotBeNullOrWhiteSpace();
        BrandCompatibility.LegacyProductDisplayName.Should().Be("Lenovo Legion Toolkit");
    }

    [Fact]
    public void LegacyAssemblyLib_IsLenovoLegionToolkitLib()
    {
        BrandCompatibility.LegacyAssemblyLib.Should().Be("LenovoLegionToolkit.Lib");
        BrandCompatibility.LegacyAssemblyLib.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PreferredAssemblyLib_IsPrimaryUdtAssemblyName()
    {
        BrandCompatibility.PreferredAssemblyLib.Should().Be("UniversalDeviceToolkit.Lib");
    }
}
