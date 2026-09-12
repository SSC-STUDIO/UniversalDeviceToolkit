using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Lib.Utils;

public static class ExplorerRestartHelper
{
    private static IDelayProvider _delayProvider = new DefaultDelayProvider();

    public static void SetDelayProvider(IDelayProvider delayProvider) => _delayProvider = delayProvider;

    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly string ExplorerExecutablePath = GetExplorerExecutablePath();

    public static async Task RestartAsync()
    {
        Log.Instance.Info("Restarting Explorer.");
        await KillExplorerAsync().ConfigureAwait(false);
        await WaitForExplorerStateAsync(shouldBeRunning: false, ShutdownTimeout).ConfigureAwait(false);
        await StartExplorerAsync().ConfigureAwait(false);
        await WaitForExplorerStateAsync(shouldBeRunning: true, StartupTimeout).ConfigureAwait(false);
        Log.Instance.Info("Explorer restarted successfully.");
    }

    private static async Task KillExplorerAsync()
    {
        using var killProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "taskkill.exe",
            Arguments = "/f /im explorer.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (killProcess is null)
            return;

        await killProcess.WaitForExitAsync().ConfigureAwait(false);
        killProcess.Kill(true);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace("Explorer termination command completed.");
    }

    private static async Task StartExplorerAsync()
    {
        Exception? lastException = null;

        foreach (var startInfo in GetStartOptions())
        {
            try
            {
                using var process = Process.Start(startInfo);

                await _delayProvider.Delay(PollInterval, CancellationToken.None).ConfigureAwait(false);

                if (IsExplorerRunning())
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Explorer launch succeeded. [launcher={startInfo.FileName} {startInfo.Arguments}]");
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to start Explorer. [launcher={startInfo.FileName} {startInfo.Arguments}]",
                        ex);
            }
        }

        if (lastException is not null)
            throw lastException;

        throw ExceptionHelper.FailedToStartExplorer();
    }

    private static ProcessStartInfo[] GetStartOptions()
    {
        return
        [
            new ProcessStartInfo
            {
                FileName = ExplorerExecutablePath,
                UseShellExecute = true
            },
            new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"\" \"{ExplorerExecutablePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        ];
    }

    private static async Task WaitForExplorerStateAsync(bool shouldBeRunning, TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            if (IsExplorerRunning() == shouldBeRunning)
                return;

            await _delayProvider.Delay(PollInterval, CancellationToken.None).ConfigureAwait(false);
        }

        throw shouldBeRunning ? ExceptionHelper.ExplorerDidNotRestart() : ExceptionHelper.ExplorerDidNotExit();
    }

    private static string GetExplorerExecutablePath()
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            var candidate = Path.Combine(windowsDirectory, "explorer.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return "explorer.exe";
    }

    private static bool IsExplorerRunning()
    {
        var processes = Process.GetProcessesByName("explorer");

        try
        {
            return processes.Any(process => !process.HasExited);
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }
}
