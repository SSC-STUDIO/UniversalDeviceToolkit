using FluentAssertions;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using Xunit;

namespace UniversalDeviceToolkit.Tests.PackageDownloader;

[Trait("Category", TestCategories.Security)]
public sealed class PackageDownloadSecurityTests
{
    [Theory]
    [InlineData("83DE")]
    [InlineData("82JU")]
    [InlineData("83de")]
    public void IsValidMachineType_WithLenovoMtm_ShouldReturnTrue(string machineType)
    {
        PackageDownloadSecurity.IsValidMachineType(machineType).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../83DE")]
    [InlineData("83DE&x=1")]
    [InlineData("83DE/../x")]
    [InlineData("83DE.xml")]
    public void IsValidMachineType_WithInjection_ShouldReturnFalse(string? machineType)
    {
        PackageDownloadSecurity.IsValidMachineType(machineType).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://download.lenovo.com/pccbbs/file.exe")]
    [InlineData("https://pcsupport.lenovo.com/file.exe")]
    public void IsAllowedPackageDownloadUrl_WithLenovoHttps_ShouldReturnTrue(string url)
    {
        PackageDownloadSecurity.IsAllowedPackageDownloadUrl(url).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://download.lenovo.com/file.exe")]
    [InlineData("https://evil.example/file.exe")]
    [InlineData("https://lenovo.com.evil.example/file.exe")]
    [InlineData("https://user:pass@download.lenovo.com/file.exe")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    public void IsAllowedPackageDownloadUrl_WithDisallowedUrl_ShouldReturnFalse(string? url)
    {
        PackageDownloadSecurity.IsAllowedPackageDownloadUrl(url).Should().BeFalse();
    }

    [Fact]
    public void TryParseSha256Hex_WithValidHash_ShouldSucceed()
    {
        var hash = new string('A', 64);
        PackageDownloadSecurity.TryParseSha256Hex(hash, out var bytes).Should().BeTrue();
        bytes.Should().HaveCount(32);
    }

    [Theory]
    [InlineData("ABCD1234")]
    [InlineData("")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void TryParseSha256Hex_WithInvalidHash_ShouldFail(string hash)
    {
        PackageDownloadSecurity.TryParseSha256Hex(hash, out _).Should().BeFalse();
    }
}
