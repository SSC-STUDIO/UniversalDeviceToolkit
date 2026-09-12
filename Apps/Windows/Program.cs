using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        var configuration = ShellConfiguration.Load(arguments);
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
                using var window = new NativeWindow(error => Log(error.ToString()));
                SynchronizationContext.SetSynchronizationContext(window);
                using var app = new DesktopApp(window, configuration, Log);
                window.Post(async _ =>
                {
                    try { await app.StartAsync(); }
                    catch (Exception error)
                    {
                        Log(error.ToString());
                        Win32.MessageBox(window.Handle, error.Message, "Universal Device Toolkit", 0x10);
                        app.Quit();
                    }
                }, null);
                NativeWindow.Run();
            }
            finally { SynchronizationContext.SetSynchronizationContext(null); Win32.OleUninitialize(); }
            return 0;
        }
        catch (Exception error)
        {
            Log(error.ToString());
            Console.Error.WriteLine(error.Message);
            if (!arguments.Contains("--diagnose"))
                Win32.MessageBox(0, error is WebView2RuntimeNotFoundException
                    ? "Microsoft Edge WebView2 Runtime is not installed. Use the offline compatibility installer, which includes its browser engine."
                    : error.Message, "Universal Device Toolkit", 0x10);
            return 1;
        }
    }

    private static async Task DiagnoseAsync(ShellConfiguration configuration, string browserVersion, Action<string> log)
    {
        await using var host = new HostConnection(configuration.HostPath, ["--no-hardware", "--safe-start", "--disable-update-checker"], log);
        host.Start();
        var capabilities = await host.InvokeAsync("host.getCapabilities");
        Console.WriteLine(JsonSerializer.Serialize(new { browserVersion, host = host.Status, capabilities }));
    }
}
