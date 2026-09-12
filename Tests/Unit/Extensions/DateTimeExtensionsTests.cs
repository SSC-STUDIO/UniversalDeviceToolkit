using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class DateTimeExtensionsTests
{
    #region UtcFrom Tests

    [Fact]
    public void UtcFrom_WithValidHoursAndMinutes_ShouldReturnCorrectDateTime()
    {
        // Arrange
        var hours = 14;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.UtcFrom(hours, minutes);

        // Assert
        result.Hour.Should().Be(hours);
        result.Minute.Should().Be(minutes);
        result.Second.Should().Be(0);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void UtcFrom_ShouldUseCurrentDate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var hours = 10;
        var minutes = 15;

        // Act
        var result = DateTimeExtensions.UtcFrom(hours, minutes);

        // Assert
        result.Year.Should().Be(now.Year);
        result.Month.Should().Be(now.Month);
        result.Day.Should().Be(now.Day);
    }

    [Fact]
    public void UtcFrom_WithZeroHoursAndMinutes_ShouldReturnMidnight()
    {
        // Arrange
        var hours = 0;
        var minutes = 0;

        // Act
        var result = DateTimeExtensions.UtcFrom(hours, minutes);

        // Assert
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
        result.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void UtcFrom_WithMaxHoursAndMinutes_ShouldReturnCorrectTime()
    {
        // Arrange
        var hours = 23;
        var minutes = 59;

        // Act
        var result = DateTimeExtensions.UtcFrom(hours, minutes);

        // Assert
        result.Hour.Should().Be(23);
        result.Minute.Should().Be(59);
    }

    #endregion

    #region LocalFrom Tests

    [Fact]
    public void LocalFrom_WithValidHoursAndMinutes_ShouldReturnCorrectDateTime()
    {
        // Arrange
        var hours = 14;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.LocalFrom(hours, minutes);

        // Assert
        result.Hour.Should().Be(hours);
        result.Minute.Should().Be(minutes);
        result.Second.Should().Be(0);
        result.Kind.Should().Be(DateTimeKind.Local);
    }

    [Fact]
    public void LocalFrom_ShouldUseCurrentDate()
    {
        // Arrange
        var now = DateTime.Now;
        var hours = 10;
        var minutes = 15;

        // Act
        var result = DateTimeExtensions.LocalFrom(hours, minutes);

        // Assert
        result.Year.Should().Be(now.Year);
        result.Month.Should().Be(now.Month);
        result.Day.Should().Be(now.Day);
    }

    [Fact]
    public void LocalFrom_WithZeroHoursAndMinutes_ShouldReturnMidnight()
    {
        // Arrange
        var hours = 0;
        var minutes = 0;

        // Act
        var result = DateTimeExtensions.LocalFrom(hours, minutes);

        // Assert
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
        result.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void LocalFrom_WithMaxHoursAndMinutes_ShouldReturnCorrectTime()
    {
        // Arrange
        var hours = 23;
        var minutes = 59;

        // Act
        var result = DateTimeExtensions.LocalFrom(hours, minutes);

        // Assert
        result.Hour.Should().Be(23);
        result.Minute.Should().Be(59);
    }

    #endregion

    #region UtcDayFrom Tests

    [Fact]
    public void UtcDayFrom_WhenTargetDayIsToday_ShouldReturnToday()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetDay = now.DayOfWeek;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.UtcDayFrom(targetDay, hours, minutes);

        // Assert
        result.DayOfWeek.Should().Be(targetDay);
        result.Hour.Should().Be(hours);
        result.Minute.Should().Be(minutes);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void UtcDayFrom_WhenTargetDayIsTomorrow_ShouldReturnTomorrow()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var tomorrow = now.AddDays(1).DayOfWeek;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.UtcDayFrom(tomorrow, hours, minutes);

        // Assert
        result.DayOfWeek.Should().Be(tomorrow);
        result.Hour.Should().Be(hours);
        result.Minute.Should().Be(minutes);
    }

    [Fact]
    public void UtcDayFrom_WhenTargetDayIsNextWeek_ShouldReturnCorrectDate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var daysToAdd = 3;
        var targetDay = now.AddDays(daysToAdd).DayOfWeek;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.UtcDayFrom(targetDay, hours, minutes);

        // Assert
        result.DayOfWeek.Should().Be(targetDay);
        var diff = (result.Date - now.Date).Days;
        diff.Should().BeGreaterThanOrEqualTo(0);
        diff.Should().BeLessThanOrEqualTo(6);
    }

    [Fact]
    public void UtcDayFrom_ShouldAlwaysReturnFutureOrTodayDate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var targetDay = DayOfWeek.Monday;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.UtcDayFrom(targetDay, hours, minutes);

        // Assert
        result.Should().BeOnOrAfter(now.Date);
    }

    [Fact]
    public void UtcDayFrom_WithAllDaysOfWeek_ShouldReturnCorrectDayOfWeek()
    {
        // Arrange
        var hours = 10;
        var minutes = 30;
        var daysOfWeek = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

        foreach (var targetDay in daysOfWeek)
        {
            // Act
            var result = DateTimeExtensions.UtcDayFrom(targetDay, hours, minutes);

            // Assert
            result.DayOfWeek.Should().Be(targetDay);
            result.Hour.Should().Be(hours);
            result.Minute.Should().Be(minutes);
        }
    }

    #endregion

    #region LocalDayFrom Tests

    [Fact]
    public void LocalDayFrom_WhenTargetDayIsToday_ShouldReturnToday()
    {
        // Arrange
        var now = DateTime.Now;
        var targetDay = now.DayOfWeek;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.LocalDayFrom(targetDay, hours, minutes);

        // Assert
        result.DayOfWeek.Should().Be(targetDay);
        result.Hour.Should().Be(hours);
        result.Minute.Should().Be(minutes);
        result.Kind.Should().Be(DateTimeKind.Local);
    }

    [Fact]
    public void LocalDayFrom_WhenTargetDayIsTomorrow_ShouldReturnTomorrow()
    {
        // Arrange
        var now = DateTime.Now;
        var tomorrow = now.AddDays(1).DayOfWeek;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.LocalDayFrom(tomorrow, hours, minutes);

        // Assert
        result.DayOfWeek.Should().Be(tomorrow);
        result.Hour.Should().Be(hours);
        result.Minute.Should().Be(minutes);
    }

    [Fact]
    public void LocalDayFrom_WhenTargetDayIsNextWeek_ShouldReturnCorrectDate()
    {
        // Arrange
        var now = DateTime.Now;
        var daysToAdd = 3;
        var targetDay = now.AddDays(daysToAdd).DayOfWeek;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.LocalDayFrom(targetDay, hours, minutes);

        // Assert
        result.DayOfWeek.Should().Be(targetDay);
        var diff = (result.Date - now.Date).Days;
        diff.Should().BeGreaterThanOrEqualTo(0);
        diff.Should().BeLessThanOrEqualTo(6);
    }

    [Fact]
    public void LocalDayFrom_ShouldAlwaysReturnFutureOrTodayDate()
    {
        // Arrange
        var now = DateTime.Now;
        var targetDay = DayOfWeek.Friday;
        var hours = 10;
        var minutes = 30;

        // Act
        var result = DateTimeExtensions.LocalDayFrom(targetDay, hours, minutes);

        // Assert
        result.Should().BeOnOrAfter(now.Date);
    }

    [Fact]
    public void LocalDayFrom_WithAllDaysOfWeek_ShouldReturnCorrectDayOfWeek()
    {
        // Arrange
        var hours = 10;
        var minutes = 30;
        var daysOfWeek = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

        foreach (var targetDay in daysOfWeek)
        {
            // Act
            var result = DateTimeExtensions.LocalDayFrom(targetDay, hours, minutes);

            // Assert
            result.DayOfWeek.Should().Be(targetDay);
            result.Hour.Should().Be(hours);
            result.Minute.Should().Be(minutes);
        }
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void UtcFrom_AndLocalFrom_ShouldHaveDifferentKinds()
    {
        // Arrange
        var hours = 14;
        var minutes = 30;

        // Act
        var utcTime = DateTimeExtensions.UtcFrom(hours, minutes);
        var localTime = DateTimeExtensions.LocalFrom(hours, minutes);

        // Assert
        utcTime.Kind.Should().Be(DateTimeKind.Utc);
        localTime.Kind.Should().Be(DateTimeKind.Local);
    }

    [Fact]
    public void UtcDayFrom_AndLocalDayFrom_ShouldHaveDifferentKinds()
    {
        // Arrange
        var targetDay = DayOfWeek.Monday;
        var hours = 14;
        var minutes = 30;

        // Act
        var utcTime = DateTimeExtensions.UtcDayFrom(targetDay, hours, minutes);
        var localTime = DateTimeExtensions.LocalDayFrom(targetDay, hours, minutes);

        // Assert
        utcTime.Kind.Should().Be(DateTimeKind.Utc);
        localTime.Kind.Should().Be(DateTimeKind.Local);
    }

    #endregion
}
