using System.Diagnostics;
using UniversalDeviceToolkit.Abstractions.Lifecycle;

namespace UniversalDeviceToolkit.Platform.MacOS.Lifecycle;

/// <summary>
/// macOS implementation of <see cref="IAutorunManager"/>.
/// Manages autostart via a LaunchAgents plist file.
/// </summary>
public sealed class MacOSAutorunManager : IAutorunManager
{
    private static readonly string LaunchAgentsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents");

    private static readonly string PlistPath = Path.Combine(LaunchAgentsDir, "com.udt.app.plist");

    private const string PlistTemplate = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>com.udt.app</string>
            <key>ProgramArguments</key>
            <array>
                <string>/usr/local/bin/udt</string>
            </array>
            <key>RunAtLoad</key>
            <true/>
            <key>KeepAlive</key>
            <false/>
            <key>StandardOutPath</key>
            <string>/tmp/udt.log</string>
            <key>StandardErrorPath</key>
            <string>/tmp/udt.err</string>
        </dict>
        </plist>
        """;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(File.Exists(PlistPath));
    }

    /// <inheritdoc />
    public async Task EnableAsync()
    {
        Directory.CreateDirectory(LaunchAgentsDir);
        await File.WriteAllTextAsync(PlistPath, PlistTemplate).ConfigureAwait(false);
        await RunLaunchCtlAsync("load", "com.udt.app").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisableAsync()
    {
        await RunLaunchCtlAsync("unload", "com.udt.app").ConfigureAwait(false);
        if (File.Exists(PlistPath))
            File.Delete(PlistPath);
    }

    private static async Task RunLaunchCtlAsync(string command, string? label = null)
    {
        try
        {
            var args = command;
            if (label is not null)
                args += $" -w {Path.Combine(LaunchAgentsDir, label + ".plist")}";

            var psi = new ProcessStartInfo("launchctl", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process is not null)
                await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // launchctl may not be available in all environments
        }
    }
}
