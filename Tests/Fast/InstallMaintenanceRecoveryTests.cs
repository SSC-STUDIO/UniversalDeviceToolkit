using System.Diagnostics;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class InstallMaintenanceRecoveryTests
{
    [Theory]
    [InlineData("UniversalDeviceToolkit.Host.exe", 0)]
    [InlineData("resources/host/UniversalDeviceToolkit.Host.exe", 0)]
    [InlineData("UniversalDeviceToolkit.Host.exe", 1)]
    public async Task Replacement_UsesNewHostAndRejectsRecoveryFailure(string relative, int exitCode)
    {
        var source = Path.Combine(Path.GetTempPath(), "udt-new-recovery-" + Guid.NewGuid().ToString("N"));
        var host = Path.GetFullPath(Path.Combine(source, relative));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(host) ?? source);
            await File.WriteAllTextAsync(host, "fake new Host");
            string? invoked = null;
            Task<int> Execute(string executable)
            {
                invoked = executable;
                return Task.FromResult(exitCode);
            }
            if (exitCode == 0) await InstallCommand.RestoreNetworkStateAsync(source, Execute);
            else await Assert.ThrowsAsync<IOException>(() => InstallCommand.RestoreNetworkStateAsync(source, Execute));
            Assert.Equal(host, invoked);
        }
        finally { if (Directory.Exists(source)) Directory.Delete(source, true); }
    }

    [Fact]
    public async Task Replacement_MissingNewHostDoesNotInvokeMaintenance()
    {
        var invoked = false;
        await Assert.ThrowsAsync<FileNotFoundException>(() => InstallCommand.RestoreNetworkStateAsync(
            Path.Combine(Path.GetTempPath(), "udt-absent-" + Guid.NewGuid().ToString("N")),
            _ => { invoked = true; return Task.FromResult(0); }));
        Assert.False(invoked);
    }

    [Fact]
    public async Task TimedOutMaintenance_StopsItsOwnProcessBeforeReturning()
    {
        var directory = Path.Combine(Path.GetTempPath(), "udt-maintenance-timeout-" + Guid.NewGuid().ToString("N"));
        var marker = Path.Combine(directory, "process.pid");
        Process? child = null;
        try
        {
            Directory.CreateDirectory(directory);
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(powershell);
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("[IO.File]::WriteAllText('" + marker.Replace("'", "''") + "', $PID.ToString()); Start-Sleep -Seconds 60");
            var running = InstallCommand.RunRecoveryProcessAsync(start, TimeSpan.FromSeconds(10));
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!File.Exists(marker) && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.True(File.Exists(marker), "The isolated maintenance process did not start.");
            child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(marker)));
            _ = child.Handle;
            await Assert.ThrowsAsync<TimeoutException>(() => running);
            Assert.True(child.HasExited);
        }
        finally
        {
            if (child != null)
            {
                if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
                child.Dispose();
            }
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
