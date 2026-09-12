using FluentAssertions;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class SensorReadingHelperTests
{
    [Theory]
    [InlineData(45000, 45)]
    [InlineData(45500, 46)]
    [InlineData(45, 45)]
    [InlineData("65000", 65)]
    [InlineData(0, -1)]
    [InlineData(-100, -1)]
    [InlineData(2000000, -1)]
    public void NormalizePowerReadingToWatts_ShouldHandleWattsAndMilliwatts(object value, int expected)
    {
        var result = SensorReadingHelper.NormalizePowerReadingToWatts(value);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(8192f, true, 8f)]
    [InlineData(8f, false, 8f)]
    [InlineData(-1f, true, -1f)]
    [InlineData(0f, true, 0f)]
    public void NormalizeLibreHardwareMonitorDataToGigabytes_ShouldConvertSmallDataOnly(
        float value,
        bool isMegabyteScale,
        float expected)
    {
        var result = SensorReadingHelper.NormalizeLibreHardwareMonitorDataToGigabytes(value, isMegabyteScale);

        result.Should().BeApproximately(expected, 0.0001f);
    }

    [Theory]
    [InlineData(3002, 27)]
    [InlineData(3232, 50)]
    [InlineData("3532", 80)]
    [InlineData(0, -1)]
    [InlineData(100, -1)]
    [InlineData(5000, -1)]
    public void ConvertAcpiTenthsKelvinToCelsius_ShouldNormalizeValidThermalZoneValues(object value, int expected)
    {
        var result = SensorReadingHelper.ConvertAcpiTenthsKelvinToCelsius(value);

        result.Should().Be(expected);
    }
}
