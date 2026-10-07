using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal sealed record ShellConfiguration(string HostPath, string UiDirectory, string DataDirectory, string[] HostArguments, JsonElement? InstallerSelection)
{
    public bool StartMinimized { get; init; }
    public bool Diagnostic { get; init; }

    internal static string[] BuildRestartArguments(IReadOnlyList<string> arguments, int previousPid)
    {
        var result = new List<string>(arguments.Count + 2);
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == "--restart-after")
            {
                if (index + 1 < arguments.Count && int.TryParse(arguments[index + 1], out _)) index++;
                continue;
            }
            result.Add(arguments[index]);
        }
        result.Add("--restart-after");
        result.Add(previousPid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return result.ToArray();
    }

    public static ShellConfiguration Load(string[] arguments)
    {
        var root = AppContext.BaseDirectory;
        var dataOverride = Environment.GetEnvironmentVariable("UDT_APPDATA_OVERRIDE");
        var data = string.IsNullOrWhiteSpace(dataOverride)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalDeviceToolkit")
            : Path.GetFullPath(dataOverride);
        var host = Path.Combine(root, "UniversalDeviceToolkit.Host.exe");
        var ui = Path.Combine(root, "resources", "ui");
        var hostArguments = new List<string>();
        var allArguments = arguments.Concat(ReadExternalArguments(Path.Combine(data, "args.txt"))).ToArray();
        for (var index = 0; index < allArguments.Length; index++)
        {
            var argument = allArguments[index];
            if (argument is "--host" or "--ui")
            {
                if (++index >= allArguments.Length) throw new ArgumentException($"Missing value for {argument}.");
                if (argument == "--host") host = Path.GetFullPath(allArguments[index]);
                else ui = Path.GetFullPath(allArguments[index]);
            }
            else if (argument == "--restart-after")
            {
                if (index + 1 < allArguments.Length && int.TryParse(allArguments[index + 1], out _)) index++;
            }
            else if (argument is not "--diagnose" and not "--diagnose-ui" and not "--minimized" and not "--elevation-checked")
                hostArguments.Add(argument);
        }
        var selection = ReadInstallerSelection(Path.Combine(root, "installer-selection.ini"));
        if (selection?.GetProperty("deviceMode").GetString() == "basic") hostArguments.Add("--no-hardware");
        return new ShellConfiguration(host, ui, data, hostArguments.ToArray(), selection)
        {
            StartMinimized = allArguments.Contains("--minimized", StringComparer.OrdinalIgnoreCase),
            Diagnostic = allArguments.Contains("--diagnose-ui", StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IEnumerable<string> ReadExternalArguments(string path)
    {
        try
        {
            return File.ReadLines(path).Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    internal static JsonElement? ReadInstallerSelection(string path)
    {
        if (!File.Exists(path)) return null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inSection = false;
        foreach (var line in File.ReadLines(path))
        {
            var text = line.Trim();
            if (text.StartsWith('[') && text.EndsWith(']')) { inSection = text[1..^1].Trim().Equals("installation", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSection || text.StartsWith(';') || text.StartsWith('#')) continue;
            var separator = text.IndexOf('=');
            if (separator > 0) values[text[..separator].Trim()] = text[(separator + 1)..].Trim();
        }
        var features = new Dictionary<string, bool>();
        foreach (var name in new[] { "windowsOptimization", "networkAcceleration", "automation", "macro", "keyboard" })
            features[name] = !values.TryGetValue(name, out var value) || value.ToLowerInvariant() is not ("0" or "false" or "no" or "off");
        features["networkAcceleration"] &= features["windowsOptimization"];
        var language = values.GetValueOrDefault("language", "");
        var languages = new HashSet<string>(["en", "zh-CN", "zh-Hant", "ja", "de", "fr", "es", "it", "pt-BR", "pt", "ru", "uk", "pl", "cs", "sk", "hu", "ro", "bg", "tr", "el", "ar", "lv", "nl-NL", "vi", "uz-Latn-UZ"]);
        if (!languages.Contains(language) || values.GetValueOrDefault("deviceMode") is not ("basic" or "auto")) return null;
        return JsonSerializer.SerializeToElement(new { language, deviceMode = values["deviceMode"], features });
    }
}
