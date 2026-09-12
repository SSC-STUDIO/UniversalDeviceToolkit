using System.Diagnostics;
using System.Drawing;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Windows;

internal sealed class DesktopApp : IDisposable
{
    private const string AppOrigin = "https://udt.local";
    private readonly NativeWindow _window;
    private readonly ShellConfiguration _configuration;
    private readonly HostConnection _host;
    private readonly Action<string> _log;
    private NativeTray? _tray;
    private CoreWebView2Environment? _environment;
    private CoreWebView2Controller? _controller;
    private bool _quitting;
    private bool _minimizeToTray = true;
    private double _scale = 1;
    private UpdateReleaseInfo? _latestUpdate;
    private string? _verifiedInstallerPath;
    private static readonly HttpClient UpdateClient = CreateUpdateClient();

    public DesktopApp(NativeWindow window, ShellConfiguration configuration, Action<string> log)
    {
        _window = window;
        _configuration = configuration;
        _log = log;
        _host = new HostConnection(configuration.HostPath, configuration.HostArguments, log);
        _host.EventReceived += (name, data) => _window.Post(_ => SendEvent(name, data), null);
        _window.Resized += Resize;
        _window.Closing += HandleCloseRequest;
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
        _tray = new NativeTray(_window, _configuration.InstallerSelection?.GetProperty("language").GetString() ?? "en", RestoreFromTray, Quit, _log);
        _ = RefreshWindowBehaviorAsync();
        _ = NotifyUiActivityAsync(true);
        webView.Navigate(AppOrigin + "/index.html");
        _window.Show();
    }

