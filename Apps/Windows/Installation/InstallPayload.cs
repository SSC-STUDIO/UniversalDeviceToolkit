using System.Text;
using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal sealed record InstallOptions(string Destination, string Language, string DeviceMode, Dictionary<string, bool> Features)
{
    internal string Selection => "[installation]\n" + $"language={Language}\ndeviceMode={DeviceMode}\n"
        + string.Join("\n", Features.Select(feature => $"{feature.Key}={(feature.Value ? 1 : 0)}")) + "\n";

    internal static InstallOptions Parse(JsonElement value)
    {
        var destination = value.GetProperty("destination").GetString() ?? "";
        var language = value.GetProperty("language").GetString() ?? "";
        var deviceMode = value.GetProperty("deviceMode").GetString() ?? "";
        if (!Path.IsPathFullyQualified(destination) || destination.Contains('"') || destination.Any(char.IsControl))
            throw new ArgumentException("Choose an absolute installation folder.");
        if (!ShellStrings.IsSupportedLanguage(language) || deviceMode is not ("auto" or "basic"))
            throw new ArgumentException("Unsupported installation language or device mode.");
        var raw = value.TryGetProperty("features", out var features) ? features : default;
        var selected = new Dictionary<string, bool>();
        foreach (var key in new[] { "windowsOptimization", "networkAcceleration", "automation", "macro", "keyboard" })
            selected[key] = raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty(key, out var flag)
                || flag.ValueKind != JsonValueKind.False;
        selected["networkAcceleration"] &= selected["windowsOptimization"];
        return new InstallOptions(Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination)), language, deviceMode, selected);
    }
}

