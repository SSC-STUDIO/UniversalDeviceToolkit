using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Security)]
public class PathSecurityTests
{
    #region IsValidFileName

    [Theory]
    [InlineData("test.dll")]
    [InlineData("my-plugin.zip")]
    [InlineData("settings.json")]
    [InlineData("a1b2c3")]
    public void IsValidFileName_WithSafeNames_ShouldReturnTrue(string name)
    {
        PathSecurity.IsValidFileName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidFileName_WithNullOrEmpty_ShouldReturnFalse(string? name)
    {
        PathSecurity.IsValidFileName(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("sub/dir")]
    [InlineData("back\\path")]
    [InlineData("file%NAME")]
    [InlineData("CON")]
    [InlineData("con.dll")]
    [InlineData("AUX")]
    [InlineData("PRN")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("trailing. ")]
    [InlineData("trailingdot.")]
    [InlineData("has\0null")]
    [InlineData("has|pipe")]
    [InlineData("has>redirect")]
    public void IsValidFileName_WithDangerousName_ShouldReturnFalse(string name)
    {
        PathSecurity.IsValidFileName(name).Should().BeFalse();
    }

    #endregion

    #region SanitizeFileName

    [Fact]
    public void SanitizeFileName_WithNull_ShouldReturnUnnamed()
    {
        PathSecurity.SanitizeFileName(null).Should().Be("unnamed");
    }

    [Fact]
    public void SanitizeFileName_WithEmpty_ShouldReturnUnnamed()
    {
        PathSecurity.SanitizeFileName("").Should().Be("unnamed");
    }

    [Fact]
    public void SanitizeFileName_WithSafeName_ShouldReturnUnchanged()
    {
        PathSecurity.SanitizeFileName("test.dll").Should().Be("test.dll");
    }

    [Fact]
    public void SanitizeFileName_WithTraversal_ShouldReplaceDangerousChars()
    {
        var result = PathSecurity.SanitizeFileName("../etc/passwd");
        result.Should().NotContain("..");
        result.Should().NotContain("/");
    }

    [Fact]
    public void SanitizeFileName_WithReservedName_ShouldPrefixWithUnderscore()
    {
        PathSecurity.SanitizeFileName("CON.dll").Should().Be("_CON.dll");
    }

    [Fact]
    public void SanitizeFileName_WithTrailingDot_ShouldTrim()
    {
        PathSecurity.SanitizeFileName("file.").Should().Be("file");
    }

    [Fact]
    public void SanitizeFileName_WithCustomReplacement_ShouldUseIt()
    {
        var result = PathSecurity.SanitizeFileName("file/name", "-");
        result.Should().Contain("-");
        result.Should().NotContain("/");
    }

    #endregion

    #region IsPathWithinAllowedDirectory

    [Fact]
    public void IsPathWithinAllowedDirectory_WithNullPath_ShouldReturnFalse()
    {
        PathSecurity.IsPathWithinAllowedDirectory(null, "/tmp").Should().BeFalse();
    }

    [Fact]
    public void IsPathWithinAllowedDirectory_WithNullBase_ShouldReturnFalse()
    {
        PathSecurity.IsPathWithinAllowedDirectory("/tmp/file", null).Should().BeFalse();
    }

    [Fact]
    public void IsPathWithinAllowedDirectory_WithSubpath_ShouldReturnTrue()
    {
        var basePath = Path.GetTempPath();
        var filePath = Path.Combine(basePath, "sub", "file.txt");
        PathSecurity.IsPathWithinAllowedDirectory(filePath, basePath).Should().BeTrue();
    }

    [Fact]
    public void IsPathWithinAllowedDirectory_WithTraversal_ShouldReturnFalse()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "sandbox");
        var attackPath = Path.Combine(basePath, "..", "..", "etc", "passwd");
        PathSecurity.IsPathWithinAllowedDirectory(attackPath, basePath).Should().BeFalse();
    }

