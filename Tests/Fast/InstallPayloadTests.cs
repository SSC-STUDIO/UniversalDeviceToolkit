using System.Text.Json;
using System.Text;
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
            await File.WriteAllTextAsync(Path.Combine(destination, "resources", "install-files.json"),
                "[\"UniversalDeviceToolkit.exe\",\"old-owned.dll\",\"Uninstall.exe\",\"resources/install-files.json\",\"resources/install-files.txt\"]");
            await File.WriteAllTextAsync(Path.Combine(destination, "resources", "install-files.txt"), "old ownership list");
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
            if (fail)
                Assert.Equal("old ownership list", await File.ReadAllTextAsync(Path.Combine(destination, "resources", "install-files.txt")));
            else
                Assert.Contains("UniversalDeviceToolkit.exe\r\n", await File.ReadAllTextAsync(Path.Combine(destination, "resources", "install-files.txt"), Encoding.Unicode));
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
            var owned = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(Path.Combine(destination, "resources", "install-files.json")));
            Assert.NotNull(owned);
            Assert.DoesNotContain(files[1], owned);
            Assert.Contains("installer-selection.ini", owned);
            Assert.Contains("resources/install-files.txt", owned);
            var uninstallBytes = await File.ReadAllBytesAsync(Path.Combine(destination, "resources", "install-files.txt"));
            Assert.False(uninstallBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()));
            var uninstallFiles = Encoding.Unicode.GetString(uninstallBytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(owned.Select(file => file.Replace('/', '\\')), uninstallFiles);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Replace_LegacyInstallationWithoutManifestPreservesUnknownFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "target");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            Directory.CreateDirectory(Path.Combine(destination, "resources"));
            await File.WriteAllTextAsync(Path.Combine(source, "UniversalDeviceToolkit.exe"), "new-shell");
            await File.WriteAllTextAsync(Path.Combine(source, "new-library.dll"), "new-library");
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), "[\"UniversalDeviceToolkit.exe\",\"new-library.dll\"]");
            await File.WriteAllTextAsync(Path.Combine(destination, "UniversalDeviceToolkit.exe"), "legacy-shell");
            await File.WriteAllTextAsync(Path.Combine(destination, "new-library.dll"), "unknown user file");
            await File.WriteAllTextAsync(Path.Combine(destination, "chrome.dll"), "unknown library");
            await File.WriteAllTextAsync(Path.Combine(destination, "resources", "app.asar"), "unknown bundle");
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination, language = "en", deviceMode = "auto" }));

            var messages = new List<object>();
            await new InstallPayload(source).CopyAsync(options, new InlineProgress(messages.Add));

            Assert.Equal("new-shell", await File.ReadAllTextAsync(Path.Combine(destination, "UniversalDeviceToolkit.exe")));
            Assert.Equal("unknown library", await File.ReadAllTextAsync(Path.Combine(destination, "chrome.dll")));
            Assert.Equal("unknown bundle", await File.ReadAllTextAsync(Path.Combine(destination, "resources", "app.asar")));
            var owned = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(Path.Combine(destination, "resources", "install-files.json")));
            Assert.NotNull(owned);
            Assert.DoesNotContain("chrome.dll", owned);
            Assert.DoesNotContain("resources/app.asar", owned);
            var backup = Assert.Single(Directory.GetDirectories(root, ".udt-backup-*"));
            Assert.Equal("legacy-shell", await File.ReadAllTextAsync(Path.Combine(backup, "UniversalDeviceToolkit.exe")));
            Assert.Equal("unknown user file", await File.ReadAllTextAsync(Path.Combine(backup, "new-library.dll")));
            Assert.Contains(messages, message => JsonSerializer.SerializeToElement(message).TryGetProperty("directory", out var directory)
                && directory.GetString() == backup);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Replace_RejectsNewFileCollidingWithUnownedUserFileBeforeMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "target");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            Directory.CreateDirectory(Path.Combine(destination, "resources"));
            await File.WriteAllTextAsync(Path.Combine(source, "UniversalDeviceToolkit.exe"), "new-shell");
            await File.WriteAllTextAsync(Path.Combine(source, "new-library.dll"), "new-library");
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), "[\"UniversalDeviceToolkit.exe\",\"new-library.dll\"]");
            await File.WriteAllTextAsync(Path.Combine(destination, "UniversalDeviceToolkit.exe"), "old-shell");
            await File.WriteAllTextAsync(Path.Combine(destination, "new-library.dll"), "user-file");
            var previousManifest = Path.Combine(destination, "resources", "install-files.json");
            const string ownership = "[\"UniversalDeviceToolkit.exe\",\"resources/install-files.json\"]";
            await File.WriteAllTextAsync(previousManifest, ownership);
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination, language = "en", deviceMode = "auto" }));
            var registered = false;
            var error = await Assert.ThrowsAsync<IOException>(() => new InstallPayload(source).InstallAsync(options,
                new Progress<object>(), _ => { registered = true; return Task.CompletedTask; }));

            Assert.Contains("unowned file", error.Message);
            Assert.False(registered);
            Assert.Equal("old-shell", await File.ReadAllTextAsync(Path.Combine(destination, "UniversalDeviceToolkit.exe")));
            Assert.Equal("user-file", await File.ReadAllTextAsync(Path.Combine(destination, "new-library.dll")));
            Assert.Equal(ownership, await File.ReadAllTextAsync(previousManifest));
            Assert.Empty(Directory.GetDirectories(root, ".udt-*"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class InlineProgress(Action<object> report) : IProgress<object>
    {
        public void Report(object value) => report(value);
    }

    [Fact]
    public void Destination_RejectsRootsAndPayloadAncestorsButAllowsPreservedUserFiles()
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
            InstallPayload.ValidateDestination(unrelated, source);
            Assert.Equal("keep", File.ReadAllText(Path.Combine(unrelated, "keep.txt")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reinstall_AfterUninstallPreservesUserFilesAndRollsBackRegistrationFailure(bool fail)
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "target with spaces");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(source, "UniversalDeviceToolkit.exe"), "new-shell");
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), "[\"UniversalDeviceToolkit.exe\"]");
            var sentinel = Path.Combine(destination, "user-owned-sentinel.txt");
            await File.WriteAllTextAsync(sentinel, "keep after uninstall");
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination, language = "en", deviceMode = "auto" }));
            var registered = false;
            Task Register(string executable)
            {
                registered = true;
                Assert.Equal("new-shell", File.ReadAllText(executable));
                return fail ? Task.FromException(new IOException("Registration failed")) : Task.CompletedTask;
            }
            var payload = new InstallPayload(source);
            if (fail)
                await Assert.ThrowsAsync<IOException>(() => payload.InstallAsync(options, new Progress<object>(), Register));
            else
                await payload.InstallAsync(options, new Progress<object>(), Register);
            Assert.True(registered);
            Assert.Equal("keep after uninstall", await File.ReadAllTextAsync(sentinel));
            Assert.Equal(!fail, File.Exists(Path.Combine(destination, "UniversalDeviceToolkit.exe")));
            Assert.Empty(Directory.GetDirectories(root, ".udt-*"));
            if (fail)
                Assert.Equal([sentinel], Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Reinstall_RejectsUnownedCollisionWithoutAnInstallationManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "target");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(source, "UniversalDeviceToolkit.exe"), "new-shell");
            await File.WriteAllTextAsync(Path.Combine(source, "user-file.dll"), "new-library");
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"),
                "[\"UniversalDeviceToolkit.exe\",\"user-file.dll\"]");
            var sentinel = Path.Combine(destination, "user-file.dll");
            await File.WriteAllTextAsync(sentinel, "unowned file");
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination, language = "en", deviceMode = "auto" }));
            var error = await Assert.ThrowsAsync<IOException>(() => new InstallPayload(source).CopyAsync(options, new Progress<object>()));
            Assert.Contains("unowned file", error.Message);
            Assert.Equal("unowned file", await File.ReadAllTextAsync(sentinel));
            Assert.False(File.Exists(Path.Combine(destination, "UniversalDeviceToolkit.exe")));
            Assert.Empty(Directory.GetDirectories(root, ".udt-*"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("file\r\nowned.dll")]
    [InlineData("file.dll:stream")]
    [InlineData("folder/*")]
    [InlineData("folder/../owned.dll")]
    [InlineData("folder/owned.dll.")]
    public async Task EscapingManifest_IsRejectedBeforeWritingDestination(string entry)
    {
        var root = Path.Combine(Path.GetTempPath(), "udt-install-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "resources", "setup"));
            await File.WriteAllTextAsync(Path.Combine(source, "resources", "setup", "files.json"), JsonSerializer.Serialize(new[] { entry }));
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new { destination = Path.Combine(root, "target"), language = "en", deviceMode = "auto" }));
            await Assert.ThrowsAsync<InvalidDataException>(() => new InstallPayload(source).CopyAsync(options, new Progress<object>()));
            Assert.False(Directory.Exists(options.Destination));
        }
        finally { Directory.Delete(root, true); }
    }
}
