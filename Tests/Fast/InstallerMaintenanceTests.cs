using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class InstallerMaintenanceTests
{
    [Fact]
    public void LegacyBackupWarning_IsWrittenSynchronously()
    {
        using var output = new StringWriter();
        var progress = new ConsoleInstallProgress(output);

        progress.Report(new { phase = "copying", message = "copy" });
        Assert.Equal(string.Empty, output.ToString());
        progress.Report(new { phase = "warning", message = @"Legacy files preserved at C:\backup with spaces" });

        Assert.Contains(@"C:\backup with spaces", output.ToString());
    }

    [Fact]
    public void ShortcutRollback_DoesNotRewriteUnchangedLockedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "udt-shortcut-rollback-" + Guid.NewGuid().ToString("N"));
        var shortcut = Path.Combine(directory, "saved.lnk");
        try
        {
            Directory.CreateDirectory(directory);
            var original = new byte[] { 1, 2, 3 };
            File.WriteAllBytes(shortcut, original);
            using (File.Open(shortcut, FileMode.Open, FileAccess.Read, FileShare.Read))
                InstallCommand.RestoreShortcut(shortcut, original);

            Assert.Equal(original, File.ReadAllBytes(shortcut));
            Directory.Delete(directory, true);
            InstallCommand.RestoreShortcut(shortcut, null);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
