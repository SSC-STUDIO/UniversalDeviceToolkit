using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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
public sealed class DevicePackManagerTests : IDisposable
{
    private readonly string _appDataOverride;
    private readonly string? _previousAppDataOverride;
    private readonly string? _previousCatalogUrl;

    public DevicePackManagerTests()
    {
        _previousAppDataOverride = Environment.GetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable);
        _previousCatalogUrl = Environment.GetEnvironmentVariable(OnlineResourceCatalogClient.CatalogUrlEnvironmentVariable);
        _appDataOverride = Path.Combine(Path.GetTempPath(), $"udt-device-pack-tests-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable(Folders.AppDataOverrideEnvironmentVariable, _appDataOverride);
        Environment.SetEnvironmentVariable(OnlineResourceCatalogClient.CatalogUrlEnvironmentVariable, "https://example.test/catalog.json");
    }

    [Fact]
    public async Task InstallAsync_WithValidJsonPack_ShouldInstallManifest()
    {
        // Arrange
        var manifestJson = """
                           {
                             "id": "lenovo-legion-pro-7",
                             "displayName": "Lenovo Legion Pro 7",
                             "vendor": "LENOVO",
                             "families": ["Legion"],
                             "modelPrefixes": ["16IRX"],
                             "modelKeywords": ["Legion Pro 7"],
                             "machineTypes": ["83DE"],
                             "enabledFeatures": ["lenovo-hardware-controls"],
                             "hiddenFeatures": []
                           }
                           """;
        var zip = CreateZip(("device-pack.json", manifestJson));
        var manager = CreateManager(zip);

        // Act
        var pack = await manager.InstallAsync("lenovo-legion-pro-7");

        // Assert
        pack.Id.Should().Be("lenovo-legion-pro-7");
        manager.IsInstalled("lenovo-legion-pro-7").Should().BeTrue();
        File.Exists(Path.Combine(_appDataOverride, "device-packs", "lenovo-legion-pro-7", "device-pack.json")).Should().BeTrue();
    }

    [Fact]
    public async Task InstallAsync_WithExecutableContent_ShouldRejectPack()
    {
        // Arrange
        var zip = CreateZip(("device-pack.json", "{\"id\":\"lenovo-legion-pro-7\",\"displayName\":\"Lenovo Legion Pro 7\",\"vendor\":\"LENOVO\"}"),
            ("payload.exe", "not allowed"));
        var manager = CreateManager(zip);

        // Act
        var action = () => manager.InstallAsync("lenovo-legion-pro-7");

        // Assert
        await action.Should().ThrowAsync<InvalidDataException>();
        manager.IsInstalled("lenovo-legion-pro-7").Should().BeFalse();
    }

    [Fact]
    public async Task GetInstalledCatalog_WithInstalledPack_ShouldReturnPack()
    {
        // Arrange
        var manifestJson = """
                           {
                             "id": "lenovo-legion-pro-7",
                             "displayName": "Lenovo Legion Pro 7",
                             "vendor": "LENOVO",
                             "families": ["Legion"],
                             "machineTypes": ["83DE"],
                             "enabledFeatures": ["lenovo-hardware-controls"]
                           }
                           """;
        var zip = CreateZip(("device-pack.json", manifestJson));
        var manager = CreateManager(zip);
        await manager.InstallAsync("lenovo-legion-pro-7");

        // Act
        var catalog = manager.GetInstalledCatalog();

        // Assert
        catalog.DevicePacks.Should().ContainSingle(pack =>
            pack.Id == "lenovo-legion-pro-7" &&
            pack.DisplayName == "Lenovo Legion Pro 7" &&
            pack.MachineTypes.Contains("83DE"));
    }

    [Fact]
    public async Task GetInstalledCatalog_ShouldIgnoreBackupAndPendingDirectories()
    {
        var manifestJson = """
                           {
                             "id": "lenovo-legion-pro-7",
                             "displayName": "Lenovo Legion Pro 7",
                             "vendor": "LENOVO",
                             "machineTypes": ["83DE"]
                           }
                           """;
        var zip = CreateZip(("device-pack.json", manifestJson));
        var manager = CreateManager(zip);
        await manager.InstallAsync("lenovo-legion-pro-7");

        var backup = Path.Combine(_appDataOverride, "device-packs", "lenovo-legion-pro-7.backup");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "device-pack.json"), """
            { "id": "lenovo-legion-pro-7", "displayName": "Backup", "vendor": "LENOVO" }
            """);

        var catalog = manager.GetInstalledCatalog();

        catalog.DevicePacks.Should().ContainSingle(pack => pack.Id == "lenovo-legion-pro-7");
        catalog.DevicePacks.Should().NotContain(pack => pack.DisplayName == "Backup");
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