/// <summary>Copies only the packaged manifest, preserving installer selection contracts.</summary>
internal sealed class InstallPayload(string source)
{
    internal string[] Files { get; } = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(source, "resources", "setup", "files.json")))
        ?? throw new InvalidDataException("The installation manifest is missing.");

    internal long Size => Files.Sum(file => new FileInfo(Resolve(source, file)).Length);

    internal async Task<string> CopyAsync(InstallOptions options, IProgress<object> progress)
        => await InstallAsync(options, progress, _ => Task.CompletedTask);

    internal async Task<string> InstallAsync(InstallOptions options, IProgress<object> progress, Func<string, Task> register)
    {
        ValidateDestination(options.Destination, source);
        var ownedManifest = "resources/install-files.json";
        var uninstallManifest = "resources/install-files.txt";
        foreach (var file in Files) _ = Resolve(source, file);
        var selected = Files.Where(file => options.Features["networkAcceleration"] || !IsNetworkProxy(file)).ToArray();
        var owned = selected.Append("installer-selection.ini").Append("Uninstall.exe")
            .Append(ownedManifest).Append(uninstallManifest).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var previousManifest = Resolve(options.Destination, ownedManifest);
        CheckParents(previousManifest);
        var previous = File.Exists(previousManifest)
            ? JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(previousManifest))
                ?? throw new InvalidDataException("The previous installation manifest is invalid.")
            : Array.Empty<string>();
        var affected = owned.Concat(previous)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var file in selected)
        {
            if (!File.Exists(Resolve(source, file))) throw new FileNotFoundException("An installation file is missing.", file);
        }
        // Validate both manifests and all existing file locks before writing.
        foreach (var file in affected)
        {
            var target = Resolve(options.Destination, file);
            CheckParents(target);
            if (File.Exists(target))
            {
                using var probe = File.Open(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }
        var parent = Path.GetDirectoryName(options.Destination) ?? throw new ArgumentException("Invalid installation folder.");
        var stage = Path.Combine(parent, ".udt-stage-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(parent, ".udt-backup-" + Guid.NewGuid().ToString("N"));
        var moved = new List<string>();
        var written = new List<string>();
        var committed = false;
        var rolledBack = false;
        var existed = Directory.Exists(options.Destination);
        try
        {
            var total = selected.Sum(file => new FileInfo(Resolve(source, file)).Length);
            long completed = 0;
            foreach (var file in selected)
            {
                var target = Resolve(stage, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? stage);
                await using (var input = File.OpenRead(Resolve(source, file)))
                await using (var output = File.Create(target))
                    await input.CopyToAsync(output);
                completed += new FileInfo(target).Length;
                progress.Report(new { phase = "copying", percent = total == 0 ? 100 : completed * 100.0 / total, completedBytes = completed, totalBytes = total, file });
            }
            Directory.CreateDirectory(Path.Combine(stage, "resources"));
            await File.WriteAllTextAsync(Resolve(stage, ownedManifest), JsonSerializer.Serialize(owned));
            // NSIS reads this selected ownership list without a JSON plugin or a WebView2 dependency.
            var uninstallFiles = owned.Select(file => file.Replace('/', '\\'));
            await File.WriteAllTextAsync(Resolve(stage, uninstallManifest), string.Join("\r\n", uninstallFiles) + "\r\n",
                new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true));
            await File.WriteAllTextAsync(Resolve(stage, "installer-selection.ini"), options.Selection);
            foreach (var file in affected)
            {
                var target = Resolve(options.Destination, file);
                if (!File.Exists(target)) continue;
                var saved = Resolve(backup, file);
                Directory.CreateDirectory(Path.GetDirectoryName(saved) ?? backup);
                File.Move(target, saved);
                moved.Add(file);
            }
            foreach (var file in selected.Append("installer-selection.ini").Append(ownedManifest).Append(uninstallManifest).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var target = Resolve(options.Destination, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? options.Destination);
                File.Move(Resolve(stage, file), target);
                written.Add(file);
            }
            var executable = Path.Combine(options.Destination, "UniversalDeviceToolkit.exe");
            if (!written.Contains("Uninstall.exe", StringComparer.OrdinalIgnoreCase)) written.Add("Uninstall.exe");
            await register(executable);
            committed = true;
            return executable;
        }
        catch (Exception failure)
        {
            var failures = new List<Exception> { failure };
            foreach (var file in written)
            {
                try { File.Delete(Resolve(options.Destination, file)); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add(error); }
            }
            foreach (var file in moved)
            {
                try { File.Move(Resolve(backup, file), Resolve(options.Destination, file), true); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failures.Add(error); }
            }
            if (failures.Count > 1)
                throw new AggregateException("Installation rollback was incomplete. The previous files are retained at " + backup, failures);
            rolledBack = true;
            if (!existed && Directory.Exists(options.Destination)) RemoveEmptyDirectories(options.Destination);
            throw;
        }
        finally
        {
            Cleanup(stage, progress);
            if (committed || rolledBack)
                Cleanup(backup, progress);
        }
    }

    private static void Cleanup(string directory, IProgress<object> progress)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            progress.Report(new { phase = "cleanup-warning", directory, error = error.Message });
        }
    }

    private static void RemoveEmptyDirectories(string directory)
    {
        foreach (var child in Directory.EnumerateDirectories(directory)) RemoveEmptyDirectories(child);
        if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
    }

    internal static void ValidateDestination(string destination, string source)
    {
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var origin = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        if (string.Equals(target, Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase)
            || IsWithin(target, origin) || IsWithin(origin, target))
            throw new ArgumentException("Choose a dedicated folder outside the installer payload.");
        CheckParents(target);
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()
            && !File.Exists(Path.Combine(target, "UniversalDeviceToolkit.exe")))
            throw new ArgumentException("Choose an empty folder or an existing Universal Device Toolkit installation.");
    }

    private static bool IsWithin(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string Resolve(string root, string relative)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 950 || Path.IsPathRooted(relative)
            || relative.Any(character => char.IsControl(character) || "\"<>:|*?".Contains(character))
            || relative.Split(['/', '\\']).Any(part => part.Length == 0 || part is "." or ".."
                || part.EndsWith('.') || part.EndsWith(' ')))
            throw new InvalidDataException("Invalid installation manifest entry.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsWithin(path, root) || path.Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Installation manifest entry escapes the payload.");
        return path;
    }

    private static void CheckParents(string path)
    {
        for (var current = path; current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The installation path must not pass through a symbolic link or junction.");
    }

    private static bool IsNetworkProxy(string relative) => Path.GetFileName(relative)
        .StartsWith("UniversalDeviceToolkit.NetworkProxy.", StringComparison.OrdinalIgnoreCase);
}
