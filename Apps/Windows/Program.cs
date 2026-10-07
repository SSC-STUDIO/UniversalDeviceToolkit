using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Contains("--install-payload")) return InstallCommand.Run(arguments);
        if (arguments.Contains("--setup")) return SetupApp.Run(arguments);
        var restartIndex = Array.IndexOf(arguments, "--restart-after");
        if (restartIndex >= 0 && restartIndex + 1 < arguments.Length && int.TryParse(arguments[restartIndex + 1], out var previousPid))
        {
            try
            {
                using var previous = Process.GetProcessById(previousPid);
                if (!previous.WaitForExit(15000)) return 1;
            }
            catch (ArgumentException) { Console.Error.WriteLine("Previous session already exited."); }
        }
        var diagnostic = arguments.Contains("--diagnose") || arguments.Contains("--diagnose-ui");
        if (!diagnostic && !IsAdministrator())
        {
            if (!arguments.Contains("--elevation-checked") && TryRelaunchElevated(arguments))
                return 0;
            var chinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            Win32.MessageBox(0, chinese
                ? "没有管理员权限。处理器温度和风扇无法读取。\n请在用户账户控制中选择“是”，或右键以管理员身份重新运行。"
                : "Administrator permission was not granted. CPU temperature and fan speeds cannot be read.\nApprove the permission prompt, or start the app as administrator.",
                "Universal Device Toolkit", 0x30);
        }
        // Diagnostics cannot activate or replace a running user session.
        using var instance = new SingleInstance(diagnostic
            ? $"Local\\UniversalDeviceToolkit.Diagnostic.{Guid.NewGuid():N}"
            : "Global\\UniversalDeviceToolkit.Windows.Singleton");
        if (!instance.IsPrimary) return 0;
        var diagnoseUi = arguments.Contains("--diagnose-ui");
        var configuration = ShellConfiguration.Load(arguments);
        if (diagnostic)
            configuration = configuration with
            {
                DataDirectory = Path.Combine(Path.GetTempPath(), $"udt-ui-diagnostic-{Guid.NewGuid():N}"),
                HostArguments = ["--no-hardware", "--safe-start", "--disable-update-checker"]
            };
        // Closing a diagnostic window early must not count as a passed check.
        var exitCode = diagnoseUi ? 1 : 0;
        var logs = Path.Combine(configuration.DataDirectory, "log");
        Directory.CreateDirectory(logs);
        var logGate = new object();
        void Log(string text)
        {
            lock (logGate)
            {
                try { File.AppendAllText(Path.Combine(logs, "windows-shell.log"), $"{DateTimeOffset.Now:O} {text}{Environment.NewLine}"); }
                catch (IOException error) { Console.Error.WriteLine(error.Message); }
            }
        }
        try
        {
            var browserVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (arguments.Contains("--diagnose"))
            {
                DiagnoseAsync(configuration, browserVersion, Log).GetAwaiter().GetResult();
                return 0;
            }
            var oleResult = Win32.OleInitialize(0);
            if (oleResult < 0) System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(oleResult);
            try
            {
                using var window = new NativeWindow(Path.Combine(configuration.DataDirectory, "window-state.json"), error => Log(error.ToString()));
                SynchronizationContext.SetSynchronizationContext(window);
                using var app = new DesktopApp(window, configuration, Log);
                instance.Listen(() => window.Post(_ => app.RestoreFromTray(), null));
                window.Post(async _ =>
                {
                    try
                    {
                        await app.StartAsync();
                        if (diagnoseUi)
                        {
                            await app.VerifyUiAsync();
                            exitCode = 0;
                            Console.WriteLine("Lightweight UI smoke check passed: visible renderer, Host bridge, minimize and tray restore.");
                            app.Quit();
                        }
                    }
                    catch (Exception error)
                    {
                        exitCode = 1;
                        Log(error.ToString());
                        if (diagnoseUi) Console.Error.WriteLine(error.Message);
                        else if (ShellRecovery.IsBrowserFailure(error)) ShellRecovery.Show(error, window.Handle);
                        else Win32.MessageBox(window.Handle, error.Message, "Universal Device Toolkit", 0x10);
                        app.Quit();
                    }
                }, null);
                NativeWindow.Run();
            }
            finally { SynchronizationContext.SetSynchronizationContext(null); Win32.OleUninitialize(); }
            return exitCode;
        }
        catch (Exception error)
        {
            Log(error.ToString());
            Console.Error.WriteLine(error.Message);
            if (!arguments.Contains("--diagnose") && !diagnoseUi)
                if (ShellRecovery.IsBrowserFailure(error)) ShellRecovery.Show(error);
                else Win32.MessageBox(0, error.Message, "Universal Device Toolkit", 0x10);
            return 1;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Visual Studio starts this shell asInvoker, so CPU package temperature and
    /// Lenovo fan WMI never become readable. Ask Windows to relaunch elevated.
    /// </summary>
    private static bool TryRelaunchElevated(string[] arguments)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
                return false;
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory
            };
            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);
            start.ArgumentList.Add("--elevation-checked");
            return Process.Start(start) is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static async Task DiagnoseAsync(ShellConfiguration configuration, string browserVersion, Action<string> log)
    {
        await using var host = new HostConnection(configuration.HostPath, ["--no-hardware", "--safe-start", "--disable-update-checker"], configuration.DataDirectory, log, diagnostic: true);
        host.Start();
        var capabilities = await host.InvokeAsync("host.getCapabilities");
        Console.WriteLine(JsonSerializer.Serialize(new { shellVariant = "webview2", version = ShellRecovery.Version, browserVersion, host = host.Status, capabilities }));
    }
}
