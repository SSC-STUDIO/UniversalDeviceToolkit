using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Guard)]
public sealed class PackageManifestScriptTests
{
    [Fact]
    public void PrepareAndTestPackageManifests_ShouldRoundTripReleaseMetadata()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var tempRoot = NewTempDirectory("UDT-package-manifests");
        const string version = "9.8.7";
        const string releaseDate = "2026-06-06";
        var fullHash = new string('a', 64);
        var portableHash = new string('b', 64);
        var onlineHash = new string('d', 64);
        var hashManifestPath = Path.Combine(tempRoot, "UniversalDeviceToolkit_v9.8.7_SHA256.txt");

        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "Packaging"));
            File.WriteAllText(hashManifestPath, BuildSha256Manifest(version, fullHash, portableHash, onlineHash));

            RunPowerShellScript(
                Path.Combine(repositoryRoot, "Packaging", "Prepare-PackageManifests.ps1"),
                [
                    "-RootPath", tempRoot,
                    "-Version", version,
                    "-ReleaseDate", releaseDate,
                    "-HashManifestPath", hashManifestPath,
                    "-UpdatePublishedScoopManifest",
                ],
                repositoryRoot);

            var wingetDirectory = Path.Combine(tempRoot, "Packaging", "winget", "manifests", "s", "SSC-STUDIO", "UniversalDeviceToolkit", version);
            var scoopDraftPath = Path.Combine(tempRoot, "Packaging", "scoop", $"universaldevicetoolkit.{version}.draft.json");
            var scoopPublishedPath = Path.Combine(tempRoot, "Packaging", "scoop", "universaldevicetoolkit.json");

            File.Exists(Path.Combine(wingetDirectory, "SSC-STUDIO.UniversalDeviceToolkit.installer.yaml")).Should().BeTrue();
            File.Exists(scoopDraftPath).Should().BeTrue();
            File.Exists(scoopPublishedPath).Should().BeTrue();

            var draftValidationOutput = RunPowerShellScript(
                Path.Combine(repositoryRoot, "Packaging", "Test-PackageManifests.ps1"),
                [
                    "-Version", version,
                    "-HashManifestPath", hashManifestPath,
                    "-WingetManifestDirectory", wingetDirectory,
                    "-ScoopManifestPaths", scoopDraftPath,
                ],
                repositoryRoot);
            var publishedValidationOutput = RunPowerShellScript(
                Path.Combine(repositoryRoot, "Packaging", "Test-PackageManifests.ps1"),
                [
                    "-Version", version,
                    "-HashManifestPath", hashManifestPath,
                    "-WingetManifestDirectory", wingetDirectory,
                    "-ScoopManifestPaths", scoopPublishedPath,
                ],
                repositoryRoot);

            draftValidationOutput.Should().Contain($"Package manifests match UniversalDeviceToolkit_v{version}_Full_Setup.exe");
            publishedValidationOutput.Should().Contain($"Package manifests match UniversalDeviceToolkit_v{version}_Full_Setup.exe");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void TestPackageManifests_ShouldRejectHashThatDiffersFromReleaseManifest()
    {
        var repositoryRoot = RepositoryPaths.FindRoot();
        var tempRoot = NewTempDirectory("UDT-package-manifest-hash-mismatch");
        const string version = "9.8.6";
        var releaseHash = new string('b', 64);
        var portableHash = new string('c', 64);
        var onlineHash = new string('e', 64);
        var wrongHash = new string('f', 64);
        var hashManifestPath = Path.Combine(tempRoot, "UniversalDeviceToolkit_v9.8.6_SHA256.txt");

        try
        {
            File.WriteAllText(hashManifestPath, BuildSha256Manifest(version, releaseHash, portableHash, onlineHash));

            RunPowerShellScript(
                    Path.Combine(repositoryRoot, "Packaging", "Test-PackageManifests.ps1"),
                    [
                        "-Version", version,
                        "-HashManifestPath", hashManifestPath,
                        "-ExpectedInstallerSha256", wrongHash,
                    ],
                    repositoryRoot,
                    expectSuccess: false)
                .Should()
                .Contain("Expected installer SHA256 and release SHA256 manifest mismatch");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string BuildSha256Manifest(string version, string fullHash, string portableHash, string onlineHash)
    {
        var hash = fullHash.ToUpperInvariant();
        var portable = portableHash.ToUpperInvariant();
        var online = onlineHash.ToUpperInvariant();
        return string.Join(
            Environment.NewLine,
            [
                $"{hash}  UniversalDeviceToolkit_v{version}_Full_Setup.exe",
                $"{portable}  UniversalDeviceToolkit_v{version}_Full_win-x64.zip",
                $"{online}  UniversalDeviceToolkit_v{version}_Online_Setup.exe",
                string.Empty,
            ]);
    }

    private static string RunPowerShellScript(string scriptPath, string[] arguments, string workingDirectory, bool expectSuccess = true)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
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

        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit(30_000).Should().BeTrue($"{Path.GetFileName(scriptPath)} should finish quickly");
        if (expectSuccess)
        {
            process.ExitCode.Should().Be(0, error);
        }
        else
        {
            process.ExitCode.Should().NotBe(0, output);
        }

        return output + error;
    }

    private static string NewTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
