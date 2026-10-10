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
    private readonly OsdFpsSubscription _fpsSubscription = new((method, parameters, token) => host.InvokeAsync(method, parameters, token));
    private double? _subscribedInterval;
    private OsdSubscriptionPolicy _subscriptionPolicy = OsdSubscriptionPolicy.Compatible;
    private CancellationTokenSource? _subscriptionRefresh;
    private int _hostGeneration;
    private int _subscriptionGeneration = -1;
    private bool _disposed;
    private bool _positioning;
    private bool _moving;
    private int _positionSaves;
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
            if (name == "host.ready")
            {
                _hostGeneration++;
                _snapshot = null;
                _fps = null;
            }
            _ = RequestRefreshAsync(name == "osd.changed" && data.TryGetProperty("state", out var state) ? state.GetString() : null);
        }
    }

    internal Task ToggleAsync() => RequestRefreshAsync("Toggle");

    private Task RequestRefreshAsync(string? action)
    {
        // Cancel capability reads for a newer refresh. Already sent subscription
        // mutations must finish in order so a queued hide follows its subscribe.
        _subscriptionRefresh?.Cancel();
        return RefreshAsync(action);
    }

    private async Task RefreshAsync(string? action)
    {
        try
        {
            await _changes.WaitAsync(_lifetime.Token);
            try
            {
                if (_subscriptionGeneration != _hostGeneration)
                {
                    _subscriptionGeneration = _hostGeneration;
                    _subscribed = false;
                    _fpsSubscription.Reset();
                    _subscribedInterval = null;
                    _subscriptionPolicy = OsdSubscriptionPolicy.Compatible;
                }
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
                // The renderer must receive its layout even when a diagnostic
                // or unavailable telemetry backend cannot accept subscriptions.
                Render();
                using var subscriptions = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _subscriptionRefresh = subscriptions;
                try { await SynchronizeSubscriptionsAsync(subscriptions.Token); }
                catch (OperationCanceledException) when (subscriptions.IsCancellationRequested) { return; }
                finally { _subscriptionRefresh = null; }
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
            _moving = false;
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
                if (message == 0x0231) _moving = true;
                if (message is 0x007E or 0x02E0) Position(true);
                if (message == 0x0232 && !_positioning)
                {
                    _moving = false;
                    Position(true);
                    _ = SavePositionAsync();
                }
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
        _subscriptionRefresh?.Cancel();
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
                await SynchronizeSubscriptionsAsync(_lifetime.Token);
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

    private async Task SynchronizeSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var generation = _hostGeneration;
        try
        {
            var capabilities = await host.InvokeAsync("host.getCapabilities", cancellationToken: cancellationToken);
            if (generation != _hostGeneration) return;
            _subscriptionPolicy = OsdSubscriptionPolicy.FromCapabilities(capabilities);
        }
        catch (Exception error) when (error is IOException or TimeoutException)
        {
            // Older Hosts may not expose this RPC. Keep their compatibility
            // behavior, or the most recent explicit policy for this Host.
            log("OSD capabilities: " + error);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != _hostGeneration) return;
        if (!_subscriptionPolicy.Sensors) _snapshot = null;
        if (!_subscriptionPolicy.Fps) _fps = null;
        Render();
        var desired = _subscriptionPolicy.ForView(_visible, _settings);
        // Cancelling a client wait does not cancel the Host's mutation. Complete
        // it before a queued hide unsubscribes or a newer refresh subscribes again.
        try { await SynchronizeSensorSubscriptionAsync(desired.Sensors, _lifetime.Token); }
        catch (Exception error) when (error is IOException or TimeoutException) { log("OSD sensors: " + error); }
        cancellationToken.ThrowIfCancellationRequested();
        if (generation != _hostGeneration) return;
        try { await _fpsSubscription.SynchronizeAsync(desired.Fps, _lifetime.Token); }
        catch (Exception error) when (error is IOException or TimeoutException) { log("OSD FPS: " + error); }
    }

    private async Task SynchronizeSensorSubscriptionAsync(bool desired, CancellationToken cancellationToken)
    {
        if (desired)
        {
            var interval = Math.Clamp(ReadNumber("OsdRefreshInterval", 1), 0.5, 60);
            if (_subscribed && _subscribedInterval == interval) return;
            // A cancelled response does not prove the Host rejected the request.
            // Remember the attempt so a subsequent hide still unsubscribes it.
            _subscribed = true;
            _subscribedInterval = null;
            await host.InvokeAsync("sensors.subscribe", new { subscriberId = "osd", intervalSec = interval }, cancellationToken);
            _subscribedInterval = interval;
        }
        else if (_subscribed)
        {
            await host.InvokeAsync("sensors.unsubscribe", new { subscriberId = "osd" }, cancellationToken);
            _subscribed = false;
            _subscribedInterval = null;
        }
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
        if (_window == null || _moving) return;
        var bar = ReadNumber("SelectedStyleIndex", 0) != 0;
        Win32.GetWindowRect(_window.Handle, out var current);
        var placement = new OverlayWindowPlacement(_width, _height, ReadNumber("SnapThreshold", 20),
            ReadNumber(bar ? "BarPositionX" : "PanelPositionX"), ReadNumber(bar ? "BarPositionY" : "PanelPositionY"), bar);
        var bounds = new WindowPlacement(current.Left, current.Top, current.Right - current.Left, current.Bottom - current.Top);
        var preservePosition = moved || _positionSaves > 0;
        var (anchorX, anchorY) = placement.GetAnchor(bounds, preservePosition);
        var anchor = new Win32.Point { X = anchorX, Y = anchorY };
        var work = NativeWindow.GetMonitor(anchor).Work;
        bounds = placement.Fit(bounds, new WindowPlacement(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top),
            Win32.GetDpiForWindow(_window.Handle), preservePosition);
        _positioning = true;
        try
        {
            Win32.SetWindowPos(_window.Handle, -1, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0010);
            Resize();
        }
        finally { _positioning = false; }
    }

    private async Task SavePositionAsync()
    {
        if (_window == null || !Win32.GetWindowRect(_window.Handle, out var bounds)) return;
        var prefix = ReadNumber("SelectedStyleIndex", 0) == 0 ? "Panel" : "Bar";
        _positionSaves++;
        try
        {
            await _changes.WaitAsync(_lifetime.Token);
            try
            {
                await SavePatchAsync(new Dictionary<string, object?> { [prefix + "PositionX"] = bounds.Left, [prefix + "PositionY"] = bounds.Top });
            }
            finally { _changes.Release(); }
        }
        catch (Exception error) { if (!_disposed) log("OSD position: " + error); }
        finally { _positionSaves--; }
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
    private double ReadNumber(string name, double fallback) => ReadNumber(name) ?? fallback;
    private double? ReadNumber(string name) => _settings.ValueKind == JsonValueKind.Object && _settings.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;
    private void Resize()
    {
        if (_window == null || _controller == null) return;
        Win32.GetClientRect(_window.Handle, out var bounds);
        _controller.Bounds = Rectangle.FromLTRB(0, 0, bounds.Right, bounds.Bottom);
    }

    public void Dispose()
    {
        _disposed = true;
        _subscriptionRefresh?.Cancel();
        _lifetime.Cancel();
        _controller?.Close();
        _window?.Dispose();
    }
}
