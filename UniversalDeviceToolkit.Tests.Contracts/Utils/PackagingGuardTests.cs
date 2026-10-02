using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class PackagingGuardTests
{
    [Fact]
    public void ElectronBuilder_ShouldShipNsisAndNsisWebWithMaximumCompression()
    {
        var config = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "electron-builder.yml");

        config.Should().Contain("compression: maximum");
        config.Should().Contain("target: nsis");
        config.Should().Contain("target: nsis-web");
        config.Should().Contain("UniversalDeviceToolkitOnlineSetup-${version}.${ext}");
        config.Should().Contain("provider: github");
    }

    [Fact]
    public void ElectronInstallerScript_ShouldAssertOnlineStubSizeAndSplitTargets()
    {
        var script = RepositoryPaths.ReadFile("Scripts", "Build-ElectronInstaller.ps1");

        script.Should().Contain("85MB");
        script.Should().Contain("UniversalDeviceToolkitOnlineSetup.exe");
        script.Should().Contain("'--win', $Target");
        script.Should().Contain("AllowedCultures 'en'");
        script.Should().Contain("--prepackaged");
        script.Should().Contain("PackagePreparedPayloads");
        script.Should().Contain("PrepareInstallerShellOnly");
        script.Should().Contain("--prepackaged $installerShellDir");
        script.Should().Contain("ELECTRON_BUILDER_NSIS_DIR");
        script.Should().Contain("stage-nsis-toolset.mjs");
        script.Should().Contain("UTF8Encoding($false)");
        script.Should().NotContain("Set-Content -LiteralPath $packageJsonPath -Encoding utf8");
        script.Should().NotContain("-Target 'nsis' -PrepackagedPath $fullPayloadDir");
    }

    [Fact]
    public void ReleaseWorkflow_ShouldPublishDistinctFullAndOnlineInstallers()
    {
        var workflow = RepositoryPaths.ReadFile(".github", "workflows", "Release.yml");

        workflow.Should().Contain("UniversalDeviceToolkitOnlineSetup.exe");
        workflow.Should().Contain("UniversalDeviceToolkitSetup.exe");
        // The modern online installer downloads the Online win-x64 ZIP from the
        // release; the retired nsis-web payload must not be demanded as an asset.
        workflow.Should().NotContain("*.nsis.7z");
        workflow.Should().Contain("Prune-ShippingFootprint.ps1");
        workflow.Should().Contain("UniversalDeviceToolkit.Host/publish/win-x64");
        workflow.Should().NotContain("OnlineInstallerPath = \"$env:INSTALLER_OUTPUT\\UniversalDeviceToolkitSetup.exe\"");
    }

    [Fact]
    public void InAppUpdater_ShouldKeepLegacyClientLaunchContract()
    {
        var downloader = RepositoryPaths.ReadFile(
            "UniversalDeviceToolkit.Electron", "src", "main", "update-downloader.ts");
        var installer = RepositoryPaths.ReadFile(
            "UniversalDeviceToolkit.Electron", "installer", "main.mjs");

        downloader.Should().Contain("windowsInstallerLaunchArgs");
        downloader.Should().Contain("['/S']");
        downloader.Should().Contain("Zone.Identifier");
        downloader.Should().Contain("Start-Process -FilePath");
        downloader.Should().Contain("-Verb RunAs");
        installer.Should().Contain("isSilentSetupArgument");
        installer.Should().Contain("shouldRelaunchElevated");
        RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "custom-online-installer.yml")
            .Should().Contain("requestExecutionLevel: user");
    }

    [Fact]
    public void FootprintPackaging_ShouldKeepOnlyRequiredChromiumLocalesAndAuditEveryPackage()
    {
        var builderConfig = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "electron-builder.yml");
        var installerConfig = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "custom-installer.yml");
        var auditScript = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "scripts", "package-footprint.mjs");
        var hostProject = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Host", "UniversalDeviceToolkit.Host.csproj");

        builderConfig.Should().Contain("afterPack: ./scripts/package-footprint.mjs");
        builderConfig.Should().Contain("electronLanguages:");
        builderConfig.Should().Contain("- zh-TW");
        builderConfig.Should().Contain("- pt-PT");
        installerConfig.Should().Contain("electronLanguages:");
        installerConfig.Should().Contain("- en-US");
        auditScript.Should().Contain("app.asar contains node_modules entries");
        auditScript.Should().Contain("Host contains PDB files");
        auditScript.Should().Contain("Chromium locale mismatch");
        hostProject.Should().Contain("<IsUdtShippingApp>true</IsUdtShippingApp>");
    }

    [Fact]
    public void ShippingApps_ShouldUseWorkstationGcAndStripUnusedRuntimeFeatures()
    {
        var targets = RepositoryPaths.ReadFile("Directory.Build.targets");
        var prune = RepositoryPaths.ReadFile("Scripts", "Prune-ShippingFootprint.ps1");

        targets.Should().Contain("<ServerGarbageCollection>false</ServerGarbageCollection>");
        targets.Should().Contain("<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>");
        targets.Should().Contain("<RetainVMGarbageCollection>false</RetainVMGarbageCollection>");
        targets.Should().Contain("<TieredPGO>true</TieredPGO>");
        targets.Should().Contain("System.GC.ConserveMemory");
        targets.Should().Contain("System.GC.DynamicAdaptationMode");
        targets.Should().Contain("<DebuggerSupport>false</DebuggerSupport>");
        targets.Should().Contain("<MetadataUpdaterSupport>false</MetadataUpdaterSupport>");
        targets.Should().Contain("<HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>");
        prune.Should().Contain("createdump.exe");
        prune.Should().Contain("mscordaccore");
        prune.Should().Contain("Microsoft.DiaSymReader.Native");
        prune.Should().Contain("libmonoposixhelper.dll");
    }

    [Fact]
    public void FootprintWorkflow_ShouldBuildNativePackagesOnSupportedRunners()
    {
        var workflow = RepositoryPaths.ReadFile(".github", "workflows", "package-footprint.yml");
        var ciTestsWorkflow = RepositoryPaths.ReadFile(".github", "workflows", "Ci-tests.yml");

        workflow.Should().Contain("windows-2022");
        workflow.Should().Contain("ubuntu-24.04");
        workflow.Should().Contain("macos-15");
        workflow.Should().Contain("macos-15-intel");
        workflow.Should().Contain("npm ci");
        workflow.Should().Contain("smoke-host.mjs");
        workflow.Should().Contain("package-footprint.mjs");
        ciTestsWorkflow.Should().Contain("os: macos-15");
        ciTestsWorkflow.Should().Contain("os: macos-15-intel");
        ciTestsWorkflow.Should().NotContain("os: macos-13");
    }

    [Fact]
    public void ElectronInstaller_ShouldPersistLanguageAndDeviceSelectionBeforeFirstLaunch()
    {
        var script = RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "buildResources", "installer.nsh");

        script.Should().Contain("customPageAfterChangeDir");
        script.Should().Contain("Page custom UdtLanguagePage UdtLanguageLeave");
        script.Should().Contain("Page custom UdtDevicePage UdtDeviceLeave");
        script.Should().Contain("$INSTDIR\\installer-selection.ini");
        script.Should().Contain("WriteINIStr");
        script.Should().Contain("deviceMode");
        script.Should().Contain("Page custom UdtFeaturesPage UdtFeaturesLeave");
        script.Should().Contain("windowsOptimization");
        script.Should().Contain("networkAcceleration");
        script.Should().Contain("UniversalDeviceToolkit.NetworkProxy.exe");
    }

    [Fact]
    public void ShippingSurfaceVersions_ShouldMatchDirectoryBuildProps()
    {
        var version = ReadReleaseVersion();
        JsonDocument.Parse(RepositoryPaths.ReadFile("package.json"))
            .RootElement.GetProperty("version").GetString().Should().Be(version);
        JsonDocument.Parse(RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "package.json"))
            .RootElement.GetProperty("version").GetString().Should().Be(version);

        var lockFile = JsonDocument.Parse(
            RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "package-lock.json"));
        lockFile.RootElement.GetProperty("version").GetString().Should().Be(version);
        lockFile.RootElement.GetProperty("packages").GetProperty("").GetProperty("version")
            .GetString().Should().Be(version);

        RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "buildResources", "installer.nsh")
            .Should().Contain($"\"{version}\"");
        RepositoryPaths.ReadFile("UniversalDeviceToolkit.Electron", "installer", "renderer.mjs")
            .Should().Contain($"version: '{version}'");
        RepositoryPaths.ReadFile("README.md")
            .Should().Contain($"Current stable release: v{version}.");
        RepositoryPaths.ReadFile("README_zh-hans.md")
            .Should().Contain($"当前稳定版：v{version}。");
    }

    private static string ReadReleaseVersion()
    {
        var props = RepositoryPaths.ReadFile("Directory.Build.props");
        var major = MatchProp(props, "MajorVersion");
        var minor = MatchProp(props, "MinorVersion");
        var patch = MatchProp(props, "PatchVersion");
        return $"{major}.{minor}.{patch}";
    }

    private static string MatchProp(string props, string name)
    {
        var match = Regex.Match(props, $"<{name}>(\\d+)</{name}>");
        match.Success.Should().BeTrue($"Directory.Build.props must define {name}");
        return match.Groups[1].Value;
    }
}
