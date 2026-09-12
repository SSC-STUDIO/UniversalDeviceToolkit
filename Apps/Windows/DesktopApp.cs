using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

internal sealed class DesktopApp : IDisposable
{
    private const string AppOrigin = "https://udt.local";
    private readonly NativeWindow _window;
    private readonly ShellConfiguration _configuration;
    private readonly HostConnection _host;
    private readonly Action<string> _log;
    private CoreWebView2Environment? _environment;
    private CoreWebView2Controller? _controller;
    private bool _quitting;
    private double _scale = 1;

    public DesktopApp(NativeWindow window, ShellConfiguration configuration, Action<string> log)
    {
        _window = window;
        _configuration = configuration;
        _log = log;
        _host = new HostConnection(configuration.HostPath, configuration.HostArguments, log);
        _host.EventReceived += (name, data) => _window.Post(_ => SendEvent(name, data), null);
        _window.Resized += Resize;
        _window.Closing += Quit;
    }

    public async Task StartAsync()
    {
        if (!File.Exists(_configuration.HostPath)) throw new FileNotFoundException("The bundled Host was not found.", _configuration.HostPath);
        if (!File.Exists(Path.Combine(_configuration.UiDirectory, "index.html"))) throw new DirectoryNotFoundException("The bundled interface was not found.");
        _environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(_configuration.DataDirectory, "WebView2"));
        _controller = await _environment.CreateCoreWebView2ControllerAsync(_window.Handle);
        _controller.DefaultBackgroundColor = Color.FromArgb(0);
        _controller.ZoomFactor = _scale * 5 / 6;
        var webView = _controller.CoreWebView2;
        webView.Settings.IsStatusBarEnabled = false;
        webView.Settings.AreDefaultContextMenusEnabled = false;
        webView.Settings.IsNonClientRegionSupportEnabled = true;
#if !DEBUG
        webView.Settings.AreDevToolsEnabled = false;
#endif
        webView.SetVirtualHostNameToFolderMapping("udt.local", _configuration.UiDirectory, CoreWebView2HostResourceAccessKind.Deny);
        webView.NavigationStarting += (_, args) => { if (!IsAppAddress(args.Uri)) args.Cancel = true; };
        webView.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            try { OpenExternal(args.Uri); }
            catch (Exception error) { _log(error.ToString()); }
        };
        webView.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
        webView.WebMessageReceived += OnMessageReceived;
        webView.NavigationCompleted += (_, args) =>
        {
            if (!args.IsSuccess) { _log($"Interface navigation failed: {args.WebErrorStatus}"); return; }
            if (_host.ReadyPayload is { } payload) SendEvent("host.ready", payload);
        };
        using var script = Assembly.GetExecutingAssembly().GetManifestResourceStream("UniversalDeviceToolkit.Windows.Bridge.js")
            ?? throw new InvalidOperationException("The shell bridge resource is missing.");
        using var reader = new StreamReader(script);
        var startup = JsonSerializer.Serialize(new { installerSelection = _configuration.InstallerSelection });
        await webView.AddScriptToExecuteOnDocumentCreatedAsync((await reader.ReadToEndAsync()).Replace("__UDT_STARTUP_JSON__", startup, StringComparison.Ordinal));
        Resize();
        _host.Start();
        webView.Navigate(AppOrigin + "/index.html");
        _window.Show();
    }

    private static bool IsAppAddress(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "udt.local" && uri.IsDefaultPort;

    private async void OnMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        long? id = null;
        try
        {
            if (!IsAppAddress(args.Source)) throw new InvalidOperationException("Only the application page may call the native bridge.");
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var request = document.RootElement;
            id = request.GetProperty("id").GetInt64();
            var method = request.GetProperty("method").GetString() ?? throw new ArgumentException("A method is required.");
            var parameters = request.TryGetProperty("params", out var value) ? value.Clone() : JsonSerializer.SerializeToElement<object?>(null);
            var result = await InvokeAsync(method, parameters);
            _controller?.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, result }));
        }
        catch (Exception error)
        {
            _log(error.ToString());
            if (id.HasValue && _controller != null)
                _controller.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, error = new { message = error.Message } }));
        }
    }

    private async Task<object?> InvokeAsync(string method, JsonElement parameters)
    {
        switch (method)
        {
            case "bridge:invoke":
                var domainMethod = parameters.GetProperty("method").GetString() ?? throw new ArgumentException("A Host method is required.");
                return await _host.InvokeAsync(domainMethod, parameters.TryGetProperty("params", out var value) ? value : null);
            case "host:get-status": return _host.Status;
            case "window:minimize": Win32.ShowWindow(_window.Handle, 6); return null;
            case "window:maximize-toggle": Win32.ShowWindow(_window.Handle, Win32.IsZoomed(_window.Handle) ? 9 : 3); return null;
            case "window:is-maximized": return Win32.IsZoomed(_window.Handle);
            case "window:close":
            case "app:quit": Quit(); return null;
            case "window:set-ui-scale":
                var scale = parameters.GetDouble();
                if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentException("A positive finite scale is required.");
                _scale = Math.Clamp(scale, 0.75, 1.5);
                if (_controller != null) _controller.ZoomFactor = _scale * 5 / 6;
                return new { ok = true, scale = _scale };
            case "window:set-background-material":
                var material = parameters.GetString() switch { "none" => 1, "mica" => 2, "acrylic" => 3, _ => throw new ArgumentException("Unknown backdrop material.") };
                Win32.DwmSetWindowAttribute(_window.Handle, 38, ref material, sizeof(int));
                return null;
            case "window:set-theme-source":
                var theme = parameters.GetString() switch { "system" => CoreWebView2PreferredColorScheme.Auto, "light" => CoreWebView2PreferredColorScheme.Light, "dark" => CoreWebView2PreferredColorScheme.Dark, _ => throw new ArgumentException("Unknown theme source.") };
                if (_controller != null) _controller.CoreWebView2.Profile.PreferredColorScheme = theme;
                var dark = theme == CoreWebView2PreferredColorScheme.Dark ? 1 : 0;
                Win32.DwmSetWindowAttribute(_window.Handle, 20, ref dark, sizeof(int));
                return null;
            case "log:write": _log($"[{parameters.GetProperty("level").GetString()}] {parameters.GetProperty("message").GetString()}"); return null;
            case "shell:open-external": OpenExternal(parameters.GetString() ?? ""); return new { opened = true };
            case "shell:open-log-folder": OpenPath(Path.Combine(_configuration.DataDirectory, "log")); return null;
            case "shell:open-app-folder":
                var path = parameters.GetString() switch
                {
                    "data" => _configuration.DataDirectory,
                    "temp" => Path.GetTempPath(),
                    "log" => Path.Combine(_configuration.DataDirectory, "log"),
                    _ => throw new ArgumentException("Unknown application folder.")
                };
                OpenPath(path);
                return new { opened = true };
            case "shell:open-path": OpenPath(parameters.GetString() ?? ""); return new { opened = true };
            // Windows autorun is owned by Host's app.setAutorun RPC, as in Electron.
            case "app:set-autorun": return new { ok = true, enabled = parameters.ValueKind == JsonValueKind.True };
            case "app:get-autorun": return new { enabled = false };
            case "app:memory-usage": return MemoryUsage();
            default: throw new InvalidOperationException($"Native shell method is not implemented: {method}");
        }
    }

    private object MemoryUsage()
    {
        var ids = new Dictionary<int, string> { [Environment.ProcessId] = "Shell" };
        if (_host.ProcessId is { } host) ids[host] = "Host";
        foreach (var info in _environment?.GetProcessInfos() ?? []) ids[info.ProcessId] = info.Kind.ToString();
        var processes = new List<object>();
        var total = 0d;
        foreach (var (id, kind) in ids)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                var size = process.WorkingSet64 / 1048576d;
                processes.Add(new { name = process.ProcessName, type = kind, workingSetMB = Math.Round(size, 2) });
                total += size;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { _log(error.Message); }
        }
        return new { processes, totalMB = Math.Round(total, 2) };
    }

    private static void OpenExternal(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not "https" and not "http") throw new ArgumentException("Only HTTP(S) URLs can be opened.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private static void OpenPath(string path)
    {
        var target = Path.GetFullPath(path);
        if (!Directory.Exists(target))
        {
            if (!File.Exists(target)) throw new FileNotFoundException("The requested path does not exist.", target);
            string[] blocked = [".bat", ".cmd", ".com", ".cpl", ".exe", ".hta", ".js", ".jse", ".lnk", ".msi", ".msp", ".mst", ".ps1", ".psm1", ".reg", ".scr", ".url", ".vbe", ".vbs", ".wsf", ".wsh"];
            if (blocked.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Executable and script files cannot be opened from the renderer.");
        }
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private void Resize()
    {
        if (_controller == null) return;
        Win32.GetClientRect(_window.Handle, out var bounds);
        _controller.Bounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        SendEvent("window:maximized-changed", JsonSerializer.SerializeToElement(Win32.IsZoomed(_window.Handle)));
    }

    private void SendEvent(string name, JsonElement data)
    {
        if (_quitting || _controller == null) return;
        _controller.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { @event = name, data }));
    }

    public void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        _controller?.Close();
        _controller = null;
        _window.Dispose();
    }

    public void Dispose()
    {
        Quit();
        _host.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
