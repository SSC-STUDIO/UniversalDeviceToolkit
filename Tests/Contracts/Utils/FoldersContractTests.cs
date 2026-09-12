using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Shared.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Security)]
public sealed class FoldersContractTests : IDisposable
{
    private readonly string _appDataDirectory;
    private readonly string? _previousOverride;

    public FoldersContractTests()
    {
        _appDataDirectory = Path.Combine(Path.GetTempPath(), "udt-folders-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appDataDirectory);
        _previousOverride = Environment.GetEnvironmentVariable(ApplicationDataPaths.OverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(ApplicationDataPaths.OverrideEnvironmentVariable, _appDataDirectory);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ApplicationDataPaths.OverrideEnvironmentVariable, _previousOverride);
        try
        {
            if (Directory.Exists(_appDataDirectory))
                Directory.Delete(_appDataDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void GetAppDataSubdirectory_WithSafeName_CreatesChildDirectory()
    {
        var path = Folders.GetAppDataSubdirectory("cache");
        path.Should().Be(Path.Combine(Folders.AppData, "cache"));
        Directory.Exists(path).Should().BeTrue();
        PathSecurity.IsPathWithinAllowedDirectory(path, Folders.AppData).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("C:\\Windows")]
    public void GetAppDataSubdirectory_WithUnsafeName_Throws(string? subdirectory)
    {
        var act = () => Folders.GetAppDataSubdirectory(subdirectory!);
        act.Should().Throw<ArgumentException>();
    }
}
