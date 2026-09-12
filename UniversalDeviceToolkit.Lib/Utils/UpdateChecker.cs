using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Settings;
using NeoSmart.AsyncLock;
using Octokit;
using Octokit.Internal;

namespace UniversalDeviceToolkit.Lib.Utils;

public class UpdateChecker
{
    private readonly HttpClientFactory _httpClientFactory;
    private readonly UpdateCheckSettings _updateCheckSettings = IoCContainer.Resolve<UpdateCheckSettings>();
    private readonly AsyncLock _updateSemaphore = new();
    private readonly object _cacheLock = new();
    private Version? _cachedLatestVersion;
    private DateTime _cachedLatestVersionTime = DateTime.MinValue;
    private const int VersionCacheDurationMinutes = 5;

    private static readonly HashSet<string> TrustedRepositoryOwners = new(StringComparer.OrdinalIgnoreCase)
    {
        "SSC-STUDIO",
        "LenovoLegionToolkit"
    };

    private DateTime _lastUpdate;
    private TimeSpan _minimumTimeSpanForRefresh;
    private Update[] _updates = [];

    public bool Disable { get; set; }
    public string? DisableReason { get; set; }
    public UpdateCheckStatus Status { get; set; }

    public UpdateChecker(HttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        UpdateMinimumTimeSpanForRefresh();
        _lastUpdate = _updateCheckSettings.Store.LastUpdateCheckDateTime ?? DateTime.MinValue;
    }

    internal static Release[] FilterPublicApplicationReleases(
        IEnumerable<Release> releases,
        bool includePrerelease)
    {
        ArgumentNullException.ThrowIfNull(releases);

        return releases
            .Where(r => ReleaseTagPolicy.IsPublicApplicationRelease(
                r.TagName,
                r.Draft,
                r.Prerelease,
                includePrerelease))
            .ToArray();
    }

