using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Custom cleanup rule bridge: read/write the persisted rule set
/// (ApplicationSettings.CustomCleanupRules → CustomCleanupRule).
/// </summary>
public static class CleanupRulesHandlers
{
    private const int MaximumRuleCount = 64;
    private const int MaximumExtensionCount = 32;
    private const int MaximumExtensionLength = 16;

    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    private static ApplicationSettings Settings => IoCContainer.Resolve<ApplicationSettings>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("cleanup.getCustomRules", (_, _) => Task.FromResult(HandleGetCustomRules()));
        rpc.RegisterHandler("cleanup.saveCustomRules", (request, _) => Task.FromResult(HandleSaveCustomRules(request)));
    }

    private static BridgeResult HandleGetCustomRules()
    {
        try
        {
            var rules = Settings.Store.CustomCleanupRules ?? [];

            return BridgeResult.Ok(new
            {
                rules = rules.Select(rule => new
                {
                    directoryPath = rule.DirectoryPath ?? string.Empty,
                    extensions = (rule.Extensions ?? []).ToArray(),
                    recursive = rule.Recursive,
                }).ToArray(),
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static BridgeResult HandleSaveCustomRules(BridgeRequest request)
    {
        try
        {
            if (request.Parameters.ValueKind != JsonValueKind.Object ||
                !request.Parameters.TryGetProperty("rules", out var rulesProp) ||
                rulesProp.ValueKind != JsonValueKind.Array)
            {
                throw new BridgeErrorException(-32602, "Missing or invalid array parameter 'rules'.");
            }

            if (rulesProp.GetArrayLength() > MaximumRuleCount)
                throw new BridgeErrorException(-32602, $"Too many cleanup rules (maximum {MaximumRuleCount}).");

            List<CustomCleanupRule>? incoming;
            try
            {
                incoming = JsonSerializer.Deserialize<List<CustomCleanupRule>>(rulesProp.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (JsonException ex)
            {
                throw new BridgeErrorException(-32602, $"Invalid 'rules' payload. {ex.Message}");
            }

            var normalized = new List<CustomCleanupRule>();
            foreach (var rule in incoming ?? [])
            {
                if (rule is null)
                    continue;
                normalized.Add(NormalizeAndValidateRule(rule));
            }

            Settings.Store.CustomCleanupRules = normalized;
            Settings.SynchronizeStore();

            return BridgeResult.Ok(new { saved = true });
        }
        catch (BridgeErrorException ex)
        {
            return BridgeResult.Error(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Rejects persisted custom-cleanup directories that would target system roots
    /// before cleanup.run expands and deletes under them.
    /// </summary>
    internal static void EnsureStoredCleanupRulesAreSafe()
    {
        var rules = Settings.Store.CustomCleanupRules ?? [];
        foreach (var rule in rules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.DirectoryPath))
                continue;
            NormalizeDirectoryPath(rule.DirectoryPath);
        }
    }

    private static CustomCleanupRule NormalizeAndValidateRule(CustomCleanupRule rule)
    {
        return new CustomCleanupRule
        {
            DirectoryPath = NormalizeDirectoryPath(rule.DirectoryPath),
            Extensions = NormalizeExtensions(rule.Extensions),
            Recursive = rule.Recursive,
        };
    }

    internal static string NormalizeDirectoryPath(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath must be a non-empty path.");

        var raw = directoryPath.Trim();
        if (raw.IndexOf('\0') >= 0)
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath contains an invalid character.");

        if (raw.Contains("..", StringComparison.Ordinal))
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath must not contain path traversal.");

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(raw);
        }
        catch (ArgumentException ex)
        {
            throw new BridgeErrorException(-32602, $"Cleanup rule directoryPath is invalid. {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(expanded) || expanded.Contains('%', StringComparison.Ordinal))
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath contains an unresolved environment variable.");

        if (expanded.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            expanded.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            expanded.StartsWith("//./", StringComparison.Ordinal) ||
            expanded.StartsWith("//?/", StringComparison.Ordinal))
        {
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath must not use a device namespace.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(expanded);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new BridgeErrorException(-32602, $"Cleanup rule directoryPath is invalid. {ex.Message}");
        }

        if (!Path.IsPathRooted(fullPath))
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath must be an absolute path.");

        if (ContainsReservedDeviceName(fullPath))
            throw new BridgeErrorException(-32602, "Cleanup rule directoryPath contains a reserved device name.");

        if (IsUnsafeCleanupDirectory(fullPath))
            throw new BridgeErrorException(-32602, $"Cleanup rule directoryPath '{raw}' is not allowed.");

        return fullPath;
    }

    private static List<string> NormalizeExtensions(List<string>? extensions)
    {
        var normalized = new List<string>();
        if (extensions is null)
            return normalized;

        if (extensions.Count > MaximumExtensionCount)
            throw new BridgeErrorException(-32602, $"Too many cleanup extensions (maximum {MaximumExtensionCount}).");

        foreach (var extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
                continue;

            var value = extension.Trim();
            if (value.Contains(',', StringComparison.Ordinal) || value.Contains(';', StringComparison.Ordinal))
                throw new BridgeErrorException(-32602, $"Cleanup rule extension '{extension}' must be a single extension.");

            if (!value.StartsWith('.'))
                value = "." + value;

            if (value.Length is < 2 or > MaximumExtensionLength)
                throw new BridgeErrorException(-32602, $"Cleanup rule extension '{extension}' is invalid.");

            if (value.Contains("..", StringComparison.Ordinal) ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                value.Contains('*', StringComparison.Ordinal) ||
                value.Contains('?', StringComparison.Ordinal))
            {
                throw new BridgeErrorException(-32602, $"Cleanup rule extension '{extension}' is invalid.");
            }

            if (!normalized.Contains(value, StringComparer.OrdinalIgnoreCase))
                normalized.Add(value);
        }

        return normalized;
    }

    private static bool IsUnsafeCleanupDirectory(string fullPath)
    {
        var path = TrimSlash(fullPath);
        if (IsDriveRoot(path))
            return true;

        if (path.Contains("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("Recycler", StringComparison.OrdinalIgnoreCase))
            return true;

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var windowsTemp = string.IsNullOrWhiteSpace(windows) ? null : Path.Combine(windows, "Temp");
        if (IsSameOrUnder(path, windows) && !IsSameOrUnder(path, windowsTemp))
            return true;

        foreach (var root in EnumerateForbiddenRoots())
        {
            if (IsSameOrUnder(path, root))
                return true;
        }

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        var usersRoot = string.IsNullOrEmpty(systemRoot) ? null : Path.Combine(systemRoot, "Users");
        if (IsSamePath(path, usersRoot))
            return true;

        return false;
    }

    private static IEnumerable<string> EnumerateForbiddenRoots()
    {
        yield return Environment.SystemDirectory;
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        if (!string.IsNullOrEmpty(systemRoot))
        {
            yield return Path.Combine(systemRoot, "Recovery");
            yield return Path.Combine(systemRoot, "EFI");
            yield return Path.Combine(systemRoot, "Boot");
        }
    }

    private static bool ContainsReservedDeviceName(string fullPath)
    {
        foreach (var segment in fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;
            var name = Path.GetFileNameWithoutExtension(segment);
            if (ReservedDeviceNames.Any(reserved => reserved.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static bool IsDriveRoot(string path)
    {
        var root = Path.GetPathRoot(path);
        return !string.IsNullOrEmpty(root) && IsSamePath(path, root);
    }

    private static bool IsSameOrUnder(string path, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return false;

        string fullRoot;
        try
        {
            fullRoot = TrimSlash(Path.GetFullPath(root));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (IsSamePath(path, fullRoot))
            return true;

        var prefix = fullRoot + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSamePath(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right))
            return false;
        return TrimSlash(left).Equals(TrimSlash(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimSlash(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
