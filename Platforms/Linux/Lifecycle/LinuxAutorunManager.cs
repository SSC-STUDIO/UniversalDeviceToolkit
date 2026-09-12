using UniversalDeviceToolkit.Abstractions.Lifecycle;

namespace UniversalDeviceToolkit.Platform.Linux.Lifecycle;

/// <summary>
/// Linux autostart via the same XDG desktop file Electron writes
/// (<c>~/.config/autostart/universal-device-toolkit.desktop</c>).
/// Prefers <c>UDT_SHELL_PATH</c> (the Electron UI) when Host was spawned by it.
/// </summary>
public sealed class LinuxAutorunManager : IAutorunManager
{
    public const string DesktopFileName = "universal-device-toolkit.desktop";

    private readonly string _desktopPath;
    private readonly Func<string> _resolveExecPath;

    public LinuxAutorunManager()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "autostart",
                DesktopFileName),
            ResolveExecPath)
    {
    }

    public LinuxAutorunManager(string desktopPath, Func<string>? resolveExecPath = null)
    {
        _desktopPath = desktopPath ?? throw new ArgumentNullException(nameof(desktopPath));
        _resolveExecPath = resolveExecPath ?? ResolveExecPath;
    }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(File.Exists(_desktopPath));

    /// <inheritdoc />
    public async Task EnableAsync()
    {
        var directory = Path.GetDirectoryName(_desktopPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var exec = _resolveExecPath();
        var contents =
            "[Desktop Entry]\n" +
            "Type=Application\n" +
            "Name=Universal Device Toolkit\n" +
            $"Exec={QuoteDesktopExec(exec)}\n" +
            "X-GNOME-Autostart-enabled=true\n";
        await File.WriteAllTextAsync(_desktopPath, contents).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DisableAsync()
    {
        if (File.Exists(_desktopPath))
            File.Delete(_desktopPath);
        return Task.CompletedTask;
    }

    internal static string QuoteDesktopExec(string filePath) =>
        $"\"{filePath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    internal static string ResolveExecPath()
    {
        var shell = Environment.GetEnvironmentVariable("UDT_SHELL_PATH");
        if (!string.IsNullOrWhiteSpace(shell) && File.Exists(shell))
            return shell;

        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        if (!string.IsNullOrWhiteSpace(appImage) && File.Exists(appImage) &&
            !appImage.Contains("/tmp/.mount_", StringComparison.OrdinalIgnoreCase))
        {
            return appImage;
        }

        return Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot enable Linux autostart: no executable path is available.");
    }
}
