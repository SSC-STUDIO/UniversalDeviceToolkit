using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Hosts the existing installer pages using the bundled native shell.</summary>
internal sealed class SetupApp(NativeWindow window, string profile, bool preview) : IDisposable
{
    private readonly InstallPayload _payload = new(AppContext.BaseDirectory);
    private CoreWebView2Controller? _controller;
    private bool _installing;
    private string? _installedExecutable;

    internal static int Run(string[] arguments)
    {
        var profile = Path.Combine(Path.GetTempPath(), $"udt-setup-{Guid.NewGuid():N}");
        var preview = arguments.Contains("--preview");
        try
        {
            // Older Electron updaters launch the outer installer with CreateProcess.
            // Keep that EXE asInvoker and elevate here while its extraction remains alive.
            if (!preview && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                var start = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("The installer executable is unavailable."))
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
                using var elevated = Process.Start(start) ?? throw new IOException("Unable to elevate the installer.");
                elevated.WaitForExit();
                return elevated.ExitCode;
            }
            Directory.CreateDirectory(profile);
            Marshal.ThrowExceptionForHR(Win32.OleInitialize(0));
            try
            {
                using var window = new NativeWindow(Path.Combine(profile, "window.json"), error => Console.Error.WriteLine(error));
                SynchronizationContext.SetSynchronizationContext(window);
                using var setup = new SetupApp(window, profile, preview);
                var exitCode = arguments.Contains("--diagnose-setup") ? 1 : 0;
                window.Post(async _ =>
                {
                    try
                    {
                        if (arguments.Contains("--silent"))
                        {
                            var index = Array.IndexOf(arguments, "--destination");
                            var destination = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : DefaultPath;
                            var previous = ShellConfiguration.ReadInstallerSelection(Path.Combine(destination, "installer-selection.ini"));
                            await setup.InstallAsync(JsonSerializer.SerializeToElement(new
                            {
                                destination,
                                language = previous?.GetProperty("language").GetString() ?? "en",
                                deviceMode = previous?.GetProperty("deviceMode").GetString() ?? "auto",
                                features = previous?.GetProperty("features")
                            }));
                            Win32.PostQuitMessage(0);
                        }
                        else
                        {
                            await setup.StartAsync();
                            if (arguments.Contains("--diagnose-setup"))
                            {
                                await setup.VerifyAsync();
                                exitCode = 0;
                                Console.WriteLine("Installer check passed: shared welcome, language, device and feature pages with native bridge.");
                                Win32.PostQuitMessage(0);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        exitCode = 1;
                        Console.Error.WriteLine(error);
                        if (!arguments.Contains("--silent") && !arguments.Contains("--diagnose-setup"))
                            Win32.MessageBox(window.Handle, error.Message, "Universal Device Toolkit Setup", 0x10);
                        Win32.PostQuitMessage(1);
                    }
                }, null);
                NativeWindow.Run();
                return exitCode;
            }
            finally { SynchronizationContext.SetSynchronizationContext(null); Win32.OleUninitialize(); }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(profile)) Directory.Delete(profile, true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Console.Error.WriteLine(error.Message); }
        }
    }

    private static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Universal Device Toolkit");

