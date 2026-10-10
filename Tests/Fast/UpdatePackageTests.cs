using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class UpdatePackageTests
{
    private static object Release(string version, string shell = "WebView2", bool draft = false, bool prerelease = false) => new
    {
        tag_name = version, draft, prerelease,
        assets = new[]
        {
            new { name = $"UniversalDeviceToolkit{shell}Setup-{version}.exe", browser_download_url = "https://example.com/setup.exe" },
            new { name = $"UniversalDeviceToolkit{shell}Setup-{version}.exe.sha256", browser_download_url = "https://example.com/setup.exe.sha256" }
        }
    };

    [Fact]
    public void SelectLatest_DoesNotFallBackToOlderPackageWhenLatestPrimaryAssetIsMissing()
    {
        var releases = JsonSerializer.SerializeToElement(new[] { Release("v6.1.3"), Release("v6.1.4", "Compatibility") });
        var error = Assert.Throws<InvalidDataException>(() => UpdatePackage.SelectLatest(releases));
        Assert.Contains("v6.1.4", error.Message);
        Assert.Contains("WebView2", error.Message);
    }

    [Fact]
    public void SelectLatest_UsesHighestPublicStableVersionAndRefreshesWithNewPayload()
    {
        var releases = JsonSerializer.SerializeToElement(new[]
        {
            Release("v6.1.3"), Release("plugin-catalog"), Release("v6.1.5", draft: true),
            Release("v6.2.0", prerelease: true), Release("v6.1.4")
        });
        var selected = UpdatePackage.SelectLatest(releases);
        Assert.NotNull(selected);
        Assert.Equal("v6.1.4", selected.Value.Release.GetProperty("tag_name").GetString());

        var refreshed = UpdatePackage.SelectLatest(JsonSerializer.SerializeToElement(new[] { Release("v6.1.5") }));
        Assert.NotNull(refreshed);
        Assert.Equal("v6.1.5", refreshed.Value.Release.GetProperty("tag_name").GetString());
    }

    [Fact]
    public void SelectLatest_RejectsMissingDownloadUrlsAndDoesNotUseOlderRelease()
    {
        var releases = JsonSerializer.SerializeToElement(new object[]
        {
            Release("v6.1.3"),
            new { tag_name = "v6.1.4", assets = new[]
            {
                new { name = "UniversalDeviceToolkitWebView2Setup-6.1.4.exe" },
                new { name = "UniversalDeviceToolkitWebView2Setup-6.1.4.exe.sha256" }
            } }
        });
        Assert.Throws<InvalidDataException>(() => UpdatePackage.SelectLatest(releases));
    }

    [Fact]
    public void Select_MixedReleasePrefersWebView2AndItsOwnManifest()
    {
        var assets = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "UniversalDeviceToolkitLightweightSetup-6.1.1.exe" },
            new { name = "UniversalDeviceToolkitLightweightSetup-6.1.1.exe.sha256" },
            new { name = "UniversalDeviceToolkitWebView2Setup-6.1.1.exe" },
            new { name = "UniversalDeviceToolkitWebView2Setup-6.1.1.exe.sha256" },
            new { name = "UniversalDeviceToolkitCompatibilitySetup-6.1.1.exe" },
            new { name = "unrelated.sha256" }
        });
        var selection = UpdatePackage.Select(assets);
        Assert.NotNull(selection);
        Assert.Equal("UniversalDeviceToolkitWebView2Setup-6.1.1.exe", selection.Value.Installer.GetProperty("name").GetString());
        Assert.Equal("UniversalDeviceToolkitWebView2Setup-6.1.1.exe.sha256", selection.Value.Manifest.GetProperty("name").GetString());
    }

    [Fact]
    public void Select_RejectsHistoricalElectronAndUnverifiedOrUnsafeAssets()
    {
        foreach (var name in new[]
        {
            "UniversalDeviceToolkit_v6.1.1_Full_Setup.exe", "UniversalDeviceToolkitSetup-6.1.1.exe",
            "UniversalDeviceToolkitCompatibilitySetup-6.1.1.exe", "UniversalDeviceToolkitWebView2Setup-../../other.exe"
        })
            Assert.Null(UpdatePackage.Select(JsonSerializer.SerializeToElement(new[] { new { name }, new { name = name + ".sha256" } })));
        Assert.Null(UpdatePackage.Select(JsonSerializer.SerializeToElement(new[] { new { name = "UniversalDeviceToolkitWebView2Setup-6.1.1.exe" } })));
    }

    [Fact]
    public void Select_AcceptsReleaseWideManifestAndHashRequiresExactFilename()
    {
        const string name = "UniversalDeviceToolkitWebView2Setup-6.1.1.exe";
        var assets = JsonSerializer.SerializeToElement(new[] { new { name }, new { name = "UniversalDeviceToolkit_v6.1.1_SHA256.txt" } });
        Assert.NotNull(UpdatePackage.Select(assets));
        var digest = new string('a', 64);
        Assert.Equal(digest, UpdatePackage.ReadHash(digest + " *" + name, name));
        Assert.Null(UpdatePackage.ReadHash(digest + "  " + name + ".bak", name));
        Assert.Null(UpdatePackage.ReadHash(digest, name));
        Assert.Null(UpdatePackage.ReadHash(digest + "  unrelated.exe", name));
    }

    [Fact]
    public async Task CopyVerified_ClosesWriterAndRejectsTamperingWithoutReplacingExistingInstaller()
    {
        var folder = Path.Combine(Path.GetTempPath(), "udt-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "setup.exe");
            var bytes = Encoding.UTF8.GetBytes("verified installer");
            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            long progress = 0;
            using (var input = new MemoryStream(bytes))
                await UpdatePackage.CopyVerifiedAsync(input, path, digest, received => progress = received);
            Assert.Equal(bytes.Length, progress);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            Assert.True(await UpdatePackage.MatchesHashAsync(path, digest));
            using (var input = new MemoryStream([1, 2, 3]))
                await Assert.ThrowsAsync<IOException>(() => UpdatePackage.CopyVerifiedAsync(input, path, digest, _ => { }));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            Assert.False(File.Exists(path + ".partial"));
            await File.WriteAllTextAsync(path, "replaced after download");
            Assert.False(await UpdatePackage.MatchesHashAsync(path, digest));
        }
        finally { Directory.Delete(folder, true); }
    }
}
