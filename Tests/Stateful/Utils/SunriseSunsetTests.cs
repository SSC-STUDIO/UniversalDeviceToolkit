using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Tests.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.Settings)]
public class SunriseSunsetTests : IDisposable
{
    private readonly SunriseSunsetSettings _settings;

    public SunriseSunsetTests()
    {
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.SunriseSunset);
        _settings = new SunriseSunsetSettings();
    }

    public void Dispose()
    {
        SettingsCleanupHelper.CleanupSettingsFile(SettingsCleanupHelper.SettingsFiles.SunriseSunset);
    }

    [Fact]
    public void CalculateSunriseSunsetUtc_ShouldReturnKnownUtcTimesForSeattle()
    {
        var date = new DateTime(2019, 3, 19, 0, 0, 0, DateTimeKind.Utc);

        var (sunrise, sunset) = SunriseSunset.CalculateSunriseSunsetUtc(47.6062, -122.3321, date);

        sunrise.Should().NotBeNull();
        sunset.Should().NotBeNull();
        TotalMinutes(sunrise!.Value).Should().BeInRange(TotalMinutes(new Time(14, 15)) - 2, TotalMinutes(new Time(14, 15)) + 2);
        TotalMinutes(sunset!.Value).Should().BeInRange(TotalMinutes(new Time(2, 19)) - 2, TotalMinutes(new Time(2, 19)) + 2);
    }

    [Fact]
    public async Task GetSunriseSunsetAsync_ShouldReuseCachedValuesWithinSameUtcDay()
    {
        _settings.Store.LastCheckDateTime = DateTime.UtcNow;
        _settings.Store.Sunrise = new Time(6, 30);
        _settings.Store.Sunset = new Time(18, 45);
        _settings.SynchronizeStore();

        var factory = new CountingHttpClientFactory("""{"lat":47.6062,"lon":-122.3321}""");
        var sunriseSunset = new SunriseSunset(_settings, factory);

        var (sunrise, sunset) = await sunriseSunset.GetSunriseSunsetAsync();

        sunrise.Should().Be(new Time(6, 30));
        sunset.Should().Be(new Time(18, 45));
        factory.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSunriseSunsetAsync_ShouldRefreshExpiredCacheAndPersistNewValues()
    {
        _settings.Store.LastCheckDateTime = DateTime.UtcNow.AddDays(-1);
        _settings.Store.Sunrise = new Time(1, 1);
        _settings.Store.Sunset = new Time(2, 2);
        _settings.SynchronizeStore();

        var factory = new CountingHttpClientFactory("""{"lat":47.6062,"lon":-122.3321}""");
        var sunriseSunset = new SunriseSunset(_settings, factory);

        var (sunrise, sunset) = await sunriseSunset.GetSunriseSunsetAsync();

        sunrise.Should().NotBeNull();
        sunset.Should().NotBeNull();
        sunrise.Should().NotBe(new Time(1, 1));
        sunset.Should().NotBe(new Time(2, 2));
        factory.RequestCount.Should().Be(1);
        _settings.Store.LastCheckDateTime?.Date.Should().Be(DateTime.UtcNow.Date);
        _settings.Store.Sunrise.Should().Be(sunrise);
        _settings.Store.Sunset.Should().Be(sunset);
    }

    private sealed class CountingHttpClientFactory(string jsonPayload) : HttpClientFactory
    {
        private readonly HttpClient _client = new(new StubHttpMessageHandler(jsonPayload));

        public int RequestCount { get; private set; }

        public override HttpClient Create()
        {
            RequestCount++;
            return _client;
        }
    }

    private sealed class StubHttpMessageHandler(string jsonPayload) : HttpMessageHandler
    {
        private readonly string _jsonPayload = jsonPayload;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StringContent(_jsonPayload, Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = content });
        }
    }

    private static int TotalMinutes(Time time) => (time.Hour * 60) + time.Minute;
}
