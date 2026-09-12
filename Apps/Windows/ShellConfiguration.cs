using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal sealed record ShellConfiguration(string HostPath, string UiDirectory, string DataDirectory, string[] HostArguments, JsonElement? InstallerSelection)
{
    public static ShellConfiguration Load(string[] arguments)
    {
        var root = AppContext.BaseDirectory;
        var host = Path.Combine(root, "UniversalDeviceToolkit.Host.exe");
        var ui = Path.Combine(root, "resources", "ui");
        var hostArguments = new List<string>();
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument is "--host" or "--ui")
            {
                if (++index >= arguments.Length) throw new ArgumentException($"Missing value for {argument}.");
                if (argument == "--host") host = Path.GetFullPath(arguments[index]);
                else ui = Path.GetFullPath(arguments[index]);
            }
            else if (argument is not "--diagnose" and not "--minimized") hostArguments.Add(argument);
        }
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalDeviceToolkit");
        var selection = ReadInstallerSelection(Path.Combine(root, "installer-selection.ini"));
        if (selection?.GetProperty("deviceMode").GetString() == "basic") hostArguments.Add("--no-hardware");
        return new ShellConfiguration(host, ui, data, hostArguments.ToArray(), selection);
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