    private async Task NotifyUiActivityAsync(bool active)
    {
        try { await _host.InvokeAsync("app.setUiActive", new { active, pid = Environment.ProcessId }); }
        catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException) { _log(error.Message); }
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
                var domainParameters = parameters.TryGetProperty("params", out var value) ? value : (JsonElement?)null;
                return await InvokeDomainAsync(domainMethod, domainParameters);
            case "host:get-status": return _host.Status;
            case "window:minimize": Win32.ShowWindow(_window.Handle, 6); return null;
            case "window:maximize-toggle": Win32.ShowWindow(_window.Handle, Win32.IsZoomed(_window.Handle) ? 9 : 3); return null;
            case "window:is-maximized": return Win32.IsZoomed(_window.Handle);
            case "window:close": HandleCloseRequest(); return null;
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
                var dark = theme == CoreWebView2PreferredColorScheme.Dark ||
                    (theme == CoreWebView2PreferredColorScheme.Auto && UsesDarkSystemTheme()) ? 1 : 0;
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
            case "dialog:select-exe-file": return SelectFile("Open", "Executable files\0*.exe\0All files\0*.*\0\0");
            case "dialog:select-audio-file": return SelectFile("Import", "Audio files\0*.wav;*.mp3;*.ogg;*.flac;*.aac;*.m4a;*.wma\0All files\0*.*\0\0");
            case "dialog:select-json-file": return SelectFile("Import keyboard backlight profile", "JSON files\0*.json\0All files\0*.*\0\0");
            case "dialog:open-file": return SelectFile("Open", "All files\0*.*\0\0");
            case "dialog:save-file": return SaveFile("Save", "All files\0*.*\0\0");
            case "dialog:open-path": OpenPath(parameters.GetProperty("path").GetString() ?? ""); return new { ok = true };
            case "dialog:open-url": OpenExternal(parameters.GetProperty("url").GetString() ?? ""); return new { ok = true };
            case "clipboard:write-lines": WriteClipboard(parameters); return new { ok = true };
            case "tray:set-language":
                _tray?.SetLanguage(parameters.GetString() ?? "en");
                return null;
            case "tray:refresh": return null;
            case "app:set-autorun": return await SetAutorunAsync(parameters);
            case "app:get-autorun": return await GetAutorunAsync();
            case "app:memory-usage": return MemoryUsage();
            default: throw new InvalidOperationException($"Native shell method is not implemented: {method}");
        }
    }

    private async Task<JsonElement> InvokeDomainAsync(string method, JsonElement? parameters)
    {
        switch (method)
        {
            case "device.info":
                try { return DeviceInfoProjection.From(await _host.InvokeAsync("system.info")); }
                catch (Exception error) when (error is IOException or TimeoutException)
                {
                    _log($"Unable to read device information: {error.Message}");
                    return DeviceInfoProjection.From(default);
                }
            case "app.update.check":
            case "app.update.status":
            case "app.setUiActive":
                return await _host.InvokeAsync(method, parameters);
            case "update.getRelease":
                return JsonSerializer.SerializeToElement(new { release = await GetLatestUpdateAsync() });
            case "update.download":
                return await DownloadUpdateAsync();
            case "update.launchInstaller":
                return await LaunchVerifiedInstallerAsync(parameters ?? JsonSerializer.SerializeToElement<object?>(null));
            case "powerPlans.getList":
                return await ListPowerPlansAsync();
            case "powerPlans.setActive":
                return await SetPowerPlanAsync(parameters);
            case "power.restart":
                return await RunPowerActionAsync("/r /t 0");
            case "power.shutdown":
                return await RunPowerActionAsync("/s /t 0");
            case "power.sleep":
                return await RunPowerActionAsync("/h");
            default:
                return await _host.InvokeAsync(method, parameters);
        }
    }

    private async Task<JsonElement> SetAutorunAsync(JsonElement parameters)
    {
        var enabled = parameters.ValueKind == JsonValueKind.True;
        var state = enabled ? "Enabled" : "Disabled";
        var result = await _host.InvokeAsync("app.setAutorun", JsonSerializer.SerializeToElement(new { state }));
        var applied = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("state", out var value)
            && value.GetString() is { } text && !text.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
        return JsonSerializer.SerializeToElement(new { ok = true, enabled = applied });
    }

    private sealed record UpdateReleaseInfo(string Version, string Url, string AssetUrl, string AssetName, long AssetSize, string? ReleaseNotes, string? ReleaseDate, string? Sha256Url);

    private static HttpClient CreateUpdateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("UniversalDeviceToolkit-WebView2", "6.1.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private async Task<UpdateReleaseInfo?> GetLatestUpdateAsync()
    {
        if (_latestUpdate is { } cached) return cached;
        using var response = await UpdateClient.GetAsync("https://api.github.com/repos/SSC-STUDIO/UniversalDeviceToolkit/releases?per_page=10");
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            if (release.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean()) continue;
            var tag = release.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag) || tag.Equals("plugin-catalog", StringComparison.OrdinalIgnoreCase) || tag.Equals("plugin-catalog-preview", StringComparison.OrdinalIgnoreCase)) continue;
            var assets = release.TryGetProperty("assets", out var assetList) ? assetList.EnumerateArray() : [];
            JsonElement installer = default;
            JsonElement hash = default;
            foreach (var asset in assets)
            {
                var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                if (name == null) continue;
                if (Regex.IsMatch(name, @"UniversalDeviceToolkit.*(?:Full_Setup|Setup-).+\.exe$", RegexOptions.IgnoreCase)) installer = asset;
                else if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)) hash = asset;
            }
            if (installer.ValueKind != JsonValueKind.Object) continue;
            var installerName = installer.GetProperty("name").GetString();
            var installerUrl = installer.GetProperty("browser_download_url").GetString();
            if (installerName == null || installerUrl == null) continue;
            var releaseUrl = release.TryGetProperty("html_url", out var html) ? html.GetString() : null;
            var shaUrl = hash.ValueKind == JsonValueKind.Object && hash.TryGetProperty("browser_download_url", out var sha) ? sha.GetString() : null;
            _latestUpdate = new UpdateReleaseInfo(tag, releaseUrl ?? $"https://github.com/SSC-STUDIO/UniversalDeviceToolkit/releases/tag/{tag}", installerUrl, installerName,
                installer.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                release.TryGetProperty("body", out var body) ? body.GetString() : null,
                release.TryGetProperty("published_at", out var published) ? published.GetString() : null, shaUrl);
            return _latestUpdate;
        }
        return null;
    }

    private async Task<JsonElement> DownloadUpdateAsync()
    {
        _verifiedInstallerPath = null;
        var release = await GetLatestUpdateAsync();
        if (release == null) return JsonSerializer.SerializeToElement(new { ok = false, error = "No compatible installer asset found in the latest release" });
        var destinationDirectory = Path.Combine(_configuration.DataDirectory, "updates");
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, release.AssetName);
        var expectedHash = await ReadExpectedHashAsync(release);
        if (expectedHash == null) return JsonSerializer.SerializeToElement(new { ok = false, error = "The update release has no SHA256 manifest" });
        var partial = destination + ".partial";
        try
        {
            using var response = await UpdateClient.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.AssetSize;
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(partial);
            var buffer = new byte[128 * 1024];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                received += read;
                var percent = total > 0 ? received * 100d / total : 0;
                _window.Post(_ => SendEvent("update.download-progress", JsonSerializer.SerializeToElement(new { percent, receivedBytes = received, totalBytes = total, done = false })), null);
            }
            await output.FlushAsync();
            string actual;
            await using (var hashInput = File.OpenRead(partial))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(hashInput)).ToLowerInvariant();
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update package integrity check failed.");
            File.Move(partial, destination, true);
            _verifiedInstallerPath = destination;
            _window.Post(_ => SendEvent("update.download-progress", JsonSerializer.SerializeToElement(new { percent = 100d, receivedBytes = received, totalBytes = total, done = true })), null);
            return JsonSerializer.SerializeToElement(new { ok = true, path = destination });
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
        {
            try { File.Delete(partial); } catch (IOException) { }
            return JsonSerializer.SerializeToElement(new { ok = false, error = error.Message });
        }
    }

    private async Task<string?> ReadExpectedHashAsync(UpdateReleaseInfo release)
    {
        if (release.Sha256Url == null) return null;
        var text = await UpdateClient.GetStringAsync(release.Sha256Url);
        var match = Regex.Match(text, @"(?<![a-fA-F0-9])([a-fA-F0-9]{64})(?![a-fA-F0-9])");
        return match.Success ? match.Groups[1].Value : null;
    }

    private Task<JsonElement> LaunchVerifiedInstallerAsync(JsonElement parameters)
    {
        var requested = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("path", out var path) ? path.GetString() : null;
        if (_verifiedInstallerPath == null || requested == null || !Path.GetFullPath(requested).Equals(Path.GetFullPath(_verifiedInstallerPath), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(JsonSerializer.SerializeToElement(new { ok = false, error = "Installer path is not the verified download." }));
        try
        {
            Process.Start(new ProcessStartInfo(_verifiedInstallerPath, "/S") { UseShellExecute = true, Verb = "runas" });
            _window.Post(_ => Quit(), null);
            return Task.FromResult(JsonSerializer.SerializeToElement(new { ok = true }));
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Task.FromResult(JsonSerializer.SerializeToElement(new { ok = false, error = error.Message }));
        }
    }

    private async Task<JsonElement> GetAutorunAsync()
    {
        var result = await _host.InvokeAsync("app.getAutorun");
        var enabled = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("state", out var value)
            && value.GetString() is { } text && !text.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
        return JsonSerializer.SerializeToElement(new { enabled });
    }

    private static async Task<JsonElement> ListPowerPlansAsync()
    {
        var result = await RunProcessAsync("powercfg.exe", "/list");
        if (result.ExitCode != 0) return JsonSerializer.SerializeToElement(new { plans = Array.Empty<object>() });
        var plans = new List<object>();
        foreach (Match match in Regex.Matches(result.Output, @"GUID:\s*([0-9a-fA-F-]{36})\s*\(([^)]*)\)\s*(\*)?"))
            plans.Add(new { guid = match.Groups[1].Value.ToUpperInvariant(), name = match.Groups[2].Value.Trim(), isActive = match.Groups[3].Success });
        return JsonSerializer.SerializeToElement(new { plans });
    }

    private static async Task<JsonElement> SetPowerPlanAsync(JsonElement? parameters)
    {
        var guid = parameters?.ValueKind == JsonValueKind.Object && parameters.Value.TryGetProperty("guid", out var value)
            ? value.GetString() : null;
        if (guid == null || !Regex.IsMatch(guid, "^[0-9a-fA-F-]{36}$")) throw new ArgumentException("A power plan GUID is required.");
        var result = await RunProcessAsync("powercfg.exe", $"/setactive {guid}");
        if (result.ExitCode != 0) throw new IOException(result.Error.Length == 0 ? "Unable to activate the power plan." : result.Error);
        return JsonSerializer.SerializeToElement(new { ok = true });
    }

    private static async Task<JsonElement> RunPowerActionAsync(string action)
    {
        var result = await RunProcessAsync("shutdown.exe", action);
        return JsonSerializer.SerializeToElement(new { ok = result.ExitCode == 0, error = result.ExitCode == 0 ? null : result.Error });
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        if (!process.Start()) throw new IOException($"Unable to start {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
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

    private static bool UsesDarkSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (SecurityException) { return false; }
        catch (IOException) { return false; }
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

    private string? SelectFile(string title, string filter)
    {
        var file = new Win32.OpenFileName
        {
            Size = Marshal.SizeOf<Win32.OpenFileName>(),
            Owner = _window.Handle,
            Filter = filter,
            FilterIndex = 1,
            File = new StringBuilder(32768),
            MaxFile = 32768,
            Title = title,
            Flags = 0x00001000 | 0x00000800
        };
        return Win32.GetOpenFileName(ref file) ? file.File.ToString() : null;
    }

    private string? SaveFile(string title, string filter)
    {
        var file = new Win32.OpenFileName
        {
            Size = Marshal.SizeOf<Win32.OpenFileName>(),
            Owner = _window.Handle,
            Filter = filter,
            FilterIndex = 1,
            File = new StringBuilder(32768),
            MaxFile = 32768,
            Title = title,
            Flags = 0x00000002 | 0x00000800
        };
        return Win32.GetSaveFileName(ref file) ? file.File.ToString() : null;
    }

    private static void WriteClipboard(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("lines", out var lines)
            || lines.ValueKind != JsonValueKind.Array || lines.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            throw new ArgumentException("A lines array of strings is required.");
        var text = string.Join(Environment.NewLine, lines.EnumerateArray().Select(item => item.GetString() ?? string.Empty));
        var bytes = checked((text.Length + 1) * 2);
        var memory = Win32.GlobalAlloc(0x0042, (nuint)bytes);
        if (memory == 0) throw new OutOfMemoryException("Unable to allocate clipboard memory.");
        try
        {
            var target = Win32.GlobalLock(memory);
            if (target == 0) throw new InvalidOperationException("Unable to lock clipboard memory.");
            try { Marshal.Copy(text.ToCharArray(), 0, target, text.Length); Marshal.WriteInt16(target, text.Length * 2, 0); }
            finally { Win32.GlobalUnlock(memory); }
            if (!Win32.OpenClipboard(0)) throw new IOException("Unable to open the clipboard.");
            try
            {
                if (!Win32.EmptyClipboard() || Win32.SetClipboardData(13, memory) == 0) throw new IOException("Unable to write the clipboard.");
                memory = 0;
            }
            finally { Win32.CloseClipboard(); }
        }
        finally { if (memory != 0) Win32.GlobalFree(memory); }
    }

    private void Resize()
    {
        if (_controller == null) return;
        Win32.GetClientRect(_window.Handle, out var bounds);
        _controller.Bounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        SendEvent("window:maximized-changed", JsonSerializer.SerializeToElement(Win32.IsZoomed(_window.Handle)));
    }

    private async Task RefreshWindowBehaviorAsync()
    {
        try
        {
            var result = await _host.InvokeAsync("settings.get", new { scope = "application" });
            if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.Object && value.TryGetProperty("MinimizeToTray", out var setting)
                && setting.ValueKind is JsonValueKind.False or JsonValueKind.True)
                _minimizeToTray = setting.GetBoolean();
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException)
        {
            _log($"Unable to read tray behavior setting: {error.Message}");
        }
    }

    private void HandleCloseRequest()
    {
        if (_quitting) return;
        if (_minimizeToTray)
        {
            _window.Hide();
            _ = NotifyUiActivityAsync(false);
            return;
        }
        Quit();
    }

    private void RestoreFromTray()
    {
        if (_quitting) return;
        _window.Show();
        _ = NotifyUiActivityAsync(true);
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
        _ = NotifyUiActivityAsync(false);
        _tray?.Dispose();
        _tray = null;
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
