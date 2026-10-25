using System.IO;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class ReleaseSigningGuardTests
{
    [Fact]
    public void ReleaseWorkflow_ShouldPublishInstallersWithoutAzureSigning()
    {
        var root = RepositoryPaths.FindRoot();
        var workflowText = File.ReadAllText(Path.Combine(root, ".github", "workflows", "Release.yml"));
        var workflow = GitHubWorkflowContract.Parse(workflowText);
        var job = workflow.Job("build");

        workflowText.Should().NotContain("azure/trusted-signing-action");
        workflowText.Should().NotContain("AZURE_TENANT_ID");
        workflowText.Should().NotContain("AZURE_CLIENT_ID");
        workflowText.Should().NotContain("AZURE_CLIENT_SECRET");
        workflowText.Should().NotContain("AZURE_TRUSTED_SIGNING_");
        workflowText.Should().NotContain("skip_signing");
        workflowText.Should().NotContain("RELEASE_SKIP_SIGNING");
        workflowText.Should().NotContain("Assert-AuthenticodeSignatures.ps1");

        var prepareWindowsPayload = job.Step("Prepare WebView2 installer payload");
        var buildInstaller = job.Step("Package WebView2 payload");
        var prepareCompatibility = job.Step("Prepare Electron compatibility payload");
        var packageCompatibility = job.Step("Package Electron compatibility payload");
        var footprint = job.Step("Verify final WebView2 installer footprint");
        var finalize = job.Step("Finalize release assets");
        var publish = job.Step("Create GitHub release");

        prepareWindowsPayload.Run.Should().Contain("-PreparePayloadsOnly");
        prepareWindowsPayload.Run.Should().Contain("Build-WebView2Installer.ps1");
        buildInstaller.Run.Should().Contain("-PackagePreparedPayloads");
        buildInstaller.Run.Should().Contain("Build-WebView2Installer.ps1");
        prepareCompatibility.Run.Should().Contain("-PreparePayloadsOnly");
        prepareCompatibility.Run.Should().Contain("Build-CompatibilityInstaller.ps1");
        packageCompatibility.Run.Should().Contain("-PackagePreparedPayloads");
        packageCompatibility.Run.Should().Contain("Build-CompatibilityInstaller.ps1");
        footprint.Run.Should().Contain("package-footprint.mjs");
        footprint.Run.Should().Contain("UniversalDeviceToolkitWebView2Setup-");
        footprint.Run.Should().Contain("UniversalDeviceToolkitCompatibilitySetup-");
        publish.Uses.Should().Be("softprops/action-gh-release@v3");
        workflowText.Should().Contain("release-assets/*.exe");
        workflowText.Should().Contain("release-assets/*.zip");
        workflowText.Should().Contain("release-assets/*_SHA256.txt");
        job.Step("Test (Contracts)").Run.Should().Contain("UniversalDeviceToolkit.Tests.Contracts.csproj");
        job.Step("Publish release payload").Run.Should().Contain("dotnet publish");

        job.Steps.IndexOf(prepareWindowsPayload).Should().BeLessThan(job.Steps.IndexOf(buildInstaller));
        job.Steps.IndexOf(prepareCompatibility).Should().BeLessThan(job.Steps.IndexOf(packageCompatibility));
        job.Steps.IndexOf(prepareCompatibility).Should().BeLessThan(job.Steps.IndexOf(buildInstaller));
        job.Steps.IndexOf(buildInstaller).Should().BeLessThan(job.Steps.IndexOf(packageCompatibility));
        job.Steps.IndexOf(packageCompatibility).Should().BeLessThan(job.Steps.IndexOf(footprint));
        job.Steps.IndexOf(footprint).Should().BeLessThan(job.Steps.IndexOf(finalize));
        job.Steps.IndexOf(finalize).Should().BeLessThan(job.Steps.IndexOf(publish));
    }
}
