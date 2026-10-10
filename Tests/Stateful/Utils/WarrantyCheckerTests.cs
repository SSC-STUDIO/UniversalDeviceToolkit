using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Tests.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.Settings)]
public class WarrantyCheckerTests : IDisposable
{
    private readonly ApplicationSettings _settings;
    private readonly WarrantyChecker _warrantyChecker;

    public WarrantyCheckerTests()
    {
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.Application);
        _settings = new ApplicationSettings();
        _warrantyChecker = new WarrantyChecker(_settings, new StubHttpClientFactory());
    }

    public void Dispose()
    {
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.Application);
    }

    #region GetWarrantyInfo Tests

    [Fact]
    public async Task GetWarrantyInfo_WhenCachedAndNotForceRefresh_ShouldReturnCachedValue()
    {
        // Arrange
        var cachedWarrantyInfo = new WarrantyInfo(
            new DateTime(2023, 1, 1),
            new DateTime(2025, 12, 31),
            new Uri("https://pcsupport.lenovo.com/products/LNV123")
        );
        _settings.Store.WarrantyInfo = cachedWarrantyInfo;

        var machineInfo = CreateMachineInformation("LNV123", "Type123");

        // Act
        var result = await _warrantyChecker.GetWarrantyInfo(machineInfo, forceRefresh: false);

        // Assert
        result.Should().Be(cachedWarrantyInfo);
    }

    [Fact]
    public async Task GetWarrantyInfo_WhenForceRefresh_ShouldCallApiEvenIfCached()
    {
        // Arrange
        var cachedWarrantyInfo = new WarrantyInfo(
            new DateTime(2023, 1, 1),
            new DateTime(2025, 12, 31),
            new Uri("https://pcsupport.lenovo.com/products/LNV123")
        );
        _settings.Store.WarrantyInfo = cachedWarrantyInfo;

        var newWarrantyInfo = new WarrantyInfo(
            new DateTime(2024, 1, 1),
            new DateTime(2026, 12, 31),
            new Uri("https://pcsupport.lenovo.com/products/LNV456")
        );

        var factory = new StubHttpClientFactory(CreateHttpClient(newWarrantyInfo));
        var checker = new WarrantyChecker(_settings, factory);

        var machineInfo = CreateMachineInformation("LNV456", "Type456");

        // Act
        var result = await checker.GetWarrantyInfo(machineInfo, forceRefresh: true);

        // Assert
        result.Should().NotBe(cachedWarrantyInfo);
        result.Should().Be(newWarrantyInfo);
        _settings.Store.WarrantyInfo.Should().Be(newWarrantyInfo);
    }

    [Fact]
    public async Task GetWarrantyInfo_WhenNoCache_ShouldCallApiAndSaveToSettings()
    {
        // Arrange
        _settings.Store.WarrantyInfo = null;

        var warrantyInfo = new WarrantyInfo(
            new DateTime(2023, 1, 1),
            new DateTime(2025, 12, 31),
            new Uri("https://pcsupport.lenovo.com/products/LNV123")
        );

        var factory = new StubHttpClientFactory(CreateHttpClient(warrantyInfo));
        var checker = new WarrantyChecker(_settings, factory);

        var machineInfo = CreateMachineInformation("LNV123", "Type123");

        // Act
        var result = await checker.GetWarrantyInfo(machineInfo, forceRefresh: false);

        // Assert
        result.Should().Be(warrantyInfo);
        _settings.Store.WarrantyInfo.Should().Be(warrantyInfo);
    }

    [Fact]
    public async Task GetWarrantyInfo_WhenApiReturnsInvalidCode_ShouldReturnNull()
    {
        // Arrange
        _settings.Store.WarrantyInfo = null;
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"code":1}""")
        });
        var checker = new WarrantyChecker(_settings, new StubHttpClientFactory(new HttpClient(handler)));

        var machineInfo = CreateMachineInformation("LNV123", "Type123");

        // Act
        var result = await checker.GetWarrantyInfo(machineInfo, forceRefresh: false);

        // Assert
        result.Should().BeNull();
        _settings.Store.WarrantyInfo.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static MachineInformation CreateMachineInformation(string serialNumber, string machineType) => new()
    {
        SerialNumber = serialNumber,
        MachineType = machineType
    };

    private static HttpClient CreateHttpClient(WarrantyInfo warrantyInfo)
    {
        var warrantyApiResponse = new
        {
            code = 0,
            data = new
            {
                baseWarranties = new[]
                {
                    new
                    {
                        startDate = warrantyInfo.Start?.ToString("yyyy-MM-dd"),
                        endDate = warrantyInfo.End?.ToString("yyyy-MM-dd")
                    }
                },
                upgradeWarranties = Array.Empty<object>()
            }
        };

        var productId = warrantyInfo.Link?.Segments[^1];
        var productApiResponse = $$"""
        [
          { "Id": "{{productId}}" }
        ]
        """;

        var handler = new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("getIbaseInfo", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(warrantyApiResponse)
                };
            }

            if (url.Contains("getproducts", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonNode.Parse(productApiResponse)!.ToJsonString())
                };
            }

            throw new InvalidOperationException($"Unexpected request URL: {url}");
        });

        return new HttpClient(handler);
    }

    private sealed class StubHttpClientFactory(HttpClient? client = null) : HttpClientFactory
    {
        private readonly HttpClient _client = client ?? new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("No handler configured")));

        public override HttpClient Create() => _client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }

    #endregion
}
