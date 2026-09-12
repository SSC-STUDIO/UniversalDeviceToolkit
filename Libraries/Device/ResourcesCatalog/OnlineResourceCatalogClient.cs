using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Serialization;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.ResourcesCatalog;

public sealed class OnlineResourceCatalogClient(HttpClientFactory httpClientFactory)
{
    public const string CatalogUrlEnvironmentVariable = "UDT_RESOURCE_CATALOG_URL";
    private const string JsdelivrCatalogUrl = "https://cdn.jsdelivr.net/gh/SSC-STUDIO/UniversalDeviceToolkit@master/resources/stable/catalog.json";
    private const string RawCatalogUrl = "https://raw.githubusercontent.com/SSC-STUDIO/UniversalDeviceToolkit/master/resources/stable/catalog.json";
    private static readonly HashSet<string> AllowedCatalogHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "ssc-studio.github.io",
        "cdn.jsdelivr.net",
        "github.com",
        "raw.githubusercontent.com",
        "gh-proxy.com",
        "ghfast.top"
    };
    private static readonly TimeSpan CatalogAttemptTimeout = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions JsonOptions = LltJson.CreateCompactOptions();

    private static IEnumerable<string> GetCatalogUrlCandidates()
    {
        var catalogUrl = Environment.GetEnvironmentVariable(CatalogUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(catalogUrl))
        {
            if (!Uri.TryCreate(catalogUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !IsAllowedTestCatalogUri(uri)) ||
                (!IsAllowedCatalogHost(uri.Host) && !IsAllowedTestCatalogUri(uri)))
            {
                catalogUrl = null;
            }
        }

        // An explicit override is authoritative (used by offline smoke tests).
        if (!string.IsNullOrWhiteSpace(catalogUrl))
        {
            yield return catalogUrl;
            yield break;
        }

        yield return AppIdentity.StableResourceCatalogUrl;
        yield return JsdelivrCatalogUrl;
        foreach (var rawCandidate in GitHubDownloadMirrors.WithMirrorFallbacks(RawCatalogUrl))
            yield return rawCandidate;
    }

    private static bool IsAllowedCatalogHost(string host)
    {
#if UDT_TEST_HOOKS
        // Offline tests use a fake HTTPS host with TestHttpClientFactory.
        if (host.Equals("example.test", StringComparison.OrdinalIgnoreCase))
            return true;
#endif
        return AllowedCatalogHosts.Contains(host);
    }

    private static bool IsAllowedTestCatalogUri(Uri uri)
    {
#if UDT_TEST_HOOKS
        // The local mock catalog is intentionally available only to test builds.
        return uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
#else
        return false;
#endif
    }

    public async Task<OnlineResourceCatalog> GetCatalogAsync(CancellationToken token = default)
    {
        Exception? lastError = null;

        foreach (var candidateUrl in GetCatalogUrlCandidates())
        {
            token.ThrowIfCancellationRequested();

            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                attemptCts.CancelAfter(CatalogAttemptTimeout);

                using var httpClient = httpClientFactory.Create();
                var json = await httpClient.GetStringAsync(candidateUrl, attemptCts.Token).ConfigureAwait(false);
                return JsonSerializer.Deserialize<OnlineResourceCatalog>(json, JsonOptions)
                       ?? throw ExceptionHelper.ResourceCatalogEmpty();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    "resource-catalog-candidate",
                    $"Resource catalog candidate failed: {candidateUrl}",
                    ex);
                lastError = ex;
            }
        }

        throw lastError ?? ExceptionHelper.ResourceCatalogEmpty();
    }

    public async Task DownloadAndVerifyAsync(string url, string expectedSha256, string destinationPath, IProgress<float>? progress = null, CancellationToken token = default)
    {
        await DownloadAsync(url, destinationPath, progress, token).ConfigureAwait(false);
        await VerifySha256Async(destinationPath, expectedSha256, token).ConfigureAwait(false);
    }

    public async Task DownloadAsync(string url, string destinationPath, IProgress<float>? progress = null, CancellationToken token = default)
    {
        Exception? lastError = null;

        foreach (var candidateUrl in GitHubDownloadMirrors.WithMirrorFallbacks(url))
        {
            token.ThrowIfCancellationRequested();

            try
            {
                using (var stream = File.Create(destinationPath))
                {
                    using var httpClient = httpClientFactory.Create();
                    await httpClient.DownloadAsync(candidateUrl, stream, progress, token).ConfigureAwait(false);
                }

                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                TryDelete(destinationPath);
                throw;
            }
            catch (Exception ex)
            {
                Log.Instance.TraceOnce(
                    "resource-download-candidate",
                    $"Resource download candidate failed: {candidateUrl}",
                    ex);
                lastError = ex;
                TryDelete(destinationPath);
            }
        }

        throw lastError ?? ExceptionHelper.ResourceCatalogEmpty();
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* ignore cleanup failure */ }
    }

    public async Task VerifySha256Async(string path, string expectedSha256, CancellationToken token = default)
    {
        var actualSha256 = await ComputeSha256Async(path, token).ConfigureAwait(false);
        if (!actualSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(path); }
            catch { /* ignore cleanup failure */ }

            throw ExceptionHelper.SHA256ValidationFailed(expectedSha256, actualSha256);
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, token).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
