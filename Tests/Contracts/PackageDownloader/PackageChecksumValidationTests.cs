using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.PackageDownloader;
using Xunit;

namespace UniversalDeviceToolkit.Tests.PackageDownloader;

[Trait("Category", TestCategories.Security)]
public sealed class PackageChecksumValidationTests
{
    [Fact]
    public async Task ValidateCatalogChecksum_WhenCatalogHashMatches_ShouldAcceptDownload()
    {
        var packageBytes = "package-payload"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(packageBytes));

        var package = new Package
        {
            Title = "Test",
            FileName = "package.exe",
            FileLocation = "https://download.lenovo.com/package.exe",
            FileCrc = hash
        };

        await InvokeValidateAsync(package, packageBytes).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateCatalogChecksum_WhenCatalogHashMissing_ShouldRejectEvenIfSidecarWouldMatch()
    {
        var packageBytes = "package-payload"u8.ToArray();

        var package = new Package
        {
            Title = "Test",
            FileName = "package.exe",
            FileLocation = "https://download.lenovo.com/package.exe",
            FileCrc = string.Empty
        };

        await InvokeValidateAsync(package, packageBytes).Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ValidateCatalogChecksum_WhenCatalogHashMismatches_ShouldReject()
    {
        var packageBytes = "package-payload"u8.ToArray();
        var wrongCatalogHash = new string('0', 64);

        var package = new Package
        {
            Title = "Test",
            FileName = "package.exe",
            FileLocation = "https://download.lenovo.com/package.exe",
            FileCrc = wrongCatalogHash
        };

        await InvokeValidateAsync(package, packageBytes).Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ValidateCatalogChecksum_WhenCatalogHashIsNotSha256_ShouldReject()
    {
        var packageBytes = "package-payload"u8.ToArray();

        var package = new Package
        {
            Title = "Test",
            FileName = "package.exe",
            FileLocation = "https://download.lenovo.com/package.exe",
            FileCrc = "ABCD1234"
        };

        await InvokeValidateAsync(package, packageBytes).Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DownloadPackageFileAsync_WithDisallowedHost_ShouldRejectBeforeWrite()
    {
        var location = Path.Combine(Path.GetTempPath(), $"udt-dl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(location);

        try
        {
            var downloader = new TestPackageDownloader(new StaticResponseFactory());
            var package = new Package
            {
                Title = "Test",
                FileName = "package.exe",
                FileLocation = "https://evil.example/package.exe",
                FileCrc = new string('0', 64)
            };

            var act = async () => await downloader.DownloadPackageFileAsync(package, location);
            await act.Should().ThrowAsync<InvalidOperationException>();
            Directory.GetFiles(location).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(location))
                Directory.Delete(location, true);
        }
    }

    [Fact]
    public async Task DownloadPackageFileAsync_WithTraversalFileName_ShouldStayInsideLocation()
    {
        var packageBytes = "package-payload"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(packageBytes));
        var location = Path.Combine(Path.GetTempPath(), $"udt-dl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(location);

        try
        {
            var downloader = new TestPackageDownloader(new StaticResponseFactory(packageBytes));
            var package = new Package
            {
                Title = "Test",
                FileName = @"..\..\windows\system32\evil.exe",
                FileLocation = "https://download.lenovo.com/package.exe",
                FileCrc = hash
            };

            var finalPath = await downloader.DownloadPackageFileAsync(package, location);
            Path.GetFullPath(finalPath).Should().StartWith(Path.GetFullPath(location));
            File.Exists(finalPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(location))
                Directory.Delete(location, true);
        }
    }

    private static Func<Task> InvokeValidateAsync(Package package, byte[] packageBytes)
    {
        return async () =>
        {
            var method = typeof(AbstractPackageDownloader).GetMethod(
                "ValidateCatalogChecksumAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var tempPath = Path.Combine(Path.GetTempPath(), $"udt-checksum-{Guid.NewGuid():N}.bin");
            await File.WriteAllBytesAsync(tempPath, packageBytes);
            try
            {
                await (Task)method!.Invoke(null, [package, tempPath, CancellationToken.None])!;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        };
    }

    private sealed class TestPackageDownloader(HttpClientFactory factory) : AbstractPackageDownloader(factory)
    {
        public override Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default)
            => Task.FromResult(new List<Package>());
    }

    private sealed class StaticResponseFactory(byte[]? payload = null) : HttpClientFactory
    {
        public override HttpClient Create() => new(new StaticResponseHandler(payload ?? []), disposeHandler: true);
    }

    private sealed class StaticResponseHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            if (uri.StartsWith("https://download.lenovo.com/", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload),
                    RequestMessage = request
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request
            });
        }
    }
}
