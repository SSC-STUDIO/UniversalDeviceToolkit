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
            new InstallPayload(Argument("--source")).InstallAsync(options, new Progress<object>(),
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
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)) + Path.DirectorySeparatorChar;
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
                if (executable == null || !executable.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                process.CloseMainWindow();
                if (!process.WaitForExit(2000)) process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000)) throw new IOException("The previous application is still running.");
            }
        }
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
                    if (content == null) File.Delete(path);
                    else
                    {
                        var directory = Path.GetDirectoryName(path);
                        if (directory != null) Directory.CreateDirectory(directory);
                        File.WriteAllBytes(path, content);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add(error); }
            }
            if (failures.Count > 1) throw new AggregateException("Installation registration could not be restored completely.", failures);
            throw;
        }
    }
}
