using System;
using System.Threading;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Notifications;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Notifications;

[Trait("Category", TestCategories.Unit)]
public class AppNotificationServiceTests
{
    [Fact]
    public void ShowSuccess_ShouldPublishWithFiveSecondDuration()
    {
        var service = new AppNotificationService();
        AppNotificationChangedEventArgs? args = null;
        service.Changed += (_, e) => args = e;

        var id = service.ShowSuccess("Installed", "Plugin A");

        id.Should().NotBeEmpty();
        args.Should().NotBeNull();
        args!.IsDismiss.Should().BeFalse();
        args.Notification.Severity.Should().Be(AppNotificationSeverity.Success);
        args.Notification.Duration.Should().Be(TimeSpan.FromSeconds(5));
        args.Notification.Title.Should().Be("Installed");
        args.Notification.Message.Should().Be("Plugin A");
    }

    [Fact]
    public void Show_WithSameMergeKey_ShouldReuseIdAndIncreaseCount()
    {
        var service = new AppNotificationService();
        var counts = 0;
        Guid? firstId = null;
        service.Changed += (_, e) =>
        {
            counts++;
            firstId ??= e.Notification.Id;
            e.Notification.Id.Should().Be(firstId.Value);
            e.MergeCount.Should().Be(counts);
        };

        service.ShowSuccess("Optimized", "Applied", mergeKey: "opt:test");
        service.ShowSuccess("Optimized", "Applied", mergeKey: "opt:test");
        service.ShowSuccess("Optimized", "Applied", mergeKey: "opt:test");

        counts.Should().Be(3);
    }

    [Fact]
    public void Dismiss_ShouldRaiseDismissEvent()
    {
        var service = new AppNotificationService();
        var id = service.ShowInfo("Hello");
        AppNotificationChangedEventArgs? dismiss = null;
        service.Changed += (_, e) =>
        {
            if (e.IsDismiss)
                dismiss = e;
        };

        service.Dismiss(id);

        dismiss.Should().NotBeNull();
        dismiss!.IsDismiss.Should().BeTrue();
        dismiss.Notification.Id.Should().Be(id);
    }

    [Fact]
    public void Show_EmptyTitle_ShouldThrow()
    {
        var service = new AppNotificationService();
        var act = () => service.Show(new AppNotificationRequest { Title = "  " });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dismiss_ShouldClearMergeKeySoNextShowGetsNewId()
    {
        var service = new AppNotificationService();
        var firstId = service.ShowSuccess("Optimized", "Applied", mergeKey: "opt:test");
        service.Dismiss(firstId);

        Guid? secondId = null;
        var mergeCount = 0;
        service.Changed += (_, e) =>
        {
            if (e.IsDismiss)
                return;
            secondId = e.Notification.Id;
            mergeCount = e.MergeCount;
        };

        service.ShowSuccess("Optimized", "Applied", mergeKey: "opt:test");

        secondId.Should().NotBeNull();
        secondId.Should().NotBe(firstId);
        mergeCount.Should().Be(1);
    }

    [Fact]
    public void Show_TrimsMergeKeyBeforeMatching()
    {
        var service = new AppNotificationService();
        var firstId = service.ShowSuccess("Optimized", "Applied", mergeKey: "opt:test");
        var secondId = service.ShowSuccess("Optimized", "Applied", mergeKey: "  opt:test  ");
        secondId.Should().Be(firstId);
    }
}
