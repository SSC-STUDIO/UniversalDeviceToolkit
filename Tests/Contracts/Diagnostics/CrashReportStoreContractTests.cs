using FluentAssertions;
using UniversalDeviceToolkit.Abstractions.Platform;
using UniversalDeviceToolkit.Shared.Diagnostics;
using UniversalDeviceToolkit.Shared.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Diagnostics;

[Collection(TestCollections.ProcessState)]
[Trait("Category", TestCategories.Security)]
public sealed class CrashReportStoreContractTests : IDisposable
{
    private readonly string _appDataDirectory;
    private readonly string? _previousOverride;

    public CrashReportStoreContractTests()
    {
        _appDataDirectory = Path.Combine(Path.GetTempPath(), "udt-crash-contract-" + Guid.NewGuid().ToString("N"));
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
    public void CrashReportDirectory_UsesCurrentAppDataRoot()
    {
        CrashReportStore.CrashReportDirectory.Should().Be(Path.Combine(Folders.AppData, "crash_reports"));
    }

    [Fact]
    public void Save_WritesReportUnderAppData()
    {
        var path = CrashReportStore.Save(new InvalidOperationException("contract"), "contract-test");
        path.Should().NotBeNullOrWhiteSpace();
        PathSecurity.IsPathWithinAllowedDirectory(path, Folders.AppData).Should().BeTrue();
        File.Exists(path).Should().BeTrue();

        var loaded = CrashReportStore.Load(path);
        loaded.Should().NotBeNull();
        loaded!.ExceptionMessage.Should().Be("contract");
        loaded.Source.Should().Be("contract-test");
    }

    [Fact]
    public void LoadAndDelete_RejectPathsOutsideStore()
    {
        var outside = Path.Combine(Path.GetTempPath(), "udt-crash-outside-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(outside, "{\"Source\":\"outside\"}");
        try
        {
            CrashReportStore.Load(outside).Should().BeNull();
            CrashReportStore.Delete(outside);
            File.Exists(outside).Should().BeTrue();
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void Load_WithNullPath_ReturnsNull()
    {
        CrashReportStore.Load(null!).Should().BeNull();
    }
}
