using FluentAssertions;
using System.Xml.Linq;
using UniversalDeviceToolkit.Tests;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class CrossPlatformProjectTests
{
    private static readonly string RepositoryRoot = RepositoryPaths.FindRoot();

    [Fact]
    public void CrossPlatformCli_ShouldTargetPlainNet10()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot, "Apps/CrossPlatformCLI", "UniversalDeviceToolkit.CrossPlatform.csproj"));

        project.Descendants("TargetFramework").Single().Value.Should().Be("net10.0");
        var projectText = project.ToString();
        projectText.Contains("windows10", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        projectText.Should().Contain("UniversalDeviceToolkit.Platform.Windows.Core");
        projectText.Contains("UseWPF", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        projectText.Contains("RuntimeIdentifier", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public void CrossPlatformCli_ShouldAvoidWindowsOnlyApis()
    {
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "Apps", "CrossPlatformCLI"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        source.Should().NotContain("System.Management");
        source.Should().NotContain("Microsoft.Win32");
        source.Should().NotContain("Windows.Win32");
        source.Should().NotContain("System.Windows");
        source.Should().NotContain("NamedPipe");
    }

    [Fact]
    public void CrossPlatformCliAssetScript_ShouldPackageLaunchers()
    {
        var scriptText = File.ReadAllText(Path.Combine(RepositoryRoot, "Scripts", "Build-CrossPlatformCliAsset.ps1"));

        scriptText.Should().Contain("Write-CrossPlatformLaunchers");
        scriptText.Should().Contain("'udt.cmd'");
        scriptText.Should().Contain("'README.txt'");
        scriptText.Should().Contain("dotnet \"$SCRIPT_DIR/udt.dll\" \"$@\"");
        scriptText.Should().Contain("dotnet \"%~dp0udt.dll\" %*");
    }
}
