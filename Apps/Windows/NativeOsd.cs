using System.Drawing;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

internal sealed class NativeOsd(HostConnection host, ShellConfiguration configuration, Action<string> log, Action visibilityChanged) : IDisposable
{
    private readonly SemaphoreSlim _changes = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private NativeWindow? _window;
    private CoreWebView2Controller? _controller;
    private JsonElement _settings;
    private JsonElement? _snapshot;
    private JsonElement? _fps;
    private object _format = new { showCpuAverageFrequency = false, displayMemoryInGigabytes = false, temperatureUnit = "C" };
    private bool _visible;
    private bool _subscribed;
    private bool _fpsSubscribed;
    private bool _disposed;
    private bool _positioning;
    private double _width = 320;
    private double _height = 96;
    internal bool IsVisible => _visible;

    internal void OnEvent(string name, JsonElement data)
    {
        if (_disposed) return;
        if (name == "sensors.updated") { _snapshot = data; Render(); }
        else if (name == "sensors.fpsUpdated") { _fps = data; Render(); }
        else if (name == "host.ready" || name == "settings.changed" || name == "osd.changed")
        {
            if (name == "host.ready") { _subscribed = false; _fpsSubscribed = false; }
            _ = RefreshAsync(name == "osd.changed" && data.TryGetProperty("state", out var state) ? state.GetString() : null);
        }
    }

    internal Task ToggleAsync() => RefreshAsync("Toggle");

    private async Task RefreshAsync(string? action)
    {
        try
        {
            await _changes.WaitAsync(_lifetime.Token);
            try
            {
                var osd = await host.InvokeAsync("settings.get", JsonSerializer.SerializeToElement(new { scope = "osd" }), _lifetime.Token);
                _settings = osd.GetProperty("value").Clone();
                var desired = ReadBoolean("ShowOsd");
                if (action != null)
                {
                    desired = action == "Toggle" ? !_visible : action == "Show";
                    await SavePatchAsync(new Dictionary<string, object?> { ["ShowOsd"] = desired });
                }
                var hardware = await host.InvokeAsync("settings.get", JsonSerializer.SerializeToElement(new { scope = "hardwareSensors" }), _lifetime.Token);
                var application = await host.InvokeAsync("settings.get", JsonSerializer.SerializeToElement(new { scope = "application" }), _lifetime.Token);
                var raw = hardware.GetProperty("value");
                _format = new
                {
                    showCpuAverageFrequency = raw.TryGetProperty("ShowCpuAverageFrequency", out var average) && average.ValueKind == JsonValueKind.True,
                    displayMemoryInGigabytes = raw.TryGetProperty("DisplayMemoryInGigabytes", out var gigabytes) && gigabytes.ValueKind == JsonValueKind.True,
                    temperatureUnit = application.GetProperty("value").TryGetProperty("TemperatureUnit", out var unit) && unit.GetString() == "F" ? "F" : "C"
                };
                if (desired)
                {
                    await EnsureWindowAsync();
                    _visible = true;
                    _window?.ConfigureOverlay(ReadBoolean("IsLocked"));
                    Position(false);
                    _window?.ShowOverlay();
                    if (_controller != null) _controller.IsVisible = true;
                }
                else
                {
                    _visible = false;
                    _window?.Hide();
                    if (_controller != null) _controller.IsVisible = false;
                }
                visibilityChanged();
                await SynchronizeSubscriptionsAsync();
                Render();
            }
            finally { _changes.Release(); }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (_disposed) return;
            log("OSD: " + error);
            if (configuration.Diagnostic) return;
            if (ShellRecovery.IsBrowserFailure(error)) ShellRecovery.Show(error, _window?.Handle ?? 0);
            else if (error is IOException && error.Message.StartsWith("OSD navigation", StringComparison.Ordinal)
                && ShellRecovery.Retry(0, error.Message)) await RefreshAsync("Show");
        }
    }

