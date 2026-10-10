using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class HttpClientExtensions
{
    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;

        await using var download = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (progress is null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            progress.Report(0);
            var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
            await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken).ConfigureAwait(false);
            progress.Report(1);
        }

        // A proxy/server that closes the connection mid-body surfaces as a clean end
        // of stream; treat a short body as an IO failure so callers retry or fail over.
        if (contentLength.HasValue && destination.CanSeek && destination.Length < contentLength.Value)
            throw new IOException($"Incomplete download: {destination.Length}/{contentLength.Value} bytes received.");
    }
}
