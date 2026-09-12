using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UniversalDeviceToolkit.CLI.Lib;

public static class Constants
{
    /// <summary>
    /// Legacy LLT-compatible pipe name. Server primary listen target for backward compatibility.
    /// </summary>
    public const string DEFAULT_PIPE_NAME = "LenovoLegionToolkit-IPC-0";

    /// <summary>
    /// UDT-branded preferred pipe name. Clients try this first; server also listens here.
    /// </summary>
    public const string PREFERRED_PIPE_NAME = "UniversalDeviceToolkit-IPC-0";

#if UDT_TEST_HOOKS
    public const string APPDATA_OVERRIDE_ENVIRONMENT_VARIABLE = "UDT_APPDATA_OVERRIDE";
#endif

    /// <summary>
    /// Resolves a pipe name from the default (legacy) base name, optionally isolation-suffixed.
    /// </summary>
    public static string GetPipeName(string? isolationPath = null)
        => GetPipeName(isolationPath, DEFAULT_PIPE_NAME);

    /// <summary>
    /// Resolves a pipe name from <paramref name="baseName"/>, optionally isolation-suffixed.
    /// The same path hash is applied to any base so dual UDT/LLT names stay paired under isolation.
    /// </summary>
    public static string GetPipeName(string? isolationPath, string baseName)
    {
        var resolvedBase = string.IsNullOrWhiteSpace(baseName) ? DEFAULT_PIPE_NAME : baseName;

        if (string.IsNullOrWhiteSpace(isolationPath))
            return resolvedBase;

        try
        {
            var fullPath = Path.GetFullPath(isolationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(fullPath))
                return resolvedBase;

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath));
            var suffix = Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
            return $"{resolvedBase}-{suffix}";
        }
        catch
        {
            return resolvedBase;
        }
    }

    /// <summary>
    /// Server listen order: legacy DEFAULT (primary) then preferred UDT.
    /// </summary>
    public static string[] GetServerPipeNames(string? isolationPath = null)
        =>
        [
            GetPipeName(isolationPath, DEFAULT_PIPE_NAME),
            GetPipeName(isolationPath, PREFERRED_PIPE_NAME)
        ];

    /// <summary>
    /// Client connect order: preferred UDT first, then legacy DEFAULT fallback.
    /// </summary>
    public static string[] GetClientPipeNames(string? isolationPath = null)
        =>
        [
            GetPipeName(isolationPath, PREFERRED_PIPE_NAME),
            GetPipeName(isolationPath, DEFAULT_PIPE_NAME)
        ];

    public static string[] GetClientPipeNamesFromEnvironment()
    {
#if UDT_TEST_HOOKS
        var overridePath = Environment.GetEnvironmentVariable(APPDATA_OVERRIDE_ENVIRONMENT_VARIABLE);
        return GetClientPipeNames(overridePath);
#else
        return GetClientPipeNames();
#endif
    }
}
