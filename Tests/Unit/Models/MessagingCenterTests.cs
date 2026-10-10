using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class MessagingCenterTests
{
    private sealed class TestMessage(int value) : IMessage
    {
        public int Value { get; } = value;
    }

    [Fact]
    public void Publish_Subscribe_ShouldReceiveMessage()
    {
        var subscriber = new object();
        int? received = null;
        MessagingCenter.Subscribe<TestMessage>(subscriber, m => received = m.Value);

        MessagingCenter.Publish(new TestMessage(42));

        received.Should().Be(42);
        MessagingCenter.Unsubscribe(subscriber);
    }

    [Fact]
    public void Unsubscribe_ShouldNotReceiveAfterUnsubscribe()
    {
        var subscriber = new object();
        int callCount = 0;
        MessagingCenter.Subscribe<TestMessage>(subscriber, _ => callCount++);

        MessagingCenter.Publish(new TestMessage(1));
        MessagingCenter.Unsubscribe(subscriber);
        MessagingCenter.Publish(new TestMessage(2));

        callCount.Should().Be(1);
    }

    [Fact]
    public void Subscribe_Parameterless_ShouldReceiveCallback()
    {
        var subscriber = new object();
        bool called = false;
        MessagingCenter.Subscribe<TestMessage>(subscriber, () => called = true);

        MessagingCenter.Publish(new TestMessage(99));

        called.Should().BeTrue();
        MessagingCenter.Unsubscribe(subscriber);
    }

    [Fact]
    public void Publish_WithNoSubscribers_ShouldNotThrow()
    {
        var act = () => MessagingCenter.Publish(new TestMessage(0));
        act.Should().NotThrow();
    }

    [Fact]
    public void Unsubscribe_Typed_ShouldOnlyRemoveSpecificMessageType()
    {
        var subscriber = new object();
        int testMsgCount = 0;
        int otherMsgCount = 0;
        MessagingCenter.Subscribe<TestMessage>(subscriber, _ => testMsgCount++);
        MessagingCenter.Subscribe<FeatureStateMessage<bool>>(subscriber, _ => otherMsgCount++);

        MessagingCenter.Unsubscribe<TestMessage>(subscriber);
        MessagingCenter.Publish(new TestMessage(1));
        MessagingCenter.Publish(new FeatureStateMessage<bool>(true));

        testMsgCount.Should().Be(0);
        otherMsgCount.Should().Be(1);
        MessagingCenter.Unsubscribe(subscriber);
    }
}