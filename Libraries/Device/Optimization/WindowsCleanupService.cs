using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.Optimization;

public class WindowsCleanupService
{
    private readonly ApplicationSettings _applicationSettings;
    private delegate Task<long> EstimatorDelegate(CancellationToken ct, Action<string>? progressCallback);
    private readonly Dictionary<string, EstimatorDelegate> _estimators;

    public WindowsCleanupService(ApplicationSettings applicationSettings)
    {
        _applicationSettings = applicationSettings;
        _estimators = new Dictionary<string, EstimatorDelegate>(StringComparer.OrdinalIgnoreCase)
        {
            ["cleanup.browserCache"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%LocalAppData%\\Microsoft\\Windows\\INetCache",
                "%LocalAppData%\\Microsoft\\Windows\\INetCookies"),
            
            ["cleanup.thumbnailCache"] = async (ct, cb) =>
            {
                var results = await Task.WhenAll(
                    EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%LocalAppData%\\Microsoft\\Windows\\Explorer"), "thumbcache_*.db", ct, cb),
                    EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%LocalAppData%\\Local\\D3DSCache"), null, ct, cb)
                ).ConfigureAwait(false);
                return results.Sum();
            },

            ["cleanup.windowsUpdate"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%SystemRoot%\\SoftwareDistribution\\Download",
                "%SystemRoot%\\SoftwareDistribution\\DeliveryOptimization"),

            ["cleanup.tempFiles"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%SystemRoot%\\Temp",
                "%SystemDrive%\\Windows\\Temp",
                "%TEMP%"),

            ["cleanup.logs"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%SystemRoot%\\Logs",
                "%ProgramData%\\Microsoft\\Windows\\WER\\ReportQueue",
                "%ProgramData%\\Microsoft\\Diagnosis"),

            ["cleanup.crashDumps"] = async (ct, cb) => 
                await EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%SystemRoot%\\Minidump"), "*.dmp", ct, cb).ConfigureAwait(false) +
                await EstimateFileSizeAsync(Environment.ExpandEnvironmentVariables("%SystemRoot%\\memory.dmp"), ct).ConfigureAwait(false) +
                await EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%SystemDrive%\\"), "*.dmp", ct, cb).ConfigureAwait(false),

            ["cleanup.recycleBin"] = (ct, cb) => EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%SystemDrive%\\$Recycle.bin"), null, ct, cb),
            
            ["cleanup.defender"] = (ct, cb) => EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%ProgramData%\\Microsoft\\Windows Defender\\Scans"), null, ct, cb),
            
            ["cleanup.componentStore"] = (ct, cb) => EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%SystemRoot%\\WinSxS\\Temp"), null, ct, cb),
            
            ["cleanup.prefetch"] = (ct, cb) => EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%SystemRoot%\\Prefetch"), null, ct, cb),
            
            ["cleanup.dotnetNative"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%WinDir%\\assembly\\NativeImages_v4.0.30319_32",
                "%WinDir%\\assembly\\NativeImages_v4.0.30319_64"),

            ["cleanup.remoteDesktopCache"] = (ct, cb) => EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables("%LocalAppData%\\Microsoft\\Terminal Server Client\\Cache"), null, ct, cb),
            
            ["cleanup.shaderCache"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%LocalAppData%\\D3DSCache",
                "%LocalAppData%\\NVIDIA\\DXCache",
                "%LocalAppData%\\NVIDIA\\GLCache",
                "%LocalAppData%\\AMD\\DxCache",
                "%LocalAppData%\\Intel\\ShaderCache"),

            ["cleanup.instantMessaging"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%AppData%\\Tencent\\QQNT\\nt_qq\\nt_temp",
                "%AppData%\\Telegram Desktop\\tdata\\temp",
                "%AppData%\\DingTalk\\temp",
                "%LocalAppData%\\Tencent\\WeChat\\Caches"),

            ["cleanup.devCaches"] = (ct, cb) => EstimateMultipleDirectoriesAsync(ct, cb,
                "%LocalAppData%\\pip\\cache",
                "%LocalAppData%\\npm-cache",
                "%LocalAppData%\\Yarn\\Cache",
                "%LocalAppData%\\NuGet\\v3-cache"),

            ["cleanup.largeFiles"] = EstimateLargeFilesSizeAsync,
            ["cleanup.custom"] = EstimateCustomCleanupSizeAsync
        };
    }

    public async Task<long> EstimateActionSizeAsync(string actionKey, CancellationToken cancellationToken, Action<string>? progressCallback = null)
    {
        if (_estimators.TryGetValue(actionKey, out var estimator))
        {
            return await estimator(cancellationToken, progressCallback).ConfigureAwait(false);
        }
        return 0;
    }

    public async Task<long> EstimateCleanupSizeAsync(IEnumerable<string> actionKeys, CancellationToken cancellationToken, Action<string>? progressCallback = null)
    {
        long totalSize = 0;
        foreach (var key in actionKeys)
        {
            try
            {
                totalSize += await EstimateActionSizeAsync(key, cancellationToken, progressCallback).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to estimate cleanup size for action. [action={key}]", ex);
            }
        }

        return totalSize;
    }

    public async Task<List<FileInfo>> GetLargeFilesAsync(long minSize, CancellationToken cancellationToken, Action<string>? progressCallback = null)
    {
        return await Task.Run(() =>
        {
            var largeFiles = new List<FileInfo>();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var scanPaths = new[]
            {
                Path.Combine(userProfile, "Downloads"),
                Path.Combine(userProfile, "Desktop"),
                Path.Combine(userProfile, "Documents"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp"
            };

            foreach (var path in scanPaths)
            {
                if (!Directory.Exists(path)) continue;

                var stack = new Stack<string>();
                stack.Push(path);

                while (stack.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentPath = stack.Pop();
                    
                    progressCallback?.Invoke(currentPath);

                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(currentPath))
                        {
                            try
                            {
                                var fi = new FileInfo(file);
                                if (fi.Length >= minSize)
                                {
                                    largeFiles.Add(fi);
                                }
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                if (Log.Instance.IsTraceEnabled)
                                    Log.Instance.Trace($"Insufficient permissions to access file: {file}", ex);
                            }
                            catch (IOException ex)
                            {
                                if (Log.Instance.IsTraceEnabled)
                                    Log.Instance.Trace($"File may be locked or inaccessible: {file}", ex);
                            }
                        }

                        foreach (var subDir in Directory.EnumerateDirectories(currentPath))
                        {
                            try
                            {
                                var di = new DirectoryInfo(subDir);
                                if ((di.Attributes & FileAttributes.Hidden) == 0 && (di.Attributes & FileAttributes.System) == 0)
                                {
                                    stack.Push(subDir);
                                }
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                Log.Instance.TraceOnce(
                                    "cleanup-subdir-access",
                                    $"Insufficient permissions for cleanup subdirectory: {subDir}",
                                    ex);
                            }
                            catch (IOException ex)
                            {
                                Log.Instance.TraceOnce(
                                    "cleanup-subdir-io",
                                    $"Cleanup subdirectory inaccessible: {subDir}",
                                    ex);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Insufficient permissions to access directory: {currentPath}", ex);
                    }
                    catch (IOException ex)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Directory may be inaccessible: {currentPath}", ex);
                    }
                }
            }
            return largeFiles;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> EstimateLargeFilesSizeAsync(CancellationToken cancellationToken, Action<string>? progressCallback = null)
    {
        var largeFiles = await GetLargeFilesAsync(104857600L, cancellationToken, progressCallback).ConfigureAwait(false);
        return largeFiles.Sum(f => f.Length);
    }

    private async Task<long> EstimateMultipleDirectoriesAsync(CancellationToken ct, Action<string>? progressCallback, params string[] paths)
    {
        long total = 0;
        foreach (var path in paths)
        {
            total += await EstimateDirectorySizeAsync(Environment.ExpandEnvironmentVariables(path), null, ct, progressCallback).ConfigureAwait(false);
        }
        return total;
    }

    private async Task<long> EstimateDirectorySizeAsync(string directoryPath, string? filePattern = null, CancellationToken cancellationToken = default, Action<string>? progressCallback = null)
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        return await Task.Run(() =>
        {
            long size = 0;
            var stack = new Stack<string>();
            stack.Push(directoryPath);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentPath = stack.Pop();
                
                progressCallback?.Invoke(currentPath);

                try
                {
                    var files = filePattern == null
                        ? Directory.EnumerateFiles(currentPath)
                        : Directory.EnumerateFiles(currentPath, filePattern);

                    foreach (var file in files)
                    {
                        try
                        {
                            size += new FileInfo(file).Length;
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Insufficient permissions to access file: {file}", ex);
                        }
                        catch (IOException ex)
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"File may be locked or inaccessible: {file}", ex);
                        }
                    }

                    if (filePattern == null)
                    {
                        foreach (var subDir in Directory.EnumerateDirectories(currentPath))
                        {
                            stack.Push(subDir);
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Insufficient permissions to access directory: {currentPath}", ex);
                }
                catch (IOException ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Directory may be inaccessible: {currentPath}", ex);
                }
            }

            return size;
        }, cancellationToken).ConfigureAwait(false);
    }

    private Task<long> EstimateFileSizeAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return Task.FromResult(0L);

        try
        {
            return Task.FromResult(new FileInfo(filePath).Length);
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "cleanup-estimate-file",
                $"Failed to estimate file size for cleanup: {filePath}",
                ex);
            return Task.FromResult(0L);
        }
    }

    public async Task ExecuteCustomCleanupAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var rules = _applicationSettings.Store.CustomCleanupRules ?? new List<CustomCleanupRule>();

            foreach (var rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(rule.DirectoryPath))
                    continue;

                var directoryPath = Environment.ExpandEnvironmentVariables(rule.DirectoryPath.Trim());
                ValidateCleanupPath(directoryPath);

                if (!Directory.Exists(directoryPath))
                    continue;

                var normalizedExtensions = (rule.Extensions ?? [])
                    .Select(NormalizeExtension)
                    .Where(extension => !string.IsNullOrEmpty(extension))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (normalizedExtensions.Length == 0)
                    continue;

                var extensionsSet = new HashSet<string>(normalizedExtensions, StringComparer.OrdinalIgnoreCase);

                var stack = new Stack<string>();
                stack.Push(directoryPath);

                while (stack.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentPath = stack.Pop();

                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(currentPath))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var ext = Path.GetExtension(file);
                            if (!extensionsSet.Contains(ext))
                                continue;

                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                Log.Instance.TraceOnce(
                                    "cleanup-custom-delete",
                                    $"Custom cleanup could not delete file (locked or already gone): {file}",
                                    ex);
                            }
                        }

                        if (rule.Recursive)
                        {
                            foreach (var subDir in Directory.EnumerateDirectories(currentPath))
                            {
                                stack.Push(subDir);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Log.Instance.TraceOnce(
                            "cleanup-custom-dir-access",
                            $"Custom cleanup insufficient permissions: {currentPath}",
                            ex);
                    }
                    catch (IOException ex)
                    {
                        Log.Instance.TraceOnce(
                            "cleanup-custom-dir-io",
                            $"Custom cleanup directory inaccessible: {currentPath}",
                            ex);
                    }
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteRegistryCleanupAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Common user MRU/Recent registry keys (safe to clear)
            (RegistryHive Hive, string SubKey)[] targets =
            [
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppBadgeUpdated"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\AppLaunch"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\FeatureUsage\ShowJumpView"),
                (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search\RecentApps")
            ];

            foreach (var (hive, subKey) in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var key = baseKey.OpenSubKey(subKey, writable: true);
                    if (key is null)
                        continue;

                    // Delete values
                    foreach (var valueName in key.GetValueNames())
                    {
                        try { key.DeleteValue(valueName, throwOnMissingValue: false); }
                        catch (UnauthorizedAccessException ex)
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Unauthorized access to registry value. [key={hive}\\{subKey}, value={valueName}]", ex);
                        }
                        catch (IOException ex)
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"IO error accessing registry value. [key={hive}\\{subKey}, value={valueName}]", ex);
                        }
                    }
                    // Delete subkeys
                    foreach (var child in key.GetSubKeyNames())
                    {
                        try { key.DeleteSubKeyTree(child, throwOnMissingSubKey: false); }
                        catch (UnauthorizedAccessException ex)
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Unauthorized access to registry subkey. [key={hive}\\{subKey}, subkey={child}]", ex);
                        }
                        catch (IOException ex)
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"IO error accessing registry subkey. [key={hive}\\{subKey}, subkey={child}]", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Registry cleanup failed. [key={hive}\\{subKey}]", ex);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateCleanupPath(string expandedPath)
    {
        var allowedPrefixes = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Temp",
            Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Temp",
            Path.GetTempPath().TrimEnd('\\'),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Windows\INetCache",
            Environment.GetFolderPath(Environment.SpecialFolder.InternetCache),
        };

        var normalizedPath = Path.GetFullPath(expandedPath);
        if (!allowedPrefixes.Any(prefix => normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException(
                $"Cleanup path '{expandedPath}' is not within allowed directories.");
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        extension = extension.Trim();
        if (!extension.StartsWith('.'))
            extension = "." + extension;

        return extension;
    }

    private async Task<long> EstimateCustomCleanupSizeAsync(CancellationToken cancellationToken, Action<string>? progressCallback = null)
    {
        long total = 0;
        var rules = _applicationSettings.Store.CustomCleanupRules ?? new List<CustomCleanupRule>();
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.DirectoryPath))
                continue;

            var directoryPath = Environment.ExpandEnvironmentVariables(rule.DirectoryPath.Trim());
            ValidateCleanupPath(directoryPath);
            if (Directory.Exists(directoryPath))
            {
                var extensions = (rule.Extensions ?? [])
                    .SelectMany(ext => ext.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries))
                    .Select(e => e.Trim())
                    .Where(e => !string.IsNullOrEmpty(e))
                    .Select(e => e.StartsWith('.') ? e : "." + e)
                    .ToArray();

                if (extensions.Length > 0)
                {
                    var extensionsSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
                    total += await EstimateDirectorySizeWithExtensionsAsync(directoryPath, extensionsSet, rule.Recursive, cancellationToken, progressCallback).ConfigureAwait(false);
                }
            }
        }
        return total;
    }

    private async Task<long> EstimateDirectorySizeWithExtensionsAsync(string directoryPath, HashSet<string> extensions, bool recursive, CancellationToken cancellationToken, Action<string>? progressCallback)
    {
        return await Task.Run(() =>
        {
            long size = 0;
            var stack = new Stack<string>();
            stack.Push(directoryPath);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentPath = stack.Pop();
                progressCallback?.Invoke(currentPath);

                try
                {
                    foreach (var file in Directory.EnumerateFiles(currentPath))
                    {
                        try
                        {
                            var ext = Path.GetExtension(file);
                            if (extensions.Contains(ext))
                            {
                                size += new FileInfo(file).Length;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Instance.TraceOnce(
                                "cleanup-size-file",
                                $"Could not read file size during cleanup estimate: {file}",
                                ex);
                        }
                    }

                    if (recursive)
                    {
                        foreach (var subDir in Directory.EnumerateDirectories(currentPath))
                        {
                            stack.Push(subDir);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Instance.TraceOnce(
                        "cleanup-size-dir",
                        $"Could not enumerate directory during cleanup estimate: {currentPath}",
                        ex);
                }
            }
            return size;
        }, cancellationToken).ConfigureAwait(false);
    }
}
