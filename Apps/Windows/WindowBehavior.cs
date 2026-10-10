using System.Text.Json;

namespace UniversalDeviceToolkit.Windows;

internal sealed class WindowBehavior(Func<Task<JsonElement>> readApplicationSettings, Action<string> log)
{
    private readonly object _gate = new();
    private long _refreshGeneration;
    private bool _minimizeToTray = true;
    private bool _minimizeOnClose;
    private bool? _uiActive;

    internal bool MinimizeToTray
    {
        get { lock (_gate) return _minimizeToTray; }
    }

    internal bool ShouldHideOnClose
    {
        get { lock (_gate) return _minimizeToTray || _minimizeOnClose; }
    }

    internal bool TryUpdateUiActivity(bool active)
    {
        lock (_gate)
        {
            if (_uiActive == active) return false;
            _uiActive = active;
            return true;
        }
    }

    internal Task OnHostEventAsync(string name, JsonElement data)
    {
        if (name == "host.ready")
        {
            lock (_gate) _uiActive = null;
            return RefreshAsync();
        }
        if (name == "settings.changed" && (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("scope", out var scope)
            || scope.ValueKind == JsonValueKind.String && scope.GetString() == "application"))
            return RefreshAsync();
        return Task.CompletedTask;
    }

    internal async Task RefreshAsync()
    {
        long generation;
        lock (_gate) generation = ++_refreshGeneration;
        try
        {
            var result = await readApplicationSettings();
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty("value", out var value)
                || value.ValueKind != JsonValueKind.Object) return;
            var minimizeToTray = ReadBoolean(value, "MinimizeToTray");
            var minimizeOnClose = ReadBoolean(value, "MinimizeOnClose");
            lock (_gate)
            {
                if (generation != _refreshGeneration) return;
                if (minimizeToTray.HasValue) _minimizeToTray = minimizeToTray.Value;
                if (minimizeOnClose.HasValue) _minimizeOnClose = minimizeOnClose.Value;
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException)
        {
            log($"Unable to read tray behavior setting: {error.Message}");
        }
    }

    private static bool? ReadBoolean(JsonElement value, string name) => value.TryGetProperty(name, out var setting)
        && setting.ValueKind is JsonValueKind.False or JsonValueKind.True ? setting.GetBoolean() : null;
}