    public async Task<Version?> CheckAsync(bool forceCheck)
    {
        using (await _updateSemaphore.LockAsync().ConfigureAwait(false))
        {
            if (Disable)
            {
                _lastUpdate = DateTime.UtcNow;
                _updates = [];
                return null;
            }

            try
            {
                var timeSpanSinceLastUpdate = DateTime.UtcNow - _lastUpdate;
                var shouldCheck = timeSpanSinceLastUpdate > _minimumTimeSpanForRefresh;

                if (!forceCheck && !shouldCheck)
                {
                    lock (_cacheLock)
                    {
                        if (_cachedLatestVersion != null && 
                            DateTime.UtcNow - _cachedLatestVersionTime < TimeSpan.FromMinutes(VersionCacheDurationMinutes))
                        {
                            return _cachedLatestVersion;
                        }
                    }
                    return _updates.Length != 0 ? _updates.First().Version : null;
                }

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Checking...");

                using var handler = _httpClientFactory.CreateHandler();
                var adapter = new HttpClientAdapter(() => handler);
                var productInformation = new ProductHeaderValue($"{AppIdentity.CompactName}-UpdateChecker");
                var connection = new Connection(productInformation, adapter);
                var githubClient = new GitHubClient(connection);
                
                // Get update repository from settings, fallback to default if not configured
#if DEBUG
                var repositoryOwner = !string.IsNullOrWhiteSpace(_updateCheckSettings.Store.UpdateRepositoryOwner) 
                    ? _updateCheckSettings.Store.UpdateRepositoryOwner 
                    : AppIdentity.RepositoryOwner;
                var repositoryName = !string.IsNullOrWhiteSpace(_updateCheckSettings.Store.UpdateRepositoryName) 
                    ? _updateCheckSettings.Store.UpdateRepositoryName 
                    : AppIdentity.RepositoryName;
#else
                // Release builds: always use hardcoded trusted repository to prevent supply-chain attacks
                var repositoryOwner = AppIdentity.RepositoryOwner;
                var repositoryName = AppIdentity.RepositoryName;
#endif

                // Validate repository owner against trusted allowlist
                if (!TrustedRepositoryOwners.Contains(repositoryOwner))
                {
                    var warning = $"Untrusted repository owner: {repositoryOwner}/{repositoryName}. Allowed: {string.Join(", ", TrustedRepositoryOwners)}";
                    Log.Instance.Warning(warning);
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace(warning);
                    Status = UpdateCheckStatus.Error;
                    return null;
                }

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Checking updates from repository: {repositoryOwner}/{repositoryName}");
                
                var releases = await githubClient.Repository.Release.GetAll(repositoryOwner, repositoryName, new ApiOptions { PageSize = 10 }).ConfigureAwait(false);

                var thisReleaseVersion = Assembly.GetEntryAssembly()?.GetName().Version;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Current version: {thisReleaseVersion}, Found releases: {releases.Count}");

                var includePrerelease = _updateCheckSettings.Store.IncludePrereleaseUpdates;
                var publicReleases = FilterPublicApplicationReleases(releases, includePrerelease);
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Public releases (non-draft{(includePrerelease ? ", incl. prereleases" : ", stable only")}): {publicReleases.Length}");

                var updates = publicReleases
                    .Select(r =>
                    {
                        try
                        {
                            var update = new Update(r);
                            // Normalize versions to 3 components for comparison to avoid issues with build/revision numbers
                            var localVersion = thisReleaseVersion != null 
                                ? new Version(thisReleaseVersion.Major, thisReleaseVersion.Minor, thisReleaseVersion.Build) 
                                : new Version(0, 0, 0);
                            
                            var updateVersion = new Version(update.Version.Major, update.Version.Minor, Math.Max(0, update.Version.Build));

                            var isNewer = updateVersion > localVersion;

                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Release {update.Version} (tag: {r.TagName}, prerelease: {update.IsPrerelease}) is {(isNewer ? "newer" : "not newer")} than current version {thisReleaseVersion}");
                            return (Update: (Update?)update, IsNewer: isNewer);
                        }
                        catch (Exception ex)
                        {
                            // Skip releases with invalid version tags (e.g., "v3" without minor/patch)
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Skipping release with invalid version tag: {r.TagName}", ex);
                            return (Update: (Update?)null, IsNewer: false);
                        }
                    })
                    .Where(r => r.Update.HasValue && r.IsNewer)
                    .Select(r => r.Update!.Value)
                    .OrderByDescending(r => r.Version)
                    .ToArray();

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Checked [updates.Length={updates.Length}]");

                _updates = updates;
                Status = UpdateCheckStatus.Success;

                lock (_cacheLock)
                {
                    _cachedLatestVersion = _updates.Length != 0 ? _updates.First().Version : null;
                    _cachedLatestVersionTime = DateTime.UtcNow;
                }

                return _cachedLatestVersion;
            }
            catch (RateLimitExceededException ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Reached API Rate Limitation.", ex);

                Status = UpdateCheckStatus.RateLimitReached;
                return null;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error checking for updates.", ex);

                Status = UpdateCheckStatus.Error;
                return null;
            }
            finally
            {
                _lastUpdate = DateTime.UtcNow;
                _updateCheckSettings.Store.LastUpdateCheckDateTime = _lastUpdate;
                _updateCheckSettings.SynchronizeStore();
            }
        }
    }

    public async Task<Update[]> GetUpdatesAsync()
    {
        using (await _updateSemaphore.LockAsync().ConfigureAwait(false))
            return _updates;
    }

    public async Task<string> DownloadLatestUpdateAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using (await _updateSemaphore.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            var latestUpdate = _updates.OrderByDescending(u => u.Version).FirstOrDefault();

            if (latestUpdate.Equals(default))
                throw ExceptionHelper.NoUpdatesAvailable();

            if (latestUpdate.Url is null)
                throw ExceptionHelper.SetupFileUrlNotFound();

            var fileId = $"{AppIdentity.CompactName}Setup_{Guid.NewGuid():N}";
            var finalPath = Path.Combine(Folders.Temp, $"{fileId}.exe");
            var partialPath = Path.Combine(Folders.Temp, $"{fileId}.exe.partial");

            try
            {
                using var httpClient = _httpClientFactory.Create();

                // Write the payload to a sibling .partial file, then rename after SHA256.
                using (var fileStream = File.OpenWrite(partialPath))
                    await httpClient.DownloadAsync(latestUpdate.Url, fileStream, progress, cancellationToken).ConfigureAwait(false);

                await ValidateUpdatePackageAsync(partialPath, latestUpdate, httpClient, cancellationToken).ConfigureAwait(false);

                File.Move(partialPath, finalPath);
                return finalPath;
            }
            catch
            {
                TryDeleteFile(partialPath);
                TryDeleteFile(finalPath);
                throw;
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup of a rejected partial or final installer.
        }
    }

