using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class ReleaseAssetScriptTests
{
    private const string HostLibFile = "UniversalDeviceToolkit.Lib.dll";
    private const string HostAbstractionsFile = "UniversalDeviceToolkit.Lib.Abstractions.dll";
    private const string HostSharedFile = "UniversalDeviceToolkit.Lib.Shared.dll";
    private const string HostAutomationFile = "UniversalDeviceToolkit.Lib.Automation.dll";
    private const string HostMacroFile = "UniversalDeviceToolkit.Lib.Macro.dll";
    private const string SerilogFile = "Serilog.dll";
    private const string SerilogAsyncFile = "Serilog.Sinks.Async.dll";
    private const string SerilogFileSinkFile = "Serilog.Sinks.File.dll";
    private const string TestReleaseVersion = "9.8.7";

    private static readonly string[] NonEnglishCultures =
    [
        "ar", "bg", "cs", "de", "el", "es", "fr", "hu", "it", "ja", "lv",
        "nl-NL", "pl", "pt", "pt-BR", "ro", "ru", "sk", "tr", "uk", "vi",
        "zh-Hans", "zh-Hant", "uz-Latn-UZ",
    ];

    [Fact]
    public void BuildLanguageAssets_ShouldUseHostSatellitesAndPruneOnlyOnlinePayload()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var tempRoot = NewTempDirectory("UDT-language-assets");

        try
        {
            var buildDirectory = Path.Combine(tempRoot, "electron-build");
            var onlineDirectory = Path.Combine(tempRoot, "online-build");
            var hostDirectory = Path.Combine(tempRoot, "host-publish");
            var releaseDirectory = Path.Combine(tempRoot, "release");
            var pagesDirectory = Path.Combine(tempRoot, "pages");

            WriteTestFile(buildDirectory, "app-marker.txt", "electron payload");
            WriteTestFile(
                buildDirectory,
                Path.Combine("en", "UniversalDeviceToolkit.Electron.resources.dll"),
                "english satellite");
            WriteTestFile(
                buildDirectory,
                Path.Combine("fr", "UniversalDeviceToolkit.Electron.resources.dll"),
                "french satellite");
            WriteTestFile(
                buildDirectory,
                Path.Combine("de", "ThirdParty.resources.dll"),
                "third-party german satellite");
            WriteTestFile(buildDirectory, Path.Combine("content", "keep.txt"), "shared content");

            SeedLanguageHostPayload(hostDirectory);
            File.Exists(Path.Combine(hostDirectory, "fr", "Universal Device Toolkit.resources.dll"))
                .Should()
                .BeFalse("the Electron-era Host no longer publishes the retired WPF satellite");

            var output = RunPowerShellScript(
                Path.Combine(repositoryRoot, "Scripts", "Build-LanguageAssets.ps1"),
                [
                    "-BuildDir", buildDirectory,
                    "-OnlineBuildDir", onlineDirectory,
                    "-HostBuildDir", hostDirectory,
                    "-ReleaseOutput", releaseDirectory,
                    "-PagesOutput", pagesDirectory,
                    "-Version", TestReleaseVersion,
                ],
                tempRoot);

            output.Should().Contain("Electron packaging supplies the desktop ZIP assets");

            var languageDirectory = Path.Combine(
                pagesDirectory,
                "resources",
                TestReleaseVersion,
                "languages");
            var catalogPath = Path.Combine(languageDirectory, "catalog.json");
            File.Exists(catalogPath).Should().BeTrue();

            using (var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath)))
            {
                var catalogCultures = catalog.RootElement
                    .GetProperty("languages")
                    .EnumerateArray()
                    .Select(entry => entry.GetProperty("culture").GetString())
                    .ToArray();

                catalogCultures.Should().Equal(NonEnglishCultures);
            }

            foreach (var culture in NonEnglishCultures)
            {
                File.Exists(Path.Combine(languageDirectory, $"{culture}.zip"))
                    .Should()
                    .BeTrue($"the catalog declares a pack for {culture}");
            }

            Directory.Exists(Path.Combine(onlineDirectory, "fr")).Should().BeFalse();
            Directory.Exists(Path.Combine(onlineDirectory, "de")).Should().BeFalse();
            File.Exists(Path.Combine(onlineDirectory, "app-marker.txt")).Should().BeTrue();
            File.Exists(Path.Combine(onlineDirectory, "content", "keep.txt")).Should().BeTrue();
            File.Exists(Path.Combine(
                    onlineDirectory,
                    "en",
                    "UniversalDeviceToolkit.Electron.resources.dll"))
                .Should()
                .BeTrue();
            File.Exists(Path.Combine(buildDirectory, "fr", "UniversalDeviceToolkit.Electron.resources.dll"))
                .Should()
                .BeTrue("the full source payload must not be pruned");

            GetZipEntries(Path.Combine(languageDirectory, "fr.zip")).Should().Contain(
                "fr/UniversalDeviceToolkit.Lib.resources.dll");
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void ElectronInstallerBuilder_ShouldStageFullPayloadBeforePruningOnlinePayload()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "Scripts", "Build-ElectronInstaller.ps1"));

        var fullPayloadStage = script.IndexOf(
            "Move-UnpackedPayload -Destination $fullPayloadDir",
            StringComparison.Ordinal);
        var hostPrune = script.IndexOf("& $pruneScript -PayloadPath $hostPublishDir", StringComparison.Ordinal);
        var onlineChannel = script.IndexOf("Set-InstallChannel -Channel 'online'", StringComparison.Ordinal);
        var onlinePayloadStage = script.IndexOf(
            "Move-UnpackedPayload -Destination $onlinePayloadDir",
            StringComparison.Ordinal);
        var onlineZipCopy = script.IndexOf(
            "Copy-Item -LiteralPath $onlineZipArtifact.FullName -Destination $onlineZipPath",
            StringComparison.Ordinal);

        fullPayloadStage.Should().BeGreaterThan(-1);
        hostPrune.Should().BeGreaterThan(fullPayloadStage);
        onlineChannel.Should().BeGreaterThan(hostPrune);
        onlinePayloadStage.Should().BeGreaterThan(onlineChannel);
        onlineZipCopy.Should().BeGreaterThan(onlinePayloadStage);
        script.Should().Contain("-PrepackagedPath $fullPayloadDir");
        script.Should().Contain("-PrepackagedPath $onlinePayloadDir");
        script.Should().Contain("Remove-Item -LiteralPath $distDir -Recurse -Force");
    }

    [Fact]
    public void ReleaseScripts_ShouldNotPublishRetiredInstallerOrCliAliases()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var workflow = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "workflows", "Release.yml"));
        var assets = File.ReadAllText(Path.Combine(repositoryRoot, "Scripts", "Build-LanguageAssets.ps1"));

        workflow.Should().NotContain("LEGACY_SETUP_ASSET");
        workflow.Should().NotContain("\\llt.exe");
        workflow.Should().NotContain("\\llt.dll");
        assets.Should().NotContain("LenovoLegionToolkit_v");
    }

    [Fact]
    public void BuildLanguageAssets_ShouldFailClearlyWhenHostCultureIsMissing()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var tempRoot = NewTempDirectory("UDT-language-assets-missing-culture");

        try
        {
            var buildDirectory = Path.Combine(tempRoot, "electron-build");
            var hostDirectory = Path.Combine(tempRoot, "host-publish");
            WriteTestFile(buildDirectory, "app-marker.txt", "electron payload");
            SeedLanguageHostPayload(hostDirectory, omittedCulture: "fr");

            var output = RunPowerShellScript(
                Path.Combine(repositoryRoot, "Scripts", "Build-LanguageAssets.ps1"),
                [
                    "-BuildDir", buildDirectory,
                    "-OnlineBuildDir", Path.Combine(tempRoot, "online-build"),
                    "-HostBuildDir", hostDirectory,
                    "-ReleaseOutput", Path.Combine(tempRoot, "release"),
                    "-PagesOutput", Path.Combine(tempRoot, "pages"),
                    "-Version", TestReleaseVersion,
                ],
                tempRoot,
                expectSuccess: false);

            output.Should().Contain("Language pack 'fr' cannot be created");
            output.Should().Contain("fr/UniversalDeviceToolkit.*.resources.dll");
            output.Should().Contain("Publish the Host with all supported satellite cultures");
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static void SeedLanguageHostPayload(string hostDirectory, string? omittedCulture = null)
    {
        foreach (var culture in NonEnglishCultures)
        {
            if (string.Equals(culture, omittedCulture, StringComparison.Ordinal))
            {
                continue;
            }

            WriteTestFile(
                hostDirectory,
                Path.Combine(culture, "UniversalDeviceToolkit.Lib.resources.dll"),
                $"host satellite:{culture}");
        }
    }

    private static string[] GetZipEntries(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .Select(entry => entry.FullName.Replace('\\', '/').TrimStart('/'))
            .ToArray();
    }

    private static void WriteTestFile(string rootDirectory, string relativePath, string content)
    {
        var path = Path.Combine(rootDirectory, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Test path has no parent directory: {path}");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    private static string RunPowerShellScript(
        string scriptPath,
        string[] arguments,
        string workingDirectory,
        bool expectSuccess = true,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var environmentVariable in environmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start PowerShell.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(180_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileName(scriptPath)} did not finish within 180 seconds.");
        }

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        var combinedOutput = output + error;

        if (expectSuccess)
        {
            process.ExitCode.Should().Be(
                0,
                "PowerShell output was:{0}{1}",
                Environment.NewLine,
                combinedOutput);
        }
        else
        {
            process.ExitCode.Should().NotBe(
                0,
                "PowerShell output was:{0}{1}",
                Environment.NewLine,
                combinedOutput);
        }

        return combinedOutput;
    }

    private static string NewTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
