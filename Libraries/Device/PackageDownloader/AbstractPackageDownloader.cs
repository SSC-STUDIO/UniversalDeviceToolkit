using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.PackageDownloader;

public abstract class AbstractPackageDownloader(HttpClientFactory httpClientFactory) : IPackageDownloader
{
    protected HttpClientFactory HttpClientFactory => httpClientFactory;

    public abstract Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default);

    public async Task<string> DownloadPackageFileAsync(Package package, string location, IProgress<float>? progress = null, CancellationToken token = default)
    {
        if (!PackageDownloadSecurity.IsAllowedPackageDownloadUrl(package.FileLocation))
            throw new InvalidOperationException("Package download URL is not allowed.");

        var finalPath = PackageDownloadSecurity.CreateSafePackageFilePath(location, package.Title, package.FileName);
        if (finalPath is null)
            throw ExceptionHelper.InvalidFileName(nameof(location));

        using var httpClient = httpClientFactory.Create();

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var moved = false;

        try
        {
            await DownloadToFileAsync(httpClient, package.FileLocation, tempPath, progress, token).ConfigureAwait(false);
            await ValidateCatalogChecksumAsync(package, tempPath, token).ConfigureAwait(false);

            File.Move(tempPath, finalPath, true);
            moved = true;

            if (!PathSecurity.IsPathWithinAllowedDirectory(finalPath, Path.GetFullPath(location)))
            {
                try { File.Delete(finalPath); } catch (IOException) { }
                throw ExceptionHelper.InvalidFileName(nameof(location));
            }

            return finalPath;
        }
        finally
        {
            if (!moved && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch (IOException) { }
            }
        }
    }

    private static async Task DownloadToFileAsync(HttpClient httpClient, string url, string tempPath, IProgress<float>? progress, CancellationToken token)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is null || !PackageDownloadSecurity.IsAllowedPackageDownloadUrl(finalUri.ToString()))
            throw new InvalidOperationException("Package download redirected to a disallowed URL.");

        await using var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var download = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

        var contentLength = response.Content.Headers.ContentLength;
        if (progress is null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, token).ConfigureAwait(false);
        }
        else
        {
            progress.Report(0);
            var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
            await download.CopyToAsync(destination, 81920, relativeProgress, token).ConfigureAwait(false);
            progress.Report(1);
        }

        if (contentLength.HasValue && destination.CanSeek && destination.Length < contentLength.Value)
            throw new IOException($"Incomplete download: {destination.Length}/{contentLength.Value} bytes received.");
    }

    private static async Task ValidateCatalogChecksumAsync(Package package, string tempPath, CancellationToken token)
    {
        if (!PackageDownloadSecurity.TryParseSha256Hex(package.FileCrc, out _))
            throw ExceptionHelper.FileChecksumMismatch();

        using var fileStream = File.OpenRead(tempPath);
        using var managedSha256 = SHA256.Create();

        var fileSha256Bytes = await managedSha256.ComputeHashAsync(fileStream, token).ConfigureAwait(false);
        var fileSha256 = Convert.ToHexString(fileSha256Bytes);

        if (PackageDownloadSecurity.Sha256Equals(fileSha256, package.FileCrc!))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Package file checksum match. [fileName={package.FileName}, fileLocation={package.FileLocation}]");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Catalog checksum mismatch. [fileName={package.FileName}, fileLocation={package.FileLocation}]");

        throw ExceptionHelper.FileChecksumMismatch();
    }
}