    /// <summary>
    /// Validate the integrity of the downloaded update package using SHA256 hash
    /// </summary>
    private static async Task ValidateUpdatePackageAsync(string filePath, Update update, HttpClient httpClient, CancellationToken cancellationToken, bool isIntegrityCheckRequired = true)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Validating update package integrity for {filePath}");

        // Compute SHA256 hash of downloaded file and release the handle before any delete attempt.
        string computedHash;
        using (var fileStream = File.OpenRead(filePath))
        {
            using var sha256 = global::System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken).ConfigureAwait(false);
            computedHash = global::System.Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        var expectedHash = await ResolveExpectedHashAsync(update, httpClient, cancellationToken).ConfigureAwait(false);

        // If no expected hash is available, enforce integrity check unless explicitly waived
        if (string.IsNullOrEmpty(expectedHash))
        {
            if (isIntegrityCheckRequired)
            {
#if !DEBUG
                var missingHashError = $"Update package integrity check failed: no SHA256 hash available for update {update.Version}. Integrity check is required for security.";
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace(missingHashError);

                // Delete the unverifiable file
                try
                {
                    File.Delete(filePath);
                }
                catch { /* Ignore deletion errors */ }

                throw ExceptionHelper.UpdateNoSHA256Hash(update.Version.ToString());
#endif
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"No SHA256 hash available for validation. Skipping integrity check.");
            Log.Instance.Warning($"Update package integrity verification skipped: no SHA256 hash available for update {update.Version}");
            return;
        }

        // Compare hashes
        if (!computedHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Update package integrity check failed. Expected SHA256: {expectedHash}, Computed: {computedHash}");

            // Delete the corrupted file
            try
            {
                File.Delete(filePath);
            }
            catch { /* Ignore deletion errors */ }

            throw ExceptionHelper.UpdateSHA256Mismatch(expectedHash, computedHash);
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Update package integrity check passed. SHA256: {computedHash}");
    }

    private static async Task<string?> ResolveExpectedHashAsync(Update update, HttpClient httpClient, CancellationToken cancellationToken)
    {
        var packageFileNames = GetPackageFileNameCandidates(update.Url);

        if (!string.IsNullOrWhiteSpace(update.Sha256Url))
        {
            var assetHash = await TryGetExpectedHashFromUrlAsync(update.Sha256Url, packageFileNames, httpClient, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(assetHash))
                return assetHash;
        }

        var inlineHash = NormalizeSha256Hash(update.Sha256Hash);
        if (!string.IsNullOrWhiteSpace(inlineHash))
            return inlineHash;

        if (IsHashSourceUrl(update.Sha256Hash))
        {
            var legacyHash = await TryGetExpectedHashFromUrlAsync(update.Sha256Hash!, packageFileNames, httpClient, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(legacyHash))
                return legacyHash;
        }

        return null;
    }

