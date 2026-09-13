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
    {
        ValidateDestination(options.Destination, source);
        // Validate every entry before writing anything, including reparse points.
        foreach (var file in Files)
        {
            if (!File.Exists(Resolve(source, file))) throw new FileNotFoundException("An installation file is missing.", file);
            var target = Resolve(options.Destination, file);
            CheckParents(target);
            if (File.Exists(target))
            {
                // Fail before copying when an existing app still holds its files.
                using var probe = File.Open(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }
        var selected = Files.Where(file => options.Features["networkAcceleration"] || !IsNetworkProxy(file)).ToArray();
        var total = selected.Sum(file => new FileInfo(Resolve(source, file)).Length);
        long completed = 0;
        Directory.CreateDirectory(options.Destination);
        foreach (var file in selected)
        {
            var target = Resolve(options.Destination, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? throw new InvalidDataException("Invalid target path."));
            await using (var input = File.OpenRead(Resolve(source, file)))
            await using (var output = File.Create(target))
                await input.CopyToAsync(output);
            completed += new FileInfo(target).Length;
            progress.Report(new { phase = "copying", percent = total == 0 ? 100 : completed * 100.0 / total, completedBytes = completed, totalBytes = total, file });
        }
        if (!options.Features["networkAcceleration"])
            foreach (var file in Files.Where(IsNetworkProxy)) File.Delete(Resolve(options.Destination, file));
        await File.WriteAllTextAsync(Path.Combine(options.Destination, "installer-selection.ini"), options.Selection);
        return Path.Combine(options.Destination, "UniversalDeviceToolkit.exe");
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
        if (Path.IsPathRooted(relative)) throw new InvalidDataException("Absolute installation manifest entry.");
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
