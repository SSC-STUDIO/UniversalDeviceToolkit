using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Lib.ResourcesCatalog;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public sealed class DevicePackManagerTransactionTests : IDisposable
{
    private readonly string _appDataOverride;
    private readonly string? _previousAppDataOverride;
    private readonly string? _previousCatalogUrl;

    public DevicePackManagerTransactionTests()
    {
        _previousAppDataOverride = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        _previousCatalogUrl = Environment.GetEnvironmentVariable(OnlineResourceCatalogClient.CatalogUrlEnvironmentVariable);
        _appDataOverride = Path.Combine(Path.GetTempPath(), $"udt-device-pack-txn-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _appDataOverride);
        Environment.SetEnvironmentVariable(OnlineResourceCatalogClient.CatalogUrlEnvironmentVariable, "https://example.test/catalog.json");
    }

    [Fact]
    public async Task InstallAsync_WhenCalledTwice_ShouldKeepPackInstalled()
    {
        var manifestJson = """
                           {
                             "id": "lenovo-legion-pro-7",
                             "displayName": "Lenovo Legion Pro 7",
                             "vendor": "LENOVO"
                           }
                           """;
        var zip = CreateZip(("device-pack.json", manifestJson));
        var manager = CreateManager(zip);

        await manager.InstallAsync("lenovo-legion-pro-7");
        manager.IsInstalled("lenovo-legion-pro-7").Should().BeTrue();

        await manager.InstallAsync("lenovo-legion-pro-7");
        manager.IsInstalled("lenovo-legion-pro-7").Should().BeTrue();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _previousAppDataOverride);
        Environment.SetEnvironmentVariable(OnlineResourceCatalogClient.CatalogUrlEnvironmentVariable, _previousCatalogUrl);

        try
        {
            if (Directory.Exists(_appDataOverride))
                Directory.Delete(_appDataOverride, true);
        }
        catch
        {
            // best-effort test cleanup
        }
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static DevicePackManager CreateManager(byte[] zip)
    {
        var sha256 = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        var catalog = new
        {
            schemaVersion = 1,
            appVersion = "3.8.0",
            languages = Array.Empty<object>(),
            devicePacks = new[]
            {
                new
                {
                    id = "lenovo-legion-pro-7",
                    displayName = "Lenovo Legion Pro 7",
                    vendor = "LENOVO",
                    families = new[] { "Legion" },
                    modelPrefixes = new[] { "16IRX" },
                    modelKeywords = new[] { "Legion Pro 7" },
                    machineTypes = new[] { "83DE" },
                    url = "https://example.test/lenovo-legion-pro-7.zip",
                    sha256,
                    size = zip.LongLength
                }
            }
        };

        var responses = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://example.test/catalog.json"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(catalog)),
            ["https://example.test/lenovo-legion-pro-7.zip"] = zip
        };

        return new DevicePackManager(new OnlineResourceCatalogClient(new TestHttpClientFactory(responses)));
    }

    private sealed class TestHttpClientFactory(IReadOnlyDictionary<string, byte[]> responses) : HttpClientFactory
    {
        public override HttpClient Create() => new(new TestHandler(responses), true);
    }

    private sealed class TestHandler(IReadOnlyDictionary<string, byte[]> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.RequestUri?.ToString() ?? string.Empty;
            if (!responses.TryGetValue(key, out var response))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(response)
            });
        }
    }
}