    [Fact]
    public void IsPathWithinAllowedDirectory_WithAbsoluteEscape_ShouldReturnFalse()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "sandbox");
        PathSecurity.IsPathWithinAllowedDirectory("/etc/passwd", basePath).Should().BeFalse();
    }

    #endregion

    #region CreateSafeFilePath

    [Fact]
    public void CreateSafeFilePath_WithNullBase_ShouldReturnNull()
    {
        PathSecurity.CreateSafeFilePath(null!, "file.txt").Should().BeNull();
    }

    [Fact]
    public void CreateSafeFilePath_WithNullFileName_ShouldReturnNull()
    {
        PathSecurity.CreateSafeFilePath("/tmp", null).Should().BeNull();
    }

    [Fact]
    public void CreateSafeFilePath_WithSafeName_ShouldReturnPath()
    {
        var basePath = Path.GetTempPath();
        var result = PathSecurity.CreateSafeFilePath(basePath, "test.dll");
        result.Should().NotBeNull();
        result!.Should().StartWith(basePath);
    }

    [Fact]
    public void CreateSafeFilePath_WithTraversalName_ShouldSanitizePathTraversal()
    {
        var basePath = Path.GetTempPath();
        // Sanitization replaces '/' with '_', neutralizing the traversal
        var result = PathSecurity.CreateSafeFilePath(basePath, "../../../etc/passwd");
        result.Should().NotBeNull();
        result.Should().NotContain("..");
        result.Should().StartWith(basePath);
    }

    #endregion

    #region IsValidPluginId

    [Theory]
    [InlineData("my-plugin")]
    [InlineData("custom_mouse")]
    [InlineData("plugin.v2")]
    [InlineData("A1")]
    public void IsValidPluginId_WithValidId_ShouldReturnTrue(string id)
    {
        PathSecurity.IsValidPluginId(id).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidPluginId_WithNullOrEmpty_ShouldReturnFalse(string? id)
    {
        PathSecurity.IsValidPluginId(id).Should().BeFalse();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("has space")]
    [InlineData("1starts-with-digit")]
    [InlineData("has@special")]
    public void IsValidPluginId_WithInvalidId_ShouldReturnFalse(string id)
    {
        PathSecurity.IsValidPluginId(id).Should().BeFalse();
    }

    #endregion

    #region IsValidDirectoryPath

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidDirectoryPath_WithNullOrEmpty_ShouldReturnFalse(string? path)
    {
        PathSecurity.IsValidDirectoryPath(path).Should().BeFalse();
    }

    [Fact]
    public void IsValidDirectoryPath_WithTraversalPattern_ShouldReturnFalse()
    {
        PathSecurity.IsValidDirectoryPath("/tmp/../etc").Should().BeFalse();
    }

    [Fact]
    public void IsValidDirectoryPath_WithEnvVarPattern_ShouldReturnFalse()
    {
        PathSecurity.IsValidDirectoryPath("%APPDATA%\\test").Should().BeFalse();
    }

    #endregion

    #region IsValidRegistryPath

    [Theory]
    [InlineData("HKEY_CURRENT_USER\\Software\\Test")]
    [InlineData("HKLM\\SOFTWARE\\Test")]
    [InlineData("\\HKCR\\.txt")]
    public void IsValidRegistryPath_WithValidPath_ShouldReturnTrue(string path)
    {
        PathSecurity.IsValidRegistryPath(path).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsValidRegistryPath_WithNullOrEmpty_ShouldReturnFalse(string? path)
    {
        PathSecurity.IsValidRegistryPath(path).Should().BeFalse();
    }

    [Theory]
    [InlineData("HKEY_DYN_DATA\\Test")]
    [InlineData("HKCU\\..\\..\\escape")]
    public void IsValidRegistryPath_WithInvalidPath_ShouldReturnFalse(string path)
    {
        PathSecurity.IsValidRegistryPath(path).Should().BeFalse();
    }

    #endregion
}
