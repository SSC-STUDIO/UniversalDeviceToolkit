using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        using var instance = AcquireInstance(arguments);
        if (instance == null) return 0;
        var diagnoseUi = arguments.Contains("--diagnose-ui");
        var configuration = ShellConfiguration.Load(arguments);
        if (diagnoseUi)
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
                Win32.MessageBox(0, error is WebView2RuntimeNotFoundException
                    ? "Microsoft Edge WebView2 Runtime is not installed. Use the offline compatibility installer, which includes its browser engine."
                    : error.Message, "Universal Device Toolkit", 0x10);
            return 1;
        }
    }

    private static Mutex? AcquireInstance(string[] arguments)
    {
        // Diagnostics must remain scriptable while the desktop app is running.
        if (arguments.Contains("--diagnose", StringComparer.OrdinalIgnoreCase) || arguments.Contains("--diagnose-ui", StringComparer.OrdinalIgnoreCase)) return new Mutex();
        var mutex = new Mutex(true, "Global\\UniversalDeviceToolkit.Windows.Singleton", out var created);
        if (created) return mutex;
        mutex.Dispose();
        return null;
    }

    private static async Task DiagnoseAsync(ShellConfiguration configuration, string browserVersion, Action<string> log)
    {
        await using var host = new HostConnection(configuration.HostPath, ["--no-hardware", "--safe-start", "--disable-update-checker"], log);
        host.Start();
        var capabilities = await host.InvokeAsync("host.getCapabilities");
        Console.WriteLine(JsonSerializer.Serialize(new { browserVersion, host = host.Status, capabilities }));
    }
}
