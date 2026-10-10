using System.Globalization;
using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Localization;
using UniversalDeviceToolkit.Abstractions.PackageDownloader;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Shared.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Localization;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Guard)]
public sealed class LocalizationContractTests : IDisposable
{
    private readonly string _appDataDirectory;
    private readonly string? _previousOverride;

    public LocalizationContractTests()
    {
        _appDataDirectory = Path.Combine(Path.GetTempPath(), "udt-localization-contract-" + Guid.NewGuid().ToString("N"));
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
    public void ApplicationDataRoot_MatchesFoldersAndLanguageFileDirectory()
    {
        var root = ApplicationDataPaths.GetRoot();
        root.Should().Be(Path.GetFullPath(_appDataDirectory));
        Folders.AppData.Should().Be(root);
        Path.GetDirectoryName(LocalizationRuntime.LanguageFilePath).Should().Be(root);
        ApplicationDataPaths.DirectoryName.Should().Be(AppIdentity.CompactName);
        ApplicationDataPaths.OverrideEnvironmentVariable.Should().Be(Folders.AppDataOverrideEnvironmentVariable);
    }

    [Fact]
    public void GetDisplayName_WithNullCulture_Throws()
    {
        var act = () => LocalizationCatalog.GetDisplayName(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetString_WithNullFallback_ReturnsEmpty()
    {
        var manager = new System.Resources.ResourceManager(typeof(ApplicationDataPaths));
        LocalizationCatalog.GetString(manager, "   ", fallback: null).Should().BeEmpty();
        LocalizationCatalog.GetString(manager, "missing-key", fallback: null).Should().BeEmpty();
    }

    [Fact]
    public void ResourceManagerLocalizer_RejectsNullCulture()
    {
        var manager = new System.Resources.ResourceManager(typeof(ApplicationDataPaths));
        var localizer = new ResourceManagerStringLocalizer(manager);
        var act = () => localizer.CurrentCulture = null!;
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CultureChangedEventArgs_RejectsNullCultures()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        var actPrevious = () => new CultureChangedEventArgs(null!, culture);
        var actCurrent = () => new CultureChangedEventArgs(culture, null!);
        actPrevious.Should().Throw<ArgumentNullException>();
        actCurrent.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PackageInfo_DefaultStringsAreEmptyAndInvalid()
    {
        var package = default(PackageInfo);
        package.Id.Should().BeEmpty();
        package.FileName.Should().BeEmpty();
        package.FileLocation.Should().BeEmpty();
        package.Version.Should().BeEmpty();
        package.IsValid(out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DriverInfo_NullIdentifiersBecomeEmpty()
    {
        var info = new DriverInfo(null!, null!, null, null);
        info.DeviceId.Should().BeEmpty();
        info.HardwareId.Should().BeEmpty();
    }
}