    private static async Task<string?> TryGetExpectedHashFromUrlAsync(
        string hashUrl,
        IReadOnlyCollection<string> packageFileNames,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var hashContent = await httpClient.GetStringAsync(hashUrl, cancellationToken).ConfigureAwait(false);
            var expectedHash = TryExtractExpectedHash(hashContent, packageFileNames);

            if (!string.IsNullOrWhiteSpace(expectedHash))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Fetched expected SHA256 from hash source: {expectedHash}");

                return expectedHash;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Fetched SHA256 source but could not resolve a matching hash. [url={hashUrl}]");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to fetch SHA256 from URL: {hashUrl}", ex);
        }

        return null;
    }

    private static string? TryExtractExpectedHash(string? hashContent, IReadOnlyCollection<string> packageFileNames)
    {
        if (string.IsNullOrWhiteSpace(hashContent))
            return null;

        var lines = hashContent
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (packageFileNames.Count != 0)
        {
            // Prefer longer names first so "Foo_Setup.exe" wins over a shorter substring.
            foreach (var fileName in packageFileNames.OrderByDescending(n => n.Length))
            {
                foreach (var line in lines)
                {
                    if (!LineReferencesFileName(line, fileName))
                        continue;

                    var lineHash = TryExtractFirstSha256Hash(line);
                    if (lineHash is not null)
                        return lineHash;
                }
            }
        }

        foreach (var line in lines)
        {
            var lineHash = TryExtractFirstSha256Hash(line);
            if (lineHash is not null &&
                (line.Contains("sha256", StringComparison.OrdinalIgnoreCase) || lines.Length == 1))
                return lineHash;
        }

        var allHashes = ExtractAllSha256Hashes(hashContent);
        return allHashes.Count == 1 ? allHashes[0] : null;
    }

    private static string? NormalizeSha256Hash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var allHashes = ExtractAllSha256Hashes(value);
        return allHashes.Count == 1 ? allHashes[0] : null;
    }

    private static string? TryExtractFirstSha256Hash(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Regex.Match(text, @"(?<![a-fA-F0-9])([a-fA-F0-9]{64})(?![a-fA-F0-9])", RegexOptions.IgnoreCase);
        return match.Success
            ? match.Groups[1].Value.ToLowerInvariant()
            : null;
    }

    private static List<string> ExtractAllSha256Hashes(string text)
    {
        var hashes = new List<string>();

        foreach (Match match in Regex.Matches(text, @"(?<![a-fA-F0-9])([a-fA-F0-9]{64})(?![a-fA-F0-9])", RegexOptions.IgnoreCase))
        {
            var hash = match.Groups[1].Value.ToLowerInvariant();
            if (!hashes.Contains(hash, StringComparer.OrdinalIgnoreCase))
                hashes.Add(hash);
        }

        return hashes;
    }

    private static bool IsHashSourceUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string[] GetPackageFileNameCandidates(string? packageUrl)
    {
        if (string.IsNullOrWhiteSpace(packageUrl))
            return [];

        string? fileName = null;
        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri))
            fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));

        fileName ??= Path.GetFileName(packageUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return [];

        // Only the full package file name. Adding a bare "setup.exe" candidate caused
        // multi-asset SHA256 lists to match the wrong line via substring Contains().
        return [fileName];
    }

    /// <summary>
    /// True when <paramref name="line"/> references <paramref name="fileName"/> as a whole token
    /// (not a shorter substring of another asset name).
    /// </summary>
    private static bool LineReferencesFileName(string line, string fileName)
    {
        if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(fileName))
            return false;

        var index = line.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        if (index > 0)
        {
            var before = line[index - 1];
            if (!char.IsWhiteSpace(before) && before is not ('(' or '/' or '\\' or '*' or '=' or '"' or '\''))
                return false;
        }

        var afterIndex = index + fileName.Length;
        if (afterIndex < line.Length)
        {
            var after = line[afterIndex];
            if (!char.IsWhiteSpace(after) && after is not (')' or '"' or '\'' or ',' or ';'))
                return false;
        }

        return true;
    }

    public void UpdateMinimumTimeSpanForRefresh() => _minimumTimeSpanForRefresh = _updateCheckSettings.Store.UpdateCheckFrequency switch
    {
        UpdateCheckFrequency.PerHour => TimeSpan.FromHours(1),
        UpdateCheckFrequency.PerThreeHours => TimeSpan.FromHours(3),
        UpdateCheckFrequency.PerTwelveHours => TimeSpan.FromHours(12),
        UpdateCheckFrequency.PerDay => TimeSpan.FromDays(1),
        UpdateCheckFrequency.PerWeek => TimeSpan.FromDays(7),
        UpdateCheckFrequency.PerMonth => TimeSpan.FromDays(30),
        _ => throw new ArgumentException(nameof(_updateCheckSettings.Store.UpdateCheckFrequency))
    };
}