    private async Task StartAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(profile, "WebView2"));
        _controller = await environment.CreateCoreWebView2ControllerAsync(window.Handle);
        _controller.DefaultBackgroundColor = IsDark ? Color.FromArgb(23, 23, 23) : Color.FromArgb(243, 245, 248);
        _controller.IsVisible = true;
        var web = _controller.CoreWebView2;
        web.Settings.IsStatusBarEnabled = false;
        web.Settings.AreDefaultContextMenusEnabled = false;
        web.Settings.AreDevToolsEnabled = preview;
        web.Settings.IsNonClientRegionSupportEnabled = true;
        // NSIS extraction directories can deny access to WebView2's sandbox.
        // Serve only the fixed UI assets from the parent process; do not loosen
        // directory permissions or disable the browser sandbox.
        var resources = new SetupResources(Path.Combine(AppContext.BaseDirectory, "resources", "setup"));
        web.AddWebResourceRequestedFilter("https://setup.udt.local/*", CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        web.WebResourceRequested += (_, args) =>
        {
            var asset = resources.Get(args.Request.Uri);
            args.Response = environment.CreateWebResourceResponse(asset.Content, asset.Status,
                asset.Status == 200 ? "OK" : "Not Found",
                $"Content-Type: {asset.ContentType}\r\nX-Content-Type-Options: nosniff\r\nCache-Control: no-cache");
        };
        web.NavigationStarting += (_, args) => args.Cancel = !IsSetupAddress(args.Uri);
        web.NewWindowRequested += (_, args) => args.Handled = true;
        web.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
        web.WebMessageReceived += OnMessage;
        using var script = Assembly.GetExecutingAssembly().GetManifestResourceStream("UniversalDeviceToolkit.Windows.Installation.SetupBridge.js")
            ?? throw new InvalidDataException("The installer bridge is missing.");
        using var reader = new StreamReader(script);
        await web.AddScriptToExecuteOnDocumentCreatedAsync(await reader.ReadToEndAsync());
        window.Resized += Resize;
        window.Closing += CloseWindow;
        window.MessageReceived += OnWindowMessage;
        Resize();
        web.Navigate("https://setup.udt.local/index.html");
        window.Show();
    }

    private static bool IsSetupAddress(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "setup.udt.local" && uri.IsDefaultPort;

    private async Task VerifyAsync()
    {
        if (!preview || _controller == null) throw new InvalidOperationException("Installer diagnostics require preview mode.");
        var web = _controller.CoreWebView2;
        async Task WaitForAsync(string expression)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (await web.ExecuteScriptAsync(expression) == "true") return;
                await Task.Delay(100);
            }
            throw new TimeoutException("The installer page did not render: " + expression);
        }
        await WaitForAsync("Boolean(document.querySelector('#destination')?.value && document.querySelector('.brand-logo'))");
        foreach (var selector in new[] { "[data-language]", "[data-device]", "[data-feature]" })
        {
            await web.ExecuteScriptAsync("document.querySelector('[data-action=next]').click()");
            await WaitForAsync($"Boolean(document.querySelector('{selector}'))");
        }
        await web.ExecuteScriptAsync("document.querySelector('[data-feature=windowsOptimization]').click()");
        await WaitForAsync("document.querySelector('[data-feature=networkAcceleration]').getAttribute('aria-disabled') === 'true'");
    }

    private void Resize()
    {
        if (_controller == null || !Win32.GetClientRect(window.Handle, out var bounds)) return;
        _controller.Bounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        _controller.NotifyParentWindowPositionChanged();
    }

    private void OnWindowMessage(uint message, nuint word, nint data)
    {
        if (message == 0x001A) SendEvent("theme", Theme());
        if (message == 0x0003) _controller?.NotifyParentWindowPositionChanged();
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!IsSetupAddress(args.Source)) return;
        long? id = null;
        try
        {
            using var request = JsonDocument.Parse(args.WebMessageAsJson);
            id = request.RootElement.GetProperty("id").GetInt64();
            var parameters = request.RootElement.TryGetProperty("parameters", out var value) ? value : default;
            object? result = request.RootElement.GetProperty("method").GetString() switch
            {
                "info" => Info(),
                "theme" => Theme(),
                "chooseDirectory" => ChooseDirectory(),
                "install" => await InstallAsync(parameters),
                "launch" => Launch(parameters.GetString()),
                "minimize" => Win32.ShowWindow(window.Handle, 6),
                "close" => Close(),
                _ => throw new ArgumentException("Unknown installer operation.")
            };
            _controller?.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, result }));
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            _controller?.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, error = error.Message }));
        }
    }

    private object Info()
    {
        var logo = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "resources", "setup", "icon.png"));
        return new
        {
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3), defaultPath = DefaultPath,
            payloadBytes = _payload.Size, availableBytes = new DriveInfo(Path.GetPathRoot(DefaultPath) ?? "C:\\").AvailableFreeSpace,
            architecture = "Windows x64", isUninstaller = false, isPreview = preview, isOnline = false,
            platform = "win32", logoData = "data:image/png;base64," + Convert.ToBase64String(logo), theme = Theme()
        };
    }

    private static bool IsDark => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
    private static object Theme() => new { mode = IsDark ? "dark" : "light", accent = "#ff2a38" };

    private async Task<object> InstallAsync(JsonElement parameters)
    {
        if (preview) throw new InvalidOperationException("Preview mode does not install files.");
        if (_installing) throw new InvalidOperationException("Installation is already in progress.");
        var options = InstallOptions.Parse(parameters);
        _installing = true;
        try
        {
            var progress = new Progress<object>(payload => SendEvent("progress", payload));
            var executable = await Task.Run(() => _payload.CopyAsync(options, progress));
            var command = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "resources", "setup", "register.exe"))
            {
                // NSIS requires /D last and unquoted, even when it contains spaces.
                UseShellExecute = false, CreateNoWindow = true, Arguments = "/S /D=" + options.Destination
            };
            using var registration = Process.Start(command) ?? throw new IOException("Unable to register the installation.");
            await registration.WaitForExitAsync();
            if (registration.ExitCode != 0) throw new IOException($"Installation registration failed ({registration.ExitCode}).");
            _installedExecutable = executable;
            SendEvent("progress", new { phase = "complete", percent = 100, file = "" });
            return new { destination = options.Destination, executable };
        }
        finally { _installing = false; }
    }

    private object Launch(string? executable)
    {
        if (_installedExecutable == null || !string.Equals(executable, _installedExecutable, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only the installed application can be launched.");
        Process.Start(new ProcessStartInfo(_installedExecutable) { UseShellExecute = true });
        return new { launched = true };
    }

    private bool Close()
    {
        if (!_installing) Win32.PostQuitMessage(0);
        return !_installing;
    }

    private void SendEvent(string name, object payload) => _controller?.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { @event = name, payload }));

    private string? ChooseDirectory()
    {
        var info = new Win32.BrowseInfo { Owner = window.Handle, Title = "Universal Device Toolkit", Flags = 0x0051 };
        var item = Win32.SHBrowseForFolder(ref info);
        if (item == 0) return null;
        try
        {
            var path = new StringBuilder(32768);
            return Win32.SHGetPathFromIDList(item, path, (uint)path.Capacity, 0) ? path.ToString() : null;
        }
        finally { Marshal.FreeCoTaskMem(item); }
    }

    public void Dispose()
    {
        window.Resized -= Resize;
        window.Closing -= CloseWindow;
        window.MessageReceived -= OnWindowMessage;
        _controller?.Close();
    }

    private void CloseWindow() => Close();
}
