using System.Drawing.Imaging;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class LogoInfoFormatExtensionsTests
{
    [Fact]
    public void ImageFormats_WithBmpFlag_ShouldReturnBmp()
    {
        BootLogoFormat format = BootLogoFormat.Bmp;

        var result = format.ImageFormats();

        result.Should().ContainSingle().Which.Should().BeSameAs(ImageFormat.Bmp);
    }

    [Fact]
    public void ImageFormats_WithJpegFlag_ShouldReturnJpeg()
    {
        BootLogoFormat format = BootLogoFormat.Jpeg;

        var result = format.ImageFormats();

        result.Should().ContainSingle().Which.Should().BeSameAs(ImageFormat.Jpeg);
    }

    [Fact]
    public void ImageFormats_WithPngFlag_ShouldReturnPng()
    {
        BootLogoFormat format = BootLogoFormat.Png;

        var result = format.ImageFormats();

        result.Should().ContainSingle().Which.Should().BeSameAs(ImageFormat.Png);
    }

    [Fact]
    public void ImageFormats_WithAllFlags_ShouldReturnAllThreeFormats()
    {
        BootLogoFormat format = BootLogoFormat.Bmp | BootLogoFormat.Jpeg | BootLogoFormat.Png;

        var result = format.ImageFormats();

        result.Should().HaveCount(3);
        result.Should().Contain(ImageFormat.Bmp);
        result.Should().Contain(ImageFormat.Jpeg);
        result.Should().Contain(ImageFormat.Png);
    }

    [Fact]
    public void ImageFormats_WithNoFlags_ShouldReturnEmpty()
    {
        BootLogoFormat format = 0;

        var result = format.ImageFormats();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtensionFilters_WithBmpFlag_ShouldReturnBmpWildcard()
    {
        BootLogoFormat format = BootLogoFormat.Bmp;

        var result = format.ExtensionFilters();

        result.Should().ContainSingle().Which.Should().Be("*.bmp");
    }

    [Fact]
    public void ExtensionFilters_WithPngFlag_ShouldReturnPngWildcard()
    {
        BootLogoFormat format = BootLogoFormat.Png;

        var result = format.ExtensionFilters();

        result.Should().ContainSingle().Which.Should().Be("*.png");
    }

    [Fact]
    public void ExtensionFilters_WithJpegFlag_ShouldReturnBothJpegAndJpgWildcards()
    {
        BootLogoFormat format = BootLogoFormat.Jpeg;

        var result = format.ExtensionFilters();

        result.Should().HaveCount(2);
        result.Should().Contain("*.jpeg");
        result.Should().Contain("*.jpg");
    }

    [Fact]
    public void ExtensionFilters_WithAllFlags_ShouldReturnAllPatterns()
    {
        BootLogoFormat format = BootLogoFormat.Bmp | BootLogoFormat.Jpeg | BootLogoFormat.Png;

        var result = format.ExtensionFilters();

        // Bmp=1, Png=1, Jpeg=2 (*.jpeg + *.jpg) = 4 total
        result.Should().HaveCount(4);
        result.Should().Contain("*.bmp");
        result.Should().Contain("*.png");
        result.Should().Contain("*.jpeg");
        result.Should().Contain("*.jpg");
    }

    [Fact]
    public void ExtensionFilters_WithNoFlags_ShouldReturnEmpty()
    {
        BootLogoFormat format = 0;

        var result = format.ExtensionFilters();

        result.Should().BeEmpty();
    }
}
