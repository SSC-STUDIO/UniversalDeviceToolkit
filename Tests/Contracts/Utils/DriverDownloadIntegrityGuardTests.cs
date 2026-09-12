using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Security)]
public sealed class DriverDownloadIntegrityGuardTests
{
    [Fact]
    public void StartInstall_ShouldReHashBeforeElevation()
    {
        var source = RepositoryPaths.ReadFile(
            "Apps/Host", "Rpc", "Handlers", "DriverDownloadHandlers.cs");

        var pinIndex = source.IndexOf("TryPinVerifiedSha256", StringComparison.Ordinal);
        var resolveIndex = source.IndexOf("ResolveExpectedInstallerSha256", StringComparison.Ordinal);
        var validateIndex = source.IndexOf(
            "InstallerLaunchPathValidator.TryValidateForExecution",
            StringComparison.Ordinal);
        var elevateIndex = source.IndexOf("Verb = \"runas\"", StringComparison.Ordinal);

        pinIndex.Should().BeGreaterThanOrEqualTo(0);
        resolveIndex.Should().BeGreaterThanOrEqualTo(0);
        validateIndex.Should().BeGreaterThanOrEqualTo(0);
        elevateIndex.Should().BeGreaterThanOrEqualTo(0);
        validateIndex.Should().BeLessThan(elevateIndex);
        source.Should().Contain("expectedSha256");
        source.Should().Contain("Installer checksum is unknown; download the package again.");
        source.Should().Contain("Installer checksum mismatch.");
    }

    [Fact]
    public void UninstallHandler_ShouldReportCapabilityAsUnavailable()
    {
        var source = RepositoryPaths.ReadFile(
            "Apps/Host", "Rpc", "Handlers", "DriverDownloadHandlers.cs");

        source.Should().Contain("uninstallAvailable = false");
        source.Should().Contain("canUninstall = false");
        source.Should().Contain("available = false");
        source.Should().Contain("Driver uninstall is not supported.");
        source.Should().NotContain("error = \"not available\"");
    }

    [Fact]
    public void DriverDownloadPanel_ShouldNotOfferUninstallAction()
    {
        var source = RepositoryPaths.ReadFile(
            "Apps/Electron",
            "src",
            "renderer",
            "src",
            "components",
            "optimization",
            "DriverDownloadPanel.tsx");

        source.Should().NotContain("onUninstall");
        source.Should().NotContain("uninstallPackage");
        source.Should().NotContain("optimization.driver.uninstall");
        source.Should().NotContain("Delete24Regular");
        source.Should().Contain("optimization.driver.install");
    }
}
