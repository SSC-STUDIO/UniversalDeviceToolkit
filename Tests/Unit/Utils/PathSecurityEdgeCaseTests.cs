using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

public class PathSecurityEdgeCaseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidFileName_NullEmptyWhitespace_ReturnsFalse(string? name)
    {
        PathSecurity.IsValidFileName(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void IsValidFileName_ReservedDeviceName_ReturnsFalse(string name)
    {
        PathSecurity.IsValidFileName(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("valid.txt")]
    [InlineData("my-file_v2.json")]
    [InlineData("data.csv")]
    public void IsValidFileName_NormalNames_ReturnsTrue(string name)
    {
        PathSecurity.IsValidFileName(name).Should().BeTrue();
    }

    [Fact]
    public void IsValidFileName_PathTraversal_ReturnsFalse()
    {
        PathSecurity.IsValidFileName("../secret.txt").Should().BeFalse();
    }

    [Fact]
    public void IsValidFileName_NullByte_ReturnsFalse()
    {
        PathSecurity.IsValidFileName("file\0.txt").Should().BeFalse();
    }

    [Fact]
    public void IsValidFileName_TrailingDot_ReturnsFalse()
    {
        PathSecurity.IsValidFileName("file.").Should().BeFalse();
    }

    [Fact]
    public void IsValidPluginId_NullEmpty_ReturnsFalse()
    {
        PathSecurity.IsValidPluginId(null).Should().BeFalse();
        PathSecurity.IsValidPluginId("").Should().BeFalse();
    }

    [Fact]
    public void IsValidPluginId_MustStartWithLetter()
    {
        PathSecurity.IsValidPluginId("1plugin").Should().BeFalse();
        PathSecurity.IsValidPluginId("plugin1").Should().BeTrue();
    }

    [Fact]
    public void IsValidPluginId_PathTraversal_ReturnsFalse()
    {
        PathSecurity.IsValidPluginId("../plugin").Should().BeFalse();
    }

    [Fact]
    public void IsValidPluginId_AllowsDashUnderscoreDot()
    {
        PathSecurity.IsValidPluginId("my-plugin.v2").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidRegistryPath_NullEmptyWhitespace_ReturnsFalse(string? path)
    {
        PathSecurity.IsValidRegistryPath(path).Should().BeFalse();
    }

    [Fact]
    public void IsValidRegistryPath_HKLM_Valid()
    {
        PathSecurity.IsValidRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\Lenovo").Should().BeTrue();
    }

    [Fact]
    public void IsValidRegistryPath_HKCU_Valid()
    {
        PathSecurity.IsValidRegistryPath(@"HKCU\Software\Lenovo").Should().BeTrue();
    }

    [Fact]
    public void IsValidRegistryPath_PathTraversal_ReturnsFalse()
    {
        PathSecurity.IsValidRegistryPath(@"HKLM\..\SYSTEM").Should().BeFalse();
    }

    [Fact]
    public void IsValidRegistryPath_NullByte_ReturnsFalse()
    {
        PathSecurity.IsValidRegistryPath("HKLM\0Software\\test\\plugin").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValidDriverPath_NullEmpty_ReturnsFalse(string? path)
    {
        PathSecurity.IsValidDriverPath(path).Should().BeFalse();
    }

    [Fact]
    public void IsValidDriverPath_NonSystemPath_ReturnsFalse()
    {
        PathSecurity.IsValidDriverPath(@"C:\Temp\driver.sys").Should().BeFalse();
    }

    [Fact]
    public void IsValidDriverPath_NonSysExtension_ReturnsFalse()
    {
        PathSecurity.IsValidDriverPath(@"C:\Windows\System32\drivers\test.dll").Should().BeFalse();
    }

    [Fact]
    public void CreateSafeFilePath_NullInputs_ReturnsNull()
    {
        PathSecurity.CreateSafeFilePath(null!, "file.txt").Should().BeNull();
        PathSecurity.CreateSafeFilePath("C:\\test", null).Should().BeNull();
    }

    [Fact]
    public void CreateSafeFilePath_TTraversalAttack_IsSanitizedWithinBase()
    {
        var result = PathSecurity.CreateSafeFilePath("C:\\safe", "..\\\\..\\\\etc\\\\passwd");
        result.Should().NotBeNull();
        result.Should().StartWith("C:\\safe");
    }
}
