using UniversalDeviceToolkit.Platform.Linux.Lifecycle;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class LinuxAutorunManagerTests
{
    [Fact]
    public async Task EnableDisable_ShouldWriteAndRemoveXdgDesktopFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "udt-autorun-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var desktop = Path.Combine(directory, LinuxAutorunManager.DesktopFileName);
        var manager = new LinuxAutorunManager(desktop, () => "/opt/Universal Device Toolkit/udt");

        try
        {
            Assert.False(await manager.IsEnabledAsync());
            await manager.EnableAsync();
            Assert.True(await manager.IsEnabledAsync());

            var contents = await File.ReadAllTextAsync(desktop);
            Assert.Contains("Type=Application", contents, StringComparison.Ordinal);
            Assert.Contains("Exec=\"/opt/Universal Device Toolkit/udt\"", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("/usr/local/bin/udt", contents, StringComparison.Ordinal);

            await manager.DisableAsync();
            Assert.False(await manager.IsEnabledAsync());
            Assert.False(File.Exists(desktop));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