    private async Task EnsureWindowAsync()
    {
        if (_controller != null) return;
        try
        {
            _window = new NativeWindow(Path.Combine(configuration.DataDirectory, "osd-window.json"), error => log(error.ToString()),
                new WindowMetrics(320, 96, 24, 24), overlay: true);
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(configuration.DataDirectory, "WebView2"));
            _controller = await environment.CreateCoreWebView2ControllerAsync(_window.Handle);
            _controller.DefaultBackgroundColor = Color.Transparent;
            var web = _controller.CoreWebView2;
            web.Settings.AreDefaultContextMenusEnabled = false;
            web.Settings.AreDevToolsEnabled = false;
            web.Settings.IsStatusBarEnabled = false;
            web.SetVirtualHostNameToFolderMapping("osd.udt.local", configuration.UiDirectory, CoreWebView2HostResourceAccessKind.Deny);
            web.NavigationStarting += (_, args) => args.Cancel = args.Uri != "https://osd.udt.local/osd.html";
            web.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            web.NewWindowRequested += (_, args) => args.Handled = true;
            web.ProcessFailed += (_, args) =>
            {
                if (args.ProcessFailedKind != CoreWebView2ProcessFailedKind.BrowserProcessExited)
                    _ = RecoverAsync(args.ProcessFailedKind + ": " + args.Reason);
            };
            web.WebMessageReceived += (_, args) =>
            {
                try
                {
                    using var message = JsonDocument.Parse(args.WebMessageAsJson);
                    var data = message.RootElement;
                    if (data.GetProperty("event").GetString() != "osd.size") return;
                    _width = Math.Clamp(data.GetProperty("width").GetDouble(), 24, 3000);
                    _height = Math.Clamp(data.GetProperty("height").GetDouble(), 24, 1600);
                    Position(false);
                }
                catch (Exception error) { log("OSD message: " + error); }
            };
            _window.Resized += Resize;
            _window.Closing += () => _ = RefreshAsync("Hidden");
            _window.MessageReceived += (message, _, _) =>
            {
                if (message == 0x0003) _controller?.NotifyParentWindowPositionChanged();
                if (message is 0x007E or 0x02E0) Position(false);
                if (message == 0x0232 && !_positioning) { Position(true); _ = SavePositionAsync(); }
            };
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            web.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess) loaded.TrySetResult();
                else loaded.TrySetException(new IOException("OSD navigation failed: " + args.WebErrorStatus));
            };
            web.Navigate("https://osd.udt.local/osd.html");
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(20), _lifetime.Token);
            Resize();
        }
        catch
        {
            _controller?.Close();
            _controller = null;
            _window?.Dispose();
            _window = null;
            throw;
        }
    }

    private async Task RecoverAsync(string detail)
    {
        log("OSD process failure: " + detail);
        try
        {
            await _changes.WaitAsync(_lifetime.Token);
            try
            {
                _visible = false;
                _controller?.Close();
                _controller = null;
                _window?.Dispose();
                _window = null;
                visibilityChanged();
                await SynchronizeSubscriptionsAsync();
            }
            finally { _changes.Release(); }
            if (!_disposed && !configuration.Diagnostic && ShellRecovery.Retry(0, detail))
                await RefreshAsync("Show");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!_disposed) log("OSD recovery: " + error);
        }
    }

    private async Task SynchronizeSubscriptionsAsync()
    {
        if (_visible)
        {
            await host.InvokeAsync("sensors.subscribe", JsonSerializer.SerializeToElement(new
            {
                subscriberId = "osd", intervalSec = Math.Clamp(ReadNumber("OsdRefreshInterval", 1), 0.5, 60)
            }), _lifetime.Token);
            _subscribed = true;
        }
        else if (_subscribed)
        {
            await host.InvokeAsync("sensors.unsubscribe", JsonSerializer.SerializeToElement(new { subscriberId = "osd" }), _lifetime.Token);
            _subscribed = false;
        }
        var desiredFps = _visible && _settings.TryGetProperty("Items", out var items)
            && items.EnumerateArray().Any(item => item.GetString() is "Fps" or "LowFps" or "FrameTime");
        if (desiredFps == _fpsSubscribed) return;
        await host.InvokeAsync(desiredFps ? "sensors.subscribeFps" : "sensors.unsubscribeFps", cancellationToken: _lifetime.Token);
        _fpsSubscribed = desiredFps;
    }

    private void Render()
    {
        if (!_visible || _controller == null || _settings.ValueKind != JsonValueKind.Object) return;
        _controller.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            @event = "osd.render", settings = _settings, snapshot = _snapshot, fps = _fps, options = _format
        }));
    }

    private void Position(bool moved)
    {
        if (_window == null) return;
        var work = NativeWindow.GetMonitor(_window.Handle).Work;
        var scale = Win32.GetDpiForWindow(_window.Handle) / 96.0;
        var width = Math.Min(work.Right - work.Left, (int)Math.Ceiling(_width * scale));
        var height = Math.Min(work.Bottom - work.Top, (int)Math.Ceiling(_height * scale));
        var bar = ReadNumber("SelectedStyleIndex", 0) != 0;
        Win32.GetWindowRect(_window.Handle, out var current);
        var x = moved ? current.Left : (int)ReadNumber(bar ? "BarPositionX" : "PanelPositionX", bar ? work.Left + (work.Right - work.Left - width) / 2 : work.Left);
        var y = moved ? current.Top : (int)ReadNumber(bar ? "BarPositionY" : "PanelPositionY", work.Top);
        var snap = ReadNumber("SnapThreshold", 20) * scale;
        if (Math.Abs(x - work.Left) <= snap) x = work.Left;
        if (Math.Abs(y - work.Top) <= snap) y = work.Top;
        if (Math.Abs(x + width - work.Right) <= snap) x = work.Right - width;
        if (Math.Abs(y + height - work.Bottom) <= snap) y = work.Bottom - height;
        _positioning = true;
        try
        {
            Win32.SetWindowPos(_window.Handle, -1, Math.Clamp(x, work.Left, work.Right - width), Math.Clamp(y, work.Top, work.Bottom - height), width, height, 0x0010);
            Resize();
        }
        finally { _positioning = false; }
    }

    private async Task SavePositionAsync()
    {
        if (_window == null || !Win32.GetWindowRect(_window.Handle, out var bounds)) return;
        try
        {
            await _changes.WaitAsync(_lifetime.Token);
            try
            {
                var prefix = ReadNumber("SelectedStyleIndex", 0) == 0 ? "Panel" : "Bar";
                await SavePatchAsync(new Dictionary<string, object?> { [prefix + "PositionX"] = bounds.Left, [prefix + "PositionY"] = bounds.Top });
            }
            finally { _changes.Release(); }
        }
        catch (Exception error) { if (!_disposed) log("OSD position: " + error); }
    }

    private async Task SavePatchAsync(Dictionary<string, object?> patch)
    {
        // settings.set replaces the scope, so preserve properties written by
        // the settings page since the last overlay update.
        var latest = await host.InvokeAsync("settings.get", JsonSerializer.SerializeToElement(new { scope = "osd" }), _lifetime.Token);
        var values = latest.GetProperty("value").EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        foreach (var (key, value) in patch) values[key] = value;
        _settings = JsonSerializer.SerializeToElement(values);
        await host.InvokeAsync("settings.set", JsonSerializer.SerializeToElement(new { scope = "osd", value = _settings }), _lifetime.Token);
        await host.InvokeAsync("settings.save", JsonSerializer.SerializeToElement(new { scopes = new[] { "osd" } }), _lifetime.Token);
    }

    private bool ReadBoolean(string name) => _settings.ValueKind == JsonValueKind.Object && _settings.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private double ReadNumber(string name, double fallback) => _settings.ValueKind == JsonValueKind.Object && _settings.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : fallback;
    private void Resize()
    {
        if (_window == null || _controller == null) return;
        Win32.GetClientRect(_window.Handle, out var bounds);
        _controller.Bounds = Rectangle.FromLTRB(0, 0, bounds.Right, bounds.Bottom);
    }

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _controller?.Close();
        _window?.Dispose();
    }
}
