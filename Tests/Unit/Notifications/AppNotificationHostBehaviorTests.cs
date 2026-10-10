using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Notifications;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Notifications;

/// <summary>
/// Product-behavior checks for the notification bus (stacking, merge, duration).
/// UI host visual verification is covered by smoke; these fail if the contract regresses.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AppNotificationHostBehaviorTests
{
    [Fact]
    public void PublishingEightSuccessNotifications_ShouldEmitEightEventsWithStableIdsWhenUnmerged()
    {
        var service = new AppNotificationService();
        var ids = new List<Guid>();
        service.Changed += (_, e) =>
        {
            if (!e.IsDismiss)
                ids.Add(e.Notification.Id);
        };

        for (var i = 0; i < 8; i++)
            service.ShowSuccess($"Title {i}", $"Message {i}", mergeKey: $"unique:{i}");

        ids.Should().HaveCount(8);
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().OnlyContain(id => id != Guid.Empty);
    }

    [Fact]
    public void SuccessDuration_ShouldBeFiveSecondsByDefault()
    {
        AppNotificationService.DefaultSuccessDuration.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Dismiss_ShouldNotThrowForUnknownId()
    {
        var service = new AppNotificationService();
        var act = () => service.Dismiss(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void WarningAndError_ShouldHaveLongerDefaultDurationsThanSuccess()
    {
        AppNotificationService.DefaultWarningDuration.Should().BeGreaterThan(AppNotificationService.DefaultSuccessDuration);
        AppNotificationService.DefaultErrorDuration.Should().BeGreaterThanOrEqualTo(AppNotificationService.DefaultWarningDuration);
    }
}
