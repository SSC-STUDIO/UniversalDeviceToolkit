using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Host.Rpc;

namespace UniversalDeviceToolkit.Host.Rpc.Handlers;

/// <summary>
/// Driver download bridge: scans Vantage / PC Support package catalogs, downloads
/// installer files to the configured folder, launches elevated installers and
/// persists download settings (path, update-only filter, hidden packages).
///
/// Mirrors WindowsOptimizationPage.Drivers.cs + PackageControlViewModel: a package
/// start downloads the file and then runs the installer; pause cancels the active
/// download or kills the running installer; statuses/progress are tracked in-process
/// and returned by driver.getPackages / driver.getPackageStatuses.
/// </summary>
public static class DriverDownloadHandlers
{
    private const string StatusNotStarted = "NotStarted";
    private const string StatusDownloading = "Downloading";
    private const string StatusInstalling = "Installing";
    private const string StatusCompleted = "Completed";
    private const string StatusError = "Error";

    /// <summary>Per-package download/install lifecycle state (guarded by <see cref="SyncRoot"/>).</summary>
    private sealed class PackageRunState
    {
        public string Status { get; set; } = StatusNotStarted;
        public float Progress { get; set; }
        public string? Error { get; set; }
        public CancellationTokenSource? DownloadCts { get; set; }
        public Process? InstallProcess { get; set; }
        public string? DownloadedFilePath { get; set; }
        public string? VerifiedSha256 { get; set; }
    }

    private sealed record PackageCacheEntry(Package Package, PackageDownloaderFactory.Type Source);

    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, PackageCacheEntry> PackageCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, PackageRunState> RunStates = new(StringComparer.Ordinal);

    private static PackageDownloaderFactory Factory => IoCContainer.Resolve<PackageDownloaderFactory>();

    private static PackageDownloaderSettings Settings => IoCContainer.Resolve<PackageDownloaderSettings>();

    public static void Register(BridgeRpcServer rpc)
    {
        rpc.RegisterHandler("driver.getSettings", (request, ct) => HandleGetSettingsAsync(request, ct));
        rpc.RegisterHandler("driver.getPackages", (request, ct) => HandleGetPackagesAsync(request, ct));
        rpc.RegisterHandler("driver.getPackageStatuses", (request, ct) => HandleGetPackageStatusesAsync(request, ct));
        rpc.RegisterHandler("driver.start", (request, ct) => HandleStartAsync(request, ct));
        rpc.RegisterHandler("driver.pause", (request, ct) => HandlePauseAsync(request, ct));
        rpc.RegisterHandler("driver.install", (request, ct) => HandleInstallAsync(request, ct));
        rpc.RegisterHandler("driver.uninstall", (request, ct) => HandleUninstallAsync(request, ct));
        rpc.RegisterHandler("driver.setDownloadPath", (request, ct) => HandleSetDownloadPathAsync(request, ct));
        rpc.RegisterHandler("driver.setOnlyShowUpdates", (request, ct) => HandleSetOnlyShowUpdatesAsync(request, ct));
        rpc.RegisterHandler("driver.setHiddenPackageIds", (request, ct) => HandleSetHiddenPackageIdsAsync(request, ct));
    }

    private static async Task<BridgeResult> HandleGetSettingsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var store = Settings.Store;
            var currentOs = OSExtensions.GetCurrent();

            string machineType = string.Empty;
            try
            {
                var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
                machineType = machineInfo.MachineType;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to resolve machine type for driver settings. [message={ex.Message}]", ex);
            }

