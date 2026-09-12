using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class TimeExtensionsTests
{
    [Fact]
    public void UtcNow_ShouldReturnCurrentUtcHourAndMinute()
    {
        var utcNow = DateTime.UtcNow;
        var time = TimeExtensions.UtcNow;

        time.Hour.Should().Be(utcNow.Hour);
        // Minute can differ by 1 if test runs across a minute boundary
        Math.Abs(time.Minute - utcNow.Minute).Should().BeLessThanOrEqualTo(1);
    }
}
