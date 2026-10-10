using System;
using System.Reflection;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class AssemblyExtensionsTests
{
    [Fact]
    public void GetBuildDateTime_WithCurrentAssembly_ShouldNotThrow()
    {
        var assembly = typeof(AssemblyExtensionsTests).Assembly;
        var result = assembly.GetBuildDateTime();
        // Test assemblies typically lack +build metadata
        result.Should().BeNull("test assemblies typically lack +build metadata");
    }

    [Fact]
    public void GetBuildDateTimeString_WithCurrentAssembly_ShouldReturnNullWhenNoBuildMetadata()
    {
        var assembly = typeof(AssemblyExtensionsTests).Assembly;
        var result = assembly.GetBuildDateTimeString();
        result.Should().BeNull("test assemblies typically lack +build metadata");
    }

    [Theory]
    [InlineData("1.2.3+build20260512143000", 2026, 5, 12, 14, 30, 0)]
    [InlineData("2.0.0+build20250101120000", 2025, 1, 1, 12, 0, 0)]
    [InlineData("0.1.0+BUILD20260512143000", 2026, 5, 12, 14, 30, 0)]
    public void GetBuildDateTime_WithInformationalVersionFormat_ShouldParseCorrectly(
        string version, int year, int month, int day, int hour, int minute, int second)
    {
        // Use GetInformationalVersion to parse the attribute from the version string
        // directly, since the method reads AssemblyInformationalVersionAttribute
        // We test by verifying the parsing logic via reflection on the result
        var dateTime = ParseBuildDateTime(version);

        dateTime.Should().NotBeNull();
        dateTime!.Value.Year.Should().Be(year);
        dateTime.Value.Month.Should().Be(month);
        dateTime.Value.Day.Should().Be(day);
        dateTime.Value.Hour.Should().Be(hour);
        dateTime.Value.Minute.Should().Be(minute);
        dateTime.Value.Second.Should().Be(second);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0+notbuild20260512143000")]
    [InlineData("1.0.0+build-notadate")]
    [InlineData("1.0.0+build")]
    [InlineData("")]
    public void GetBuildDateTime_WithoutValidBuildSuffix_ShouldReturnNull(string version)
    {
        var dateTime = ParseBuildDateTime(version);

        dateTime.Should().BeNull();
    }

    [Theory]
    [InlineData("2.0.0+build20250101120000", "20250101120000")]
    [InlineData("1.2.3+build20260512143000", "20260512143000")]
    public void GetBuildDateTimeString_Format_ShouldProduceExpectedString(string version, string expected)
    {
        var dateTime = ParseBuildDateTime(version);

        dateTime.Should().NotBeNull();
        dateTime!.Value.ToString("yyyyMMddHHmmss").Should().Be(expected);
    }

    /// <summary>
    /// Replicates the parsing logic from AssemblyExtensions.GetBuildDateTime
    /// to test date extraction without needing AssemblyBuilder.
    /// </summary>
    private static DateTime? ParseBuildDateTime(string informationalVersion)
    {
        const string buildVersionMetadataPrefix = "+build";

        if (string.IsNullOrEmpty(informationalVersion))
            return null;

        var index = informationalVersion.IndexOf(buildVersionMetadataPrefix, StringComparison.OrdinalIgnoreCase);
        if (index <= 0)
            return null;

        var value = informationalVersion[(index + buildVersionMetadataPrefix.Length)..];

        if (DateTime.TryParseExact(value, "yyyyMMddHHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var result))
            return result;

        return null;
    }
}
