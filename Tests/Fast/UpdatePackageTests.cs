using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class UpdatePackageTests
{
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
