using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Notifications;

[Trait("Category", TestCategories.Unit)]
public class NotificationMessageTests
{
    [Fact]
    public void NotificationMessage_Constructor_ShouldSetDefaultPriority()
    {
        var type = NotificationType.ACAdapterConnected;
        var message = new NotificationMessage(type, "arg1");

        message.Type.Should().Be(type);
        message.Priority.Should().Be(NotificationPriority.Normal);
        message.Args.Should().HaveCount(1);
        message.Args[0].Should().Be("arg1");
    }

    [Fact]
    public void NotificationMessage_Constructor_ShouldSetExplicitPriority()
    {
        var type = NotificationType.UpdateAvailable;
        var priority = NotificationPriority.High;
        var message = new NotificationMessage(type, priority, "arg1", "arg2");

        message.Type.Should().Be(type);
        message.Priority.Should().Be(priority);
        message.Args.Should().HaveCount(2);
        message.Args[0].Should().Be("arg1");
        message.Args[1].Should().Be("arg2");
    }

    [Fact]
    public void PriorityMapping_ShouldMapCorrectlyForPriorityQueue()
    {
        (2 - (int)NotificationPriority.High).Should().Be(0);
        (2 - (int)NotificationPriority.Normal).Should().Be(1);
        (2 - (int)NotificationPriority.Low).Should().Be(2);
    }

    [Fact]
    public void ToString_ShouldContainTypeAndPriority()
    {
        var message = new NotificationMessage(NotificationType.FnLockOn, NotificationPriority.Low, "test");
        var str = message.ToString();

        str.Should().Contain("FnLockOn");
        str.Should().Contain("Low");
        str.Should().Contain("test");
    }

    [Fact]
    public void ToString_EmptyArgs_ShouldReturnEmptyBrackets()
    {
        var message = new NotificationMessage(NotificationType.CapsLockOn, Array.Empty<object>());
        var str = message.ToString();

        str.Should().Contain("[]");
    }

    [Fact]
    public void Constructor_WithNoArgs_ShouldHaveEmptyArgs()
    {
        var message = new NotificationMessage(NotificationType.NumLockOff);

        message.Type.Should().Be(NotificationType.NumLockOff);
        message.Priority.Should().Be(NotificationPriority.Normal);
        message.Args.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_MultipleArgs_ShouldPreserveAll()
    {
        var message = new NotificationMessage(
            NotificationType.UpdateAvailable,
            NotificationPriority.High,
            "arg1", "arg2", "arg3");

        message.Args.Should().HaveCount(3);
        message.Args[0].Should().Be("arg1");
        message.Args[1].Should().Be("arg2");
        message.Args[2].Should().Be("arg3");
    }
}
