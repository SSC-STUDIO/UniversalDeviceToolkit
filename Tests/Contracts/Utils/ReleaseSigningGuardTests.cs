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
        var prepareElectronPayloads = job.Step("Prepare Electron installer payloads");
        var electronPayloadSigning = job.Step("Sign Electron installer payloads");
        var electronPayloadVerification = job.Step("Verify Electron installer payload signatures");
        var prepareInstallerShell = job.Step("Prepare Electron installer shell");
        var installerShellSigning = job.Step("Sign Electron installer shell");
        var installerShellVerification = job.Step("Verify Electron installer shell signatures");
        var buildInstaller = job.Step("Build Electron installer");
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
        electronPayloadSigning.Uses.Should().Be("azure/trusted-signing-action@v0.5");
        electronPayloadSigning.WithValue("files-folder").Should().Be("${{ env.INSTALLER_PAYLOAD_OUTPUT }}");
        electronPayloadSigning.WithValue("files-folder-filter").Should().Be("exe,dll");
        electronPayloadSigning.WithValue("files-folder-recurse").Should().Be("true");
        electronPayloadSigning.WithValue("files-folder-depth").Should().Be("2");
        electronPayloadSigning.WithValue("append-signature").Should().Be("true");
        electronPayloadSigning.WithValue("batch-size").Should().Be("10000");
        electronPayloadSigning.WithValue("timeout").Should().Be("900");
        electronPayloadVerification.Run.Should().Contain("Assert-AuthenticodeSignatures.ps1");
        prepareElectronPayloads.Run.Should().Contain("-PreparePayloadsOnly");
        prepareInstallerShell.Run.Should().Contain("-PrepareInstallerShellOnly");
        installerShellSigning.Uses.Should().Be("azure/trusted-signing-action@v0.5");
        installerShellSigning.WithValue("files").Should().Contain("nsis\\elevate.exe");
        installerShellSigning.WithValue("files-folder").Should().Be("${{ env.INSTALLER_PAYLOAD_OUTPUT }}\\installer-shell");
        installerShellSigning.WithValue("files-folder-filter").Should().Be("exe,dll");
        installerShellSigning.WithValue("files-folder-recurse").Should().Be("false");
        installerShellSigning.WithValue("append-signature").Should().Be("true");
        installerShellVerification.Run.Should().Contain("Assert-AuthenticodeSignatures.ps1");
        installerShellVerification.Run.Should().Contain("nsis\\elevate.exe");
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
        job.Steps.IndexOf(prepareElectronPayloads).Should().BeLessThan(job.Steps.IndexOf(electronPayloadSigning));
        job.Steps.IndexOf(electronPayloadSigning).Should().BeLessThan(job.Steps.IndexOf(electronPayloadVerification));
        job.Steps.IndexOf(electronPayloadVerification).Should().BeLessThan(job.Steps.IndexOf(prepareInstallerShell));
        job.Steps.IndexOf(prepareInstallerShell).Should().BeLessThan(job.Steps.IndexOf(installerShellSigning));
        job.Steps.IndexOf(installerShellSigning).Should().BeLessThan(job.Steps.IndexOf(installerShellVerification));
        job.Steps.IndexOf(installerShellVerification).Should().BeLessThan(job.Steps.IndexOf(buildInstaller));
    }
}
