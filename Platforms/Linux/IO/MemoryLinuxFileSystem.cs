using System.Text.RegularExpressions;

namespace UniversalDeviceToolkit.Platform.Linux.IO;

/// <summary>
/// Dictionary-backed procfs/sysfs for unit tests. Keys are absolute Unix paths.
/// </summary>
public sealed class MemoryLinuxFileSystem : ILinuxFileSystem
{
    private readonly Dictionary<string, string> _files;

    public MemoryLinuxFileSystem(IReadOnlyDictionary<string, string> files)
    {
        _files = new Dictionary<string, string>(files, StringComparer.Ordinal);
    }

    public void Set(string path, string contents) => _files[path] = contents;

    public bool DirectoryExists(string path) =>
        _files.Keys.Any(file => file.StartsWith(TrimSlash(path) + "/", StringComparison.Ordinal));

    public bool FileExists(string path) => _files.ContainsKey(path);

    public string? ReadText(string path) =>
        _files.TryGetValue(path, out var value) ? value : null;

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        var prefix = TrimSlash(path) + "/";
        return _files.Keys
            .Where(file => file.StartsWith(prefix, StringComparison.Ordinal))
            .Select(file =>
            {
                var relative = file[prefix.Length..];
                var separator = relative.IndexOf('/');
                return separator < 0 ? string.Empty : prefix + relative[..separator];
            })
            .Where(directory => directory.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> EnumerateFiles(string path, string searchPattern)
    {
        var prefix = TrimSlash(path) + "/";
        var regex = new Regex(
            "^" + Regex.Escape(searchPattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return _files.Keys
            .Where(file => file.StartsWith(prefix, StringComparison.Ordinal))
            .Where(file => !file[prefix.Length..].Contains('/'))
            .Where(file => regex.IsMatch(Path.GetFileName(file)))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string TrimSlash(string path) => path.TrimEnd('/');
}
