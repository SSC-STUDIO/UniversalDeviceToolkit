using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal sealed record TrayMenuEntry(string Label, string? Command = null, bool Checked = false,
    IReadOnlyList<TrayMenuEntry>? Children = null, bool Enabled = true);

/// <summary>Projects Host state into tray commands without changing hardware policy.</summary>
internal sealed class TrayMenu(
    Func<string, object?, CancellationToken, Task<JsonElement>> invoke,
    JsonElement? installerSelection,
    Action<string> log)
{
    private static readonly (string Route, string Label, string? Feature, string? Capability)[] Navigation =
    [
        ("/dashboard", "dashboard", null, null),
        ("/keyboard", "keyboard", "keyboard", "keyboard"),
        ("/automation", "automation", "automation", "automation"),
        ("/macro", "macro", "macro", "macro"),
        ("/optimization", "windowsOptimization", "windowsOptimization", "optimization")
    ];

    internal async Task<IReadOnlyList<TrayMenuEntry>> LoadAsync(string language, CancellationToken cancellationToken)
    {
        string Label(string key) => ShellStrings.Get(language, key);
        var features = Property(installerSelection ?? default, "features");
        var requests = await Task.WhenAll(
            QueryAsync("settings.get", new { scope = "application" }, cancellationToken),
            QueryAsync("host.getCapabilities", null, cancellationToken),
            QueryAsync("features.isSupported", new { feature = "powerMode" }, cancellationToken),
            IsEnabled(features, "automation") ? QueryAsync("automation.getState", null, cancellationToken) : Task.FromResult(default(JsonElement)),
            QueryAsync("sensors.get", null, cancellationToken));
        var application = Property(requests[0], "value");
        var visibility = Property(application, "navigationItemsVisibility");
        if (visibility.ValueKind != JsonValueKind.Object) visibility = Property(application, "NavigationItemsVisibility");
        var capabilities = Property(requests[1], "capabilities");
        var charge = Property(Property(requests[4], "battery"), "chargeLevel");
        var badge = charge.ValueKind == JsonValueKind.Number && charge.TryGetDouble(out var percent) && percent is >= 0 and <= 100
            ? $" ({Math.Round(percent)}%)" : "";
        var entries = new List<TrayMenuEntry> { new("Universal Device Toolkit" + badge, Enabled: false) };

        if (Property(requests[2], "value").ValueKind == JsonValueKind.True)
        {
            var power = await Task.WhenAll(
                QueryAsync("features.get", new { feature = "powerMode" }, cancellationToken),
                QueryAsync("features.getAllStates", new { feature = "powerMode" }, cancellationToken));
            var current = Text(Property(power[0], "value"));
            var states = Property(power[1], "value");
            if (states.ValueKind == JsonValueKind.Array)
            {
                var choices = states.EnumerateArray().Where(state => state.ValueKind == JsonValueKind.String)
                    .Select(state => Text(state)).Where(state => state.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(state => new TrayMenuEntry(Label(state.ToLowerInvariant() switch
                    {
                        "godmode" or "custom" => "custom",
                        "quiet" or "balance" or "performance" or "extreme" => state.ToLowerInvariant(),
                        _ => state
                    }), "powerMode:" + state, state.Equals(current, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (choices.Length > 0) entries.Add(new TrayMenuEntry(Label("powerMode"), Children: choices));
            }
        }
        entries.Add(new TrayMenuEntry(""));
        foreach (var (route, label, feature, capability) in Navigation)
            if (IsEnabled(features, feature) && IsEnabled(visibility, feature) && IsEnabled(capabilities, capability))
                entries.Add(new TrayMenuEntry(Label(label), "nav:" + route));

        var pipelines = Property(requests[3], "pipelines");
        if (IsEnabled(capabilities, "automation") && pipelines.ValueKind == JsonValueKind.Array)
        {
            var actions = pipelines.EnumerateArray().Reverse().Where(pipeline =>
                Property(pipeline, "trigger").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
            var separatorAdded = false;
            foreach (var pipeline in actions)
            {
                var id = Text(Property(pipeline, "id"));
                if (id.Length == 0) continue;
                if (!separatorAdded) { entries.Add(new TrayMenuEntry("")); separatorAdded = true; }
                var name = Text(Property(pipeline, "name"));
                if (name.Length == 0) name = Label("unnamed");
                else if (name is "__udt.quickAction.deactivateGpu" or "Deactivate GPU" or "停用 GPU" or "停用GPU" or "強制休眠獨顯")
                    name = Label("deactivateGpu");
                entries.Add(new TrayMenuEntry(name, "run:" + id));
            }
        }
        entries.Add(new TrayMenuEntry(""));
        entries.Add(new TrayMenuEntry(Label("open"), "open"));
        entries.Add(new TrayMenuEntry(Label("close"), "close"));
        return entries;
    }

    internal async Task ExecuteAsync(string command, Action<string?> open, Action quit, CancellationToken cancellationToken)
    {
        if (command == "open") open(null);
        else if (command == "close") quit();
        else if (command.StartsWith("nav:", StringComparison.Ordinal) && Navigation.Any(item => item.Route == command[4..])) open(command[4..]);
        else if (command.StartsWith("run:", StringComparison.Ordinal) && command.Length > 4)
            await invoke("automation.runNow", new { pipelineId = command[4..] }, cancellationToken);
        else if (command.StartsWith("powerMode:", StringComparison.Ordinal) && command.Length > 10)
            await invoke("features.set", new { feature = "powerMode", value = command[10..] }, cancellationToken);
        else throw new ArgumentException("Unknown tray command.", nameof(command));
    }

    private async Task<JsonElement> QueryAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        try { return await invoke(method, parameters, cancellationToken); }
        catch (Exception error) when (error is IOException or TimeoutException or OperationCanceledException)
        {
            log($"Tray {method}: {error.Message}");
            return default;
        }
    }

    private static JsonElement Property(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) ? property : default;
    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static bool IsEnabled(JsonElement values, string? key) => key == null || Property(values, key).ValueKind != JsonValueKind.False;
}
