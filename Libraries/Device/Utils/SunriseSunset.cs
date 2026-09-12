using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Settings;

namespace UniversalDeviceToolkit.Lib.Utils;

public class SunriseSunset(SunriseSunsetSettings settings, HttpClientFactory httpClientFactory)
{
    private const double ZenithDegrees = 90.83333333333333;

    public async Task<(Time?, Time?)> GetSunriseSunsetAsync(CancellationToken token = default)
    {
        var (sunrise, sunset) = (settings.Store.Sunrise, settings.Store.Sunset);
        if (settings.Store.LastCheckDateTime?.Date == DateTime.UtcNow.Date && sunrise is not null && sunset is not null)
            return (sunrise, sunset);

        var coordinate = await GetGeoLocationAsync(token).ConfigureAwait(false);

        if (coordinate is null)
            return (null, null);

        (sunrise, sunset) = CalculateSunriseSunsetUtc(coordinate.Value.Latitude, coordinate.Value.Longitude, DateTime.UtcNow);

        settings.Store.LastCheckDateTime = DateTime.UtcNow;
        settings.Store.Sunrise = sunrise;
        settings.Store.Sunset = sunset;
        settings.SynchronizeStore();

        return (sunrise, sunset);
    }

    private async Task<(double Latitude, double Longitude)?> GetGeoLocationAsync(CancellationToken token)
    {
        try
        {
            using var httpClient = httpClientFactory.Create();
            var responseJson = await httpClient.GetStringAsync("http://ip-api.com/json?fields=lat,lon", token).ConfigureAwait(false);
            var responseJsonNode = JsonNode.Parse(responseJson);
            if (responseJsonNode is not null &&
                double.TryParse(responseJsonNode["lat"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(responseJsonNode["lon"]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                return (lat, lon);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get geolocation.", ex);
        }

        return null;
    }

    internal static (Time?, Time?) CalculateSunriseSunsetUtc(double latitude, double longitude, DateTime dateTimeUtc)
    {
        var utcDate = dateTimeUtc.Kind == DateTimeKind.Utc
            ? dateTimeUtc.Date
            : dateTimeUtc.ToUniversalTime().Date;

        var sunrise = CalculateSolarEventTimeUtc(latitude, longitude, utcDate, isSunrise: true);
        var sunset = CalculateSolarEventTimeUtc(latitude, longitude, utcDate, isSunrise: false);

        if (sunrise is null || sunset is null)
            return (null, null);

        return (sunrise, sunset);
    }

    // Use the NOAA solar calculation so the automation keeps UTC sunrise/sunset behavior without shipping a multi-megabyte astronomy dependency.
    private static Time? CalculateSolarEventTimeUtc(double latitude, double longitude, DateTime utcDate, bool isSunrise)
    {
        var dayOfYear = utcDate.DayOfYear;
        var longitudeHour = longitude / 15D;
        var approximateTime = dayOfYear + (((isSunrise ? 6D : 18D) - longitudeHour) / 24D);

        var meanAnomaly = (0.9856D * approximateTime) - 3.289D;
        var trueLongitude = NormalizeDegrees(
            meanAnomaly +
            (1.916D * SinDegrees(meanAnomaly)) +
            (0.020D * SinDegrees(2D * meanAnomaly)) +
            282.634D);

        var rightAscension = NormalizeDegrees(RadiansToDegrees(Math.Atan(0.91764D * TanDegrees(trueLongitude))));
        var longitudeQuadrant = Math.Floor(trueLongitude / 90D) * 90D;
        var rightAscensionQuadrant = Math.Floor(rightAscension / 90D) * 90D;
        rightAscension = (rightAscension + longitudeQuadrant - rightAscensionQuadrant) / 15D;

        var sinDeclination = 0.39782D * SinDegrees(trueLongitude);
        var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
        var denominator = cosDeclination * CosDegrees(latitude);
        if (Math.Abs(denominator) < 0.000000000001D)
            return null;

        var cosLocalHourAngle = (CosDegrees(ZenithDegrees) - (sinDeclination * SinDegrees(latitude))) / denominator;
        if (cosLocalHourAngle > 1D || cosLocalHourAngle < -1D)
            return null;

        var localHourAngle = isSunrise
            ? 360D - RadiansToDegrees(Math.Acos(cosLocalHourAngle))
            : RadiansToDegrees(Math.Acos(cosLocalHourAngle));
        localHourAngle /= 15D;

        var localMeanTime = localHourAngle + rightAscension - (0.06571D * approximateTime) - 6.622D;
        var utcHours = NormalizeHours(localMeanTime - longitudeHour);

        return ToTime(utcHours);
    }

    private static Time ToTime(double hours)
    {
        var normalizedHours = NormalizeHours(hours);
        var totalMinutes = (int)Math.Round(normalizedHours * 60D, MidpointRounding.AwayFromZero);
        totalMinutes = ((totalMinutes % 1440) + 1440) % 1440;

        return new Time(totalMinutes / 60, totalMinutes % 60);
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360D;
        return normalized < 0D ? normalized + 360D : normalized;
    }

    private static double NormalizeHours(double hours)
    {
        var normalized = hours % 24D;
        return normalized < 0D ? normalized + 24D : normalized;
    }

    private static double SinDegrees(double degrees) => Math.Sin(DegreesToRadians(degrees));

    private static double CosDegrees(double degrees) => Math.Cos(DegreesToRadians(degrees));

    private static double TanDegrees(double degrees) => Math.Tan(DegreesToRadians(degrees));

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180D);

    private static double RadiansToDegrees(double radians) => radians * (180D / Math.PI);
}
