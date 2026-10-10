using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Windows;

internal static class InstallCommand
{
    internal static int Run(string[] arguments)
    {
        try
        {
            string Argument(string name)
            {
                var index = Array.IndexOf(arguments, name);
                return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1]
                    : throw new ArgumentException("Missing installer argument: " + name);
            }
            var destination = Argument("--destination");
            var selection = ShellConfiguration.ReadInstallerSelection(Argument("--selection"))
                ?? throw new InvalidDataException("Invalid installation selection.");
            var options = InstallOptions.Parse(JsonSerializer.SerializeToElement(new
            {
                destination, language = selection.GetProperty("language").GetString(),
                deviceMode = selection.GetProperty("deviceMode").GetString(), features = selection.GetProperty("features")
            }));
            InstallPayload.ValidateDestination(options.Destination, Argument("--source"));
            StopInstalledProcesses(options.Destination);
            RestoreNetworkStateAsync(Argument("--source")).GetAwaiter().GetResult();
            new InstallPayload(Argument("--source")).InstallAsync(options, new ConsoleInstallProgress(Console.Out),
                _ => RegisterAsync(Argument("--source"), options.Destination)).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    internal static void StopInstalledProcesses(string destination)
    {
        var application = Path.Combine(Path.GetFullPath(destination), "UniversalDeviceToolkit.exe");
        foreach (var process in Process.GetProcessesByName("UniversalDeviceToolkit"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                string? executable;
                try { executable = process.MainModule?.FileName; }
                catch (System.ComponentModel.Win32Exception error)
                {
                    Console.Error.WriteLine($"Unable to inspect process {process.Id}: {error.Message}");
                    continue;
                }
                if (!string.Equals(executable, application, StringComparison.OrdinalIgnoreCase)) continue;
                process.CloseMainWindow();
                if (!process.WaitForExit(2000)) process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000)) throw new IOException("The previous application is still running.");
            }
        }
    }

    internal static async Task RestoreNetworkStateAsync(string source, Func<string, Task<int>>? execute = null)
    {
        var root = Path.GetFullPath(source);
        var host = new[]
        {
            Path.Combine(root, "UniversalDeviceToolkit.Host.exe"),
            Path.Combine(root, "resources", "host", "UniversalDeviceToolkit.Host.exe")
        }.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("The new package has no network recovery Host.");
        var exitCode = await (execute ?? RunRecoveryProcessAsync)(host).ConfigureAwait(false);
        if (exitCode != 0) throw new IOException($"Network recovery failed ({exitCode}); the existing installation was retained.");
    }

    private static Task<int> RunRecoveryProcessAsync(string host)
    {
        var start = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--restore-network-state");
        return RunRecoveryProcessAsync(start, TimeSpan.FromSeconds(30));
    }

    internal static async Task<int> RunRecoveryProcessAsync(ProcessStartInfo start, TimeSpan timeout)
    {
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new IOException("Unable to start the network recovery Host.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            Console.Out.Write(await output.ConfigureAwait(false));
            Console.Error.Write(await error.ConfigureAwait(false));
            throw new TimeoutException("Network recovery timed out; its maintenance process was stopped and replacement was aborted.");
        }
        Console.Out.Write(await output.ConfigureAwait(false));
        Console.Error.Write(await error.ConfigureAwait(false));
        return process.ExitCode;
    }

    internal static async Task RegisterAsync(string source, string destination)
    {
        using var nativeRegistry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var legacyRegistry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        var registryTransaction = new InstallationRegistryTransaction([nativeRegistry, legacyRegistry], destination);
        var shortcutPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Universal Device Toolkit.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Universal Device Toolkit", "Universal Device Toolkit.lnk")
        };
        var shortcuts = shortcutPaths.ToDictionary(path => path, path => File.Exists(path) ? File.ReadAllBytes(path) : null);
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Path.Combine(source, "resources", "setup", "register.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, Arguments = "/S /D=" + destination
            }) ?? throw new IOException("Unable to register the installation.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new IOException($"Installation registration failed ({process.ExitCode}).");
            // Older packages used generated keys and sometimes the 32-bit view.
            // The transaction owns only records for this exact installation.
            registryTransaction.RemoveDuplicateRecords();
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            failures.AddRange(registryTransaction.Restore());
            foreach (var (path, content) in shortcuts)
            {
                try
                {
                    RestoreShortcut(path, content);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add(error); }
            }
            if (failures.Count > 1) throw new AggregateException("Installation registration could not be restored completely.", failures);
            throw;
        }
    }

    internal static void RestoreShortcut(string path, byte[]? content)
    {
        if (content == null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(content)) return;
        var directory = Path.GetDirectoryName(path);
        if (directory != null) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, content);
    }
}
