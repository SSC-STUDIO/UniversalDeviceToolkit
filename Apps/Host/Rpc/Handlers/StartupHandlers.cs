using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.System;
using UniversalDeviceToolkit.Shared.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Startup behavior bridge — mirrors the WPF SettingsApplicationBehaviorControl
/// autorun combo (AutorunState: Enabled / EnabledDelayed / Disabled).
/// On Windows this is a logon scheduled task. Electron sets UDT_SHELL_PATH so
/// the task launches the UI shell rather than Host.exe.
/// </summary>
public static class StartupHandlers
{
    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("app.getAutorun", (_, _) => HandleGetAutorunAsync());
        rpc.RegisterHandler("app.setAutorun", (request, _) => HandleSetAutorunAsync(request));
    }

    private static Task<BridgeResult> HandleGetAutorunAsync()
    {
        try
        {
            return Task.FromResult(BridgeResult.Ok(new { state = Autorun.State.ToString() }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static Task<BridgeResult> HandleSetAutorunAsync(BridgeRequest request)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("state", out var stateProp) ||
                stateProp.ValueKind != JsonValueKind.String)
                return Task.FromResult(BridgeResult.Error(-32602, "Missing string parameter 'state'."));

            if (!Enum.TryParse<AutorunState>(stateProp.GetString()!, ignoreCase: true, out var state))
                return Task.FromResult(BridgeResult.Error(-32602, $"Unknown AutorunState '{stateProp.GetString()}'."));

            if (state != AutorunState.Disabled && !TryValidateShellLaunchPath(out var shellError))
                return Task.FromResult(BridgeResult.Error(-32602, shellError));

            Autorun.Set(state);

            var applied = Autorun.State;
            if (applied != state)
            {
                return Task.FromResult(BridgeResult.Error(
                    -32603,
                    $"Autorun state was not applied (requested {state}, current {applied})."));
            }

            return Task.FromResult(BridgeResult.Ok(new { ok = true, state = applied.ToString() }));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(BridgeResult.Error(
                BridgeErrorCodes.ElevationRequired,
                $"app.setAutorun requires elevation. {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>
    /// Autorun.Enable launches whatever <c>UDT_SHELL_PATH</c> points at. Reject an
    /// existing path that is not a trusted .exe under the Host/install tree so a
    /// hostile environment variable cannot become the logon task action.
    /// Missing/empty values are ignored by Autorun (it falls back to Host.exe).
    /// </summary>
    private static bool TryValidateShellLaunchPath(out string error)
    {
        error = string.Empty;
        var shell = Environment.GetEnvironmentVariable("UDT_SHELL_PATH");
        if (string.IsNullOrWhiteSpace(shell) || !File.Exists(shell))
            return true;

        if (IsTrustedShellExecutable(shell))
            return true;

        error = "UDT_SHELL_PATH is not a trusted executable path.";
        return false;
    }

    private static bool IsTrustedShellExecutable(string shellPath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(shellPath);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException or IOException)
        {
            return false;
        }

        if (!File.Exists(fullPath))
            return false;

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (Exception)
        {
            return false;
        }

        if ((attributes & FileAttributes.Directory) != 0)
            return false;
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return false;
        if (!string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!PathSecurity.IsValidFileName(Path.GetFileName(fullPath)))
            return false;

        foreach (var root in EnumerateTrustedLaunchRoots())
        {
            if (PathSecurity.IsPathWithinAllowedDirectory(fullPath, root, allowNonExistent: false))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateTrustedLaunchRoots()
    {
        if (IsUsableLaunchRoot(Folders.Program))
            yield return Folders.Program;

        string? directory = null;
        try
        {
            directory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
        }
        catch (Exception)
        {
            directory = null;
        }

        for (var depth = 0; depth < 6; depth++)
        {
            if (directory is not { Length: > 0 } current || !IsUsableLaunchRoot(current))
                break;

            yield return current;
            directory = Path.GetDirectoryName(current);
        }
    }

    private static bool IsUsableLaunchRoot(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return false;

        string fullDirectory;
        string? root;
        try
        {
            fullDirectory = Path.GetFullPath(directory);
            root = Path.GetPathRoot(fullDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException or IOException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(root))
            return false;

        return !string.Equals(
            fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
