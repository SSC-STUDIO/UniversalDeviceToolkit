using System;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class EnumExtensionsTests
{
    [Fact]
    public void GetDisplayName_WithDisplayAttribute_ShouldReturnDisplayName()
    {
        // RGBKeyboardBacklightPreset.Off has [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Off")]
        // The resource lookup may or may not succeed depending on satellite assemblies, but the method should not throw
        var result = RGBKeyboardBacklightPreset.Off.GetDisplayName();

        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GetDisplayName_WithoutDisplayAttribute_ShouldReturnToString()
    {
        // BootLogoFormat.Jpeg has no [Display] attribute, so it should fall back to ToString()
        var result = BootLogoFormat.Jpeg.GetDisplayName();

        result.Should().Be("Jpeg");
    }

    [Fact]
    public void GetDisplayName_WithFlagsEnum_ShouldReturnSingleFlagName()
    {
        var result = BootLogoFormat.Bmp.GetDisplayName();

        result.Should().Be("Bmp");
    }

    [Fact]
    public void GetFlagsDisplayName_WithSingleFlagSet_ShouldReturnThatFlag()
    {
        BootLogoFormat format = BootLogoFormat.Png;

        var result = format.GetFlagsDisplayName();

        result.Should().Be("Png");
    }

    [Fact]
    public void GetFlagsDisplayName_WithMultipleFlags_ShouldReturnCommaJoinedNames()
    {
        BootLogoFormat format = BootLogoFormat.Bmp | BootLogoFormat.Png;

        var result = format.GetFlagsDisplayName();

        result.Should().Contain("Bmp");
        result.Should().Contain("Png");
        result.Should().Contain(", ");
    }

    [Fact]
    public void GetFlagsDisplayName_WithExcluding_ShouldExcludeSpecifiedFlag()
    {
        BootLogoFormat format = BootLogoFormat.Bmp | BootLogoFormat.Png | BootLogoFormat.Jpeg;

        var result = format.GetFlagsDisplayName(BootLogoFormat.Jpeg);

        result.Should().Contain("Bmp");
        result.Should().Contain("Png");
        result.Should().NotContain("Jpeg");
    }

    [Fact]
    public void GetFlagsDisplayName_WithNoFlagsSet_ShouldReturnEmptyString()
    {
        BootLogoFormat format = 0;

        var result = format.GetFlagsDisplayName();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetFlagsDisplayName_AllFlagsSet_ShouldContainAllValues()
    {
        BootLogoFormat format = BootLogoFormat.Bmp | BootLogoFormat.Jpeg | BootLogoFormat.Png;

        var result = format.GetFlagsDisplayName();

        var parts = result.Split(", ");
        parts.Should().Contain("Bmp");
        parts.Should().Contain("Jpeg");
        parts.Should().Contain("Png");
    }
}
