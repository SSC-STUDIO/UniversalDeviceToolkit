using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class InstallPayloadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replace_RemovesOnlyOwnedFilesAndRestoresOnRegistrationFailure(bool fail)
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "target");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            Directory.CreateDirectory(Path.Combine(destination, "resources"));
            await File.WriteAllTextAsync(Path.Combine(source, "UniversalDeviceToolkit.exe"), "new-shell");
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), "[\"UniversalDeviceToolkit.exe\"]");
            await File.WriteAllTextAsync(Path.Combine(destination, "UniversalDeviceToolkit.exe"), "old-shell");
            await File.WriteAllTextAsync(Path.Combine(destination, "old-owned.dll"), "old-library");
            await File.WriteAllTextAsync(Path.Combine(destination, "keep.txt"), "user-file");
            await File.WriteAllTextAsync(Path.Combine(destination, "Uninstall.exe"), "old-uninstaller");
            await File.WriteAllTextAsync(Path.Combine(destination, "resources", "install-files.json"), "[\"UniversalDeviceToolkit.exe\",\"old-owned.dll\"]");
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination, language = "en", deviceMode = "auto" }));
            var payload = new InstallPayload(source);
            Task Register(string executable)
            {
                Assert.Equal("new-shell", File.ReadAllText(executable));
                File.WriteAllText(Path.Combine(destination, "Uninstall.exe"), "new-uninstaller");
                return fail ? Task.FromException(new IOException("Registration failed")) : Task.CompletedTask;
            }
            if (fail)
                await Assert.ThrowsAsync<IOException>(() => payload.InstallAsync(options, new Progress<object>(), Register));
            else
                await payload.InstallAsync(options, new Progress<object>(), Register);
            Assert.Equal(fail ? "old-shell" : "new-shell", await File.ReadAllTextAsync(Path.Combine(destination, "UniversalDeviceToolkit.exe")));
            Assert.Equal(fail ? "old-uninstaller" : "new-uninstaller", await File.ReadAllTextAsync(Path.Combine(destination, "Uninstall.exe")));
            Assert.Equal(fail, File.Exists(Path.Combine(destination, "old-owned.dll")));
            Assert.Equal("user-file", await File.ReadAllTextAsync(Path.Combine(destination, "keep.txt")));
            Assert.Empty(Directory.GetDirectories(root, ".udt-*"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Copy_UsesManifestAndPreservesSelectionWhileOmittingNetworkWorker()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "target with spaces");
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            var files = new[] { "UniversalDeviceToolkit.exe", "UniversalDeviceToolkit.NetworkProxy.dll" };
            foreach (var file in files) await File.WriteAllTextAsync(Path.Combine(source, file), file);
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), JsonSerializer.Serialize(files));
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new
            {
                destination, language = "ja", deviceMode = "basic", features = new { windowsOptimization = false, networkAcceleration = true }
            }));
            var executable = await new InstallPayload(source).CopyAsync(options, new Progress<object>());
            Assert.True(File.Exists(executable));
            Assert.False(File.Exists(Path.Combine(destination, files[1])));
            Assert.False(Directory.Exists(Path.Combine(destination, "resources", "setup")));
            var selection = await File.ReadAllTextAsync(Path.Combine(destination, "installer-selection.ini"));
            Assert.Contains("language=ja\ndeviceMode=basic\n", selection);
            Assert.Contains("networkAcceleration=0", selection);
            Assert.Contains("keyboard=1", selection);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Destination_RejectsRootsPayloadAncestorsAndUnrelatedNonemptyFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            Assert.Throws<ArgumentException>(() => InstallPayload.ValidateDestination(root, source));
            Assert.Throws<ArgumentException>(() => InstallPayload.ValidateDestination(Path.GetPathRoot(root) ?? root, source));
            Assert.Throws<ArgumentException>(() => InstallPayload.ValidateDestination(Path.Combine(source, "child"), source));
            var unrelated = Path.Combine(root, "unrelated");
            Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated, "keep.txt"), "keep");
            Assert.Throws<ArgumentException>(() => InstallPayload.ValidateDestination(unrelated, source));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(unrelated, "keep.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EscapingManifest_IsRejectedBeforeWritingDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), "[\"../outside.txt\"]");
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination = Path.Combine(root, "target"), language = "en", deviceMode = "auto" }));
            await Assert.ThrowsAsync<InvalidDataException>(() => new InstallPayload(source).CopyAsync(options, new Progress<object>()));
            Assert.False(Directory.Exists(options.Destination));
        }
        finally { Directory.Delete(root, true); }
    }
}