            return BridgeResult.Ok(new
            {
                machineType,
                os = currentOs.ToString(),
                osOptions = Enum.GetNames<OS>(),
                downloadPath = GetEffectiveDownloadPath(store),
                onlyShowUpdates = store.OnlyShowUpdates,
                hiddenPackageIds = store.HiddenPackages.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                uninstallAvailable = false,
            });
        }
        catch (Exception ex)
        {
            return BridgeResult.Error(-32603, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<BridgeResult> HandleGetPackagesAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!request.Parameters.TryGetProperty("machineType", out var machineTypeProp) ||
                machineTypeProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(machineTypeProp.GetString()))
                throw new BridgeErrorException(-32602, "Missing or invalid string parameter 'machineType'.");

            var machineType = machineTypeProp.GetString()!.Trim();
            var os = TryGetOs(request) ?? OSExtensions.GetCurrent();
            var source = TryGetSource(request);
            if (source is null)
                throw new BridgeErrorException(-32602, "Missing or invalid parameter 'source' (expected 'Vantage' or 'PCSupport').");

            List<Package> packages;
            try
            {
                packages = await Factory.GetInstance(source.Value)
                    .GetPackagesAsync(machineType, os, progress: null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Driver package scan failed. [machineType={machineType}, os={os}, source={source.Value}]", ex);
                return BridgeResult.Ok(new { packages = Array.Empty<object>(), error = $"{ex.GetType().Name}: {ex.Message}" });
            }

            lock (SyncRoot)
            {
                foreach (var package in packages)
                    PackageCache[package.Id] = new PackageCacheEntry(package, source.Value);
            }

            return BridgeResult.Ok(new
            {
                packages = packages.Select(package => ToPackageDefinition(package, GetRunState(package.Id))).ToArray(),
            });
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

    private static async Task<BridgeResult> HandleGetPackageStatusesAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var packageIds = TryGetStringArray(request, "packageIds");
            if (packageIds is null)
                throw new BridgeErrorException(-32602, "Missing or invalid array parameter 'packageIds'.");

            var definitions = new List<object>(packageIds.Length);
            foreach (var packageId in packageIds)
            {
                PackageCacheEntry? entry;
                lock (SyncRoot)
                    PackageCache.TryGetValue(packageId, out entry);
                if (entry is null)
                    continue;

                definitions.Add(ToPackageDefinition(entry.Package, GetRunState(packageId)));
            }

            await Task.CompletedTask;
            return BridgeResult.Ok(new { packages = definitions });
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

    private static async Task<BridgeResult> HandleStartAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var packageId = GetRequiredString(request, "packageId");
            var entry = GetCachedPackage(packageId);
            if (entry is null)
                return BridgeResult.Ok(new { ok = false, error = $"Package '{packageId}' is not in the scan cache; run a scan first." });

            StartOrResumePackage(entry);
            await Task.CompletedTask;
            return BridgeResult.Ok(new { ok = true });
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

    private static async Task<BridgeResult> HandlePauseAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var packageId = GetRequiredString(request, "packageId");
            lock (SyncRoot)
            {
                if (!RunStates.TryGetValue(packageId, out var state))
                    return BridgeResult.Ok(new { ok = true });

                if (state.Status == StatusDownloading)
                {
                    state.DownloadCts?.Cancel();
                    state.DownloadCts?.Dispose();
                    state.DownloadCts = null;
                    ResetToNotStartedLocked(state);
                }
                else if (state.Status == StatusInstalling)
                {
                    StopInstallProcessLocked(state);
                    ResetToNotStartedLocked(state);
                }
            }

            await Task.CompletedTask;
            return BridgeResult.Ok(new { ok = true });
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

    private static async Task<BridgeResult> HandleInstallAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var packageId = GetRequiredString(request, "packageId");
            var entry = GetCachedPackage(packageId);
            if (entry is null)
                return BridgeResult.Ok(new { ok = false, error = $"Package '{packageId}' is not in the scan cache; run a scan first." });

            lock (SyncRoot)
            {
                var state = GetOrCreateRunState(packageId);
                if (state.Status is StatusCompleted or StatusDownloading or StatusInstalling)
                    return BridgeResult.Ok(new { ok = true });
            }

            var filePath = FindDownloadedFile(entry.Package, GetEffectiveDownloadPath(Settings.Store), GetOrCreateRunState(packageId));
            if (filePath is null)
                return BridgeResult.Ok(new { ok = false, error = "Installer file is not downloaded yet; start the download first." });

            StartInstall(packageId, entry.Package, filePath);
            await Task.CompletedTask;
            return BridgeResult.Ok(new { ok = true });
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

    private static async Task<BridgeResult> HandleUninstallAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _ = GetRequiredString(request, "packageId");
            await Task.CompletedTask;

            // Lib exposes no uninstall capability (the WPF PackageControl offers none either).
            return BridgeResult.Ok(new
            {
                ok = false,
                available = false,
                error = "Driver uninstall is not supported.",
            });
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

    private static async Task<BridgeResult> HandleSetDownloadPathAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var path = GetRequiredString(request, "path");
            var store = Settings.Store;
            if (!Directory.Exists(path))
                return BridgeResult.Ok(new { saved = false, error = "Directory does not exist." });

            store.DownloadPath = path;
            Settings.SynchronizeStore();
            await Task.CompletedTask;
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

    private static async Task<BridgeResult> HandleSetOnlyShowUpdatesAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var enabled = GetRequiredBoolean(request, "enabled");
            Settings.Store.OnlyShowUpdates = enabled;
            Settings.SynchronizeStore();
            await Task.CompletedTask;
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

    private static async Task<BridgeResult> HandleSetHiddenPackageIdsAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var packageIds = TryGetStringArray(request, "packageIds");
            if (packageIds is null)
                throw new BridgeErrorException(-32602, "Missing or invalid array parameter 'packageIds'.");

            Settings.Store.HiddenPackages = new HashSet<string>(packageIds);
            Settings.SynchronizeStore();
            await Task.CompletedTask;
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

    /// <summary>Starts a download (then auto-install, mirroring the WPF control) or re-runs the installer.</summary>
    private static void StartOrResumePackage(PackageCacheEntry entry)
    {
        var packageId = entry.Package.Id;
        PackageRunState state;
        lock (SyncRoot)
        {
            state = GetOrCreateRunState(packageId);
            if (state.Status is StatusCompleted or StatusDownloading or StatusInstalling)
                return;

            state.Status = StatusDownloading;
            state.Progress = 0;
            state.Error = null;
            state.DownloadCts?.Dispose();
            state.DownloadCts = new CancellationTokenSource();
        }

        var downloadPath = GetEffectiveDownloadPath(Settings.Store);
        var existingFile = FindDownloadedFile(entry.Package, downloadPath, state);
        if (existingFile is not null)
        {
            StartInstall(packageId, entry.Package, existingFile);
            return;
        }

        var downloader = Factory.GetInstance(entry.Source);
        var cts = state.DownloadCts;
        var progress = new Progress<float>(value => UpdateProgress(packageId, value));

        _ = Task.Run(async () =>
        {
            try
            {
                var filePath = await downloader
                    .DownloadPackageFileAsync(entry.Package, downloadPath, progress, cts.Token)
                    .ConfigureAwait(false);

                if (!TryPinVerifiedSha256(entry.Package, filePath, out var pinnedSha256, out var pinError))
                {
                    SetErrorState(packageId, pinError);
                    return;
                }

                lock (SyncRoot)
                {
                    state.DownloadedFilePath = filePath;
                    state.VerifiedSha256 = pinnedSha256;
                }

                StartInstall(packageId, entry.Package, filePath);
            }
            catch (OperationCanceledException)
            {
                ResetToNotStarted(packageId);
            }
            catch (Exception ex)
            {
                SetErrorState(packageId, $"{ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    /// <summary>Validates path, name, reparse and SHA-256, then launches the installer elevated (UAC).</summary>
    private static void StartInstall(string packageId, Package package, string filePath)
    {
        string? expectedSha256;
        lock (SyncRoot)
        {
            var state = GetOrCreateRunState(packageId);
            if (state.Status is StatusCompleted or StatusInstalling)
                return;

            state.Status = StatusInstalling;
            state.Progress = 1;
            state.Error = null;
            state.DownloadCts?.Cancel();
            state.DownloadCts?.Dispose();
            state.DownloadCts = null;
            expectedSha256 = ResolveExpectedInstallerSha256(package, state);
        }

        if (expectedSha256 is null)
        {
            SetErrorState(packageId, "Installer checksum is unknown; download the package again.");
            return;
        }

        var downloadPath = GetEffectiveDownloadPath(Settings.Store);
        if (!InstallerLaunchPathValidator.TryValidateForExecution(
                filePath,
                downloadPath,
                GetActualFileName(package),
                expectedSha256,
                out var safeInstallerPath,
                out var validationError))
        {
            SetErrorState(packageId, validationError);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = safeInstallerPath,
                UseShellExecute = true,
                Verb = "runas",
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                SetErrorState(packageId, "Failed to start installer process.");
                return;
            }

            process.EnableRaisingEvents = true;
            lock (SyncRoot)
            {
                if (RunStates.TryGetValue(packageId, out var state) && state.Status == StatusInstalling)
                    state.InstallProcess = process;
            }

            process.Exited += (_, _) => HandleInstallExit(packageId, process);
        }
        catch (Exception ex)
        {
            SetErrorState(packageId, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void HandleInstallExit(string packageId, Process process)
    {
        lock (SyncRoot)
        {
            if (!RunStates.TryGetValue(packageId, out var state) || !ReferenceEquals(state.InstallProcess, process))
                return;

            state.InstallProcess = null;
            try
            {
                var exitCode = process.ExitCode;
                state.Status = exitCode == 0 ? StatusCompleted : StatusError;
                state.Progress = exitCode == 0 ? 1 : 0;
                state.Error = exitCode == 0 ? null : $"Installer exited with code {exitCode}.";
            }
            catch (Exception ex)
            {
                state.Status = StatusError;
                state.Error = $"Failed to read installer exit code: {ex.Message}";
            }
        }

        try { process.Dispose(); } catch { /* best-effort */ }
    }

    private static void UpdateProgress(string packageId, float value)
    {
        lock (SyncRoot)
        {
            if (RunStates.TryGetValue(packageId, out var state) && state.Status == StatusDownloading)
                state.Progress = Math.Clamp(value, 0f, 1f);
        }
    }

    private static void SetErrorState(string packageId, string message)
    {
        lock (SyncRoot)
        {
            if (RunStates.TryGetValue(packageId, out var state))
            {
                state.Status = StatusError;
                state.Progress = 0;
                state.Error = message;
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Driver package operation failed. [packageId={packageId}] {message}");
    }

    private static void ResetToNotStarted(string packageId)
    {
        lock (SyncRoot)
        {
            if (RunStates.TryGetValue(packageId, out var state))
                ResetToNotStartedLocked(state);
        }
    }

    /// <summary>Callers must hold <see cref="SyncRoot"/>.</summary>
    private static void ResetToNotStartedLocked(PackageRunState state)
    {
        state.Status = StatusNotStarted;
        state.Progress = 0;
        state.Error = null;
    }

    /// <summary>Callers must hold <see cref="SyncRoot"/>.</summary>
    private static void StopInstallProcessLocked(PackageRunState state)
    {
        var process = state.InstallProcess;
        if (process is null || process.HasExited)
        {
            state.InstallProcess = null;
            return;
        }

        try { process.Kill(true); } catch (Exception ex) { /* best-effort */ if (Log.Instance.IsTraceEnabled) Log.Instance.Trace($"Failed to kill installer process. [message={ex.Message}]", ex); }
        state.InstallProcess = null;
        try { process.Dispose(); } catch { /* best-effort */ }
    }

    private static object ToPackageDefinition(Package package, PackageRunState? state) => new
    {
        id = package.Id,
        title = package.Title,
        description = package.Description,
        category = package.Category,
        index = package.Index,
        isRecommended = package.IsUpdate,
        isUpdate = package.IsUpdate,
        releaseDate = package.ReleaseDate == DateTime.MinValue ? null : package.ReleaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        version = package.Version,
        fileSize = package.FileSize,
        fileName = package.FileName,
        readmeUrl = package.Readme,
        reboot = ToRebootTypeString(package.Reboot),
        status = state?.Status ?? StatusNotStarted,
        progress = state?.Progress ?? 0,
        error = state?.Error,
        canUninstall = false,
    };

    private static string ToRebootTypeString(RebootType reboot) => reboot switch
    {
        RebootType.Delayed => "Delayed",
        RebootType.Requested => "Requested",
        RebootType.Forced => "Forced",
        RebootType.ForcedPowerOff => "ForcedPowerOff",
        _ => "None",
    };

    private static PackageCacheEntry? GetCachedPackage(string packageId)
    {
        lock (SyncRoot)
            return PackageCache.TryGetValue(packageId, out var entry) ? entry : null;
    }

    private static PackageRunState? GetRunState(string packageId)
    {
        lock (SyncRoot)
            return RunStates.TryGetValue(packageId, out var state) ? state : null;
    }

    /// <summary>Callers must hold <see cref="SyncRoot"/>.</summary>
    private static PackageRunState GetOrCreateRunState(string packageId)
    {
        if (!RunStates.TryGetValue(packageId, out var state))
        {
            state = new PackageRunState();
            RunStates[packageId] = state;
        }

        return state;
    }

    /// <summary>Looks up the downloaded installer on disk; mirrors PackageControlViewModel.FindDownloadedPackagePath.</summary>
    private static string? FindDownloadedFile(Package package, string downloadPath, PackageRunState state)
    {
        if (!string.IsNullOrWhiteSpace(state.DownloadedFilePath) && File.Exists(state.DownloadedFilePath))
            return state.DownloadedFilePath;

        var expectedName = GetActualFileName(package);
        var expectedPath = Path.Combine(downloadPath, expectedName);
        if (File.Exists(expectedPath))
            return expectedPath;

        if (Directory.Exists(downloadPath))
        {
            foreach (var candidate in Directory.EnumerateFiles(downloadPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(candidate), expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    state.DownloadedFilePath = candidate;
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string GetActualFileName(Package package) =>
        $"{SanitizeFileName(package.Title)} - {SanitizeFileName(Path.GetFileName(package.FileName))}";

    private static string? ResolveExpectedInstallerSha256(Package package, PackageRunState state)
    {
        if (InstallerLaunchPathValidator.IsSha256Hex(package.FileCrc))
            return package.FileCrc;

        return InstallerLaunchPathValidator.IsSha256Hex(state.VerifiedSha256)
            ? state.VerifiedSha256
            : null;
    }

    private static bool TryPinVerifiedSha256(
        Package package,
        string filePath,
        out string pinnedSha256,
        out string failureReason)
    {
        pinnedSha256 = string.Empty;
        if (!InstallerLaunchPathValidator.TryComputeSha256Hex(filePath, out var actualSha256, out failureReason))
            return false;

        if (package.FileCrc is { } catalogHash && InstallerLaunchPathValidator.IsSha256Hex(catalogHash))
        {
            if (!string.Equals(actualSha256, catalogHash, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "Installer checksum mismatch.";
                return false;
            }

            pinnedSha256 = catalogHash;
            return true;
        }

        pinnedSha256 = actualSha256;
        return true;
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
    }

    private static string GetEffectiveDownloadPath(PackageDownloaderSettings.PackageDownloaderSettingsStore store)
    {
        if (!string.IsNullOrWhiteSpace(store.DownloadPath) && Directory.Exists(store.DownloadPath))
            return store.DownloadPath;

        return GetDefaultDownloadsPath();
    }

    private static string GetDefaultDownloadsPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");
        return Directory.Exists(downloads) ? downloads : userProfile;
    }

    private static OS? TryGetOs(BridgeRequest request)
    {
        if (!request.Parameters.TryGetProperty("os", out var osProp) || osProp.ValueKind != JsonValueKind.String)
            return null;

        var value = osProp.GetString()?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        if (Enum.TryParse<OS>(value, ignoreCase: true, out var parsed))
            return parsed;

        return value switch
        {
            "Windows 11" => OS.Windows11,
            "Windows 10" => OS.Windows10,
            "Windows 8" => OS.Windows8,
            "Windows 7" => OS.Windows7,
            _ => null,
        };
    }

    private static PackageDownloaderFactory.Type? TryGetSource(BridgeRequest request)
    {
        if (!request.Parameters.TryGetProperty("source", out var sourceProp) || sourceProp.ValueKind != JsonValueKind.String)
            return null;

        return Enum.TryParse<PackageDownloaderFactory.Type>(sourceProp.GetString(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static string GetRequiredString(BridgeRequest request, string property)
    {
        if (!request.Parameters.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new BridgeErrorException(-32602, $"Missing string parameter '{property}'.");
        return prop.GetString()!;
    }

    private static bool GetRequiredBoolean(BridgeRequest request, string property)
    {
        if (!request.Parameters.TryGetProperty(property, out var prop) ||
            prop.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new BridgeErrorException(-32602, $"Missing boolean parameter '{property}'.");
        return prop.GetBoolean();
    }

    private static string[]? TryGetStringArray(BridgeRequest request, string property)
    {
        if (!request.Parameters.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return null;

        var values = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return null;
            values.Add(item.GetString()!);
        }

        return values.ToArray();
    }
}
