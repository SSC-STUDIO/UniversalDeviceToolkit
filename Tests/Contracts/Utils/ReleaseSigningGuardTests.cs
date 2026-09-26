using System.IO;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class ReleaseSigningGuardTests
{
    [Fact]
    public void ReleaseWorkflow_ShouldSignAndVerifyPayloadAndInstallers()
    {
        var root = RepositoryPaths.FindRoot();
        var workflow = GitHubWorkflowContract.Parse(
            File.ReadAllText(Path.Combine(root, ".github", "workflows", "Release.yml")));
        var script = File.ReadAllText(Path.Combine(root, "Scripts", "Assert-AuthenticodeSignatures.ps1"));

        var job = workflow.Job("build");
        var payloadSigning = job.Step("Sign release payload");
        var payloadVerification = job.Step("Verify release payload signatures");
        var hostSigning = job.Step("Sign Host payload");
        var hostVerification = job.Step("Verify Host payload signatures");
        var prepareAssets = job.Step("Prepare release and Pages resources");
        var prepareWindowsPayload = job.Step("Prepare WebView2 installer payload");
        var windowsSigning = job.Step("Sign WebView2 installer payload");
        var windowsVerification = job.Step("Verify WebView2 payload signatures");
        var buildInstaller = job.Step("Package signed WebView2 payload");
        var installerSigning = job.Step("Sign installers");
        var installerVerification = job.Step("Verify installer signatures");

        payloadSigning.Uses.Should().Be("azure/trusted-signing-action@v0.5");
        installerSigning.Uses.Should().Be("azure/trusted-signing-action@v0.5");
        payloadSigning.WithValue("files-folder").Should().Be("${{ env.BUILD_OUTPUT }}");
        payloadSigning.WithValue("files-folder-filter").Should().Be("exe,dll");
        payloadSigning.WithValue("files-folder-recurse").Should().Be("true");
        hostSigning.Uses.Should().Be("azure/trusted-signing-action@v0.5");
        hostSigning.WithValue("files-folder").Should().Be("${{ env.HOST_BUILD_OUTPUT }}");
        hostSigning.WithValue("files-folder-filter").Should().Be("exe,dll");
        hostSigning.WithValue("files-folder-recurse").Should().Be("true");
        hostVerification.Run.Should().Contain("Assert-AuthenticodeSignatures.ps1");
        hostVerification.Run.Should().Contain("$env:HOST_BUILD_OUTPUT");
        windowsSigning.Uses.Should().Be("azure/trusted-signing-action@v0.5");
        windowsSigning.WithValue("files-folder").Should().Be("${{ env.INSTALLER_PAYLOAD_OUTPUT }}\\full");
        windowsSigning.WithValue("files-folder-filter").Should().Be("exe,dll");
        windowsSigning.WithValue("files-folder-recurse").Should().Be("true");
        windowsSigning.WithValue("append-signature").Should().Be("true");
        windowsSigning.WithValue("batch-size").Should().Be("10000");
        windowsSigning.WithValue("timeout").Should().Be("900");
        windowsVerification.Run.Should().Contain("Assert-AuthenticodeSignatures.ps1");
        prepareWindowsPayload.Run.Should().Contain("-PreparePayloadsOnly");
        job.Steps.IndexOf(windowsVerification).Should().BeLessThan(job.Steps.IndexOf(buildInstaller));
        buildInstaller.Run.Should().Contain("-PackagePreparedPayloads");
        installerSigning.WithValue("files-folder").Should().Be("${{ env.INSTALLER_OUTPUT }}");
        installerSigning.WithValue("files-folder-filter").Should().Be("exe");
        payloadVerification.Run.Should().Contain("Assert-AuthenticodeSignatures.ps1");
        payloadVerification.Run.Should().Contain("$env:BUILD_OUTPUT");
        installerVerification.Run.Should().Contain("Assert-AuthenticodeSignatures.ps1");
        script.Should().Contain("Get-AuthenticodeSignature");
        script.Should().Contain("Status -ne 'Valid'");
        job.Steps.IndexOf(hostSigning).Should().BeLessThan(job.Steps.IndexOf(hostVerification));
        job.Steps.IndexOf(hostVerification).Should().BeLessThan(job.Steps.IndexOf(payloadSigning));
        job.Steps.IndexOf(hostVerification).Should().BeLessThan(job.Steps.IndexOf(prepareAssets));
        job.Steps.IndexOf(hostVerification).Should().BeLessThan(job.Steps.IndexOf(buildInstaller));
        job.Steps.IndexOf(prepareWindowsPayload).Should().BeLessThan(job.Steps.IndexOf(windowsSigning));
        job.Steps.IndexOf(windowsSigning).Should().BeLessThan(job.Steps.IndexOf(windowsVerification));
    }
}
