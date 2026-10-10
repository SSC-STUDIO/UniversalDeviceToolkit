using System;
using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Messaging;
using UniversalDeviceToolkit.Lib.Messaging.Messages;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

[Trait("Category", TestCategories.Unit)]
public class MessagingModelsExpandedTests
{
    #region NotificationMessage Tests

    [Fact]
    public void NotificationMessage_TypeOnly_ShouldSetDefaultPriority()
    {
        var msg = new NotificationMessage(NotificationType.FnLockOn);
        msg.Type.Should().Be(NotificationType.FnLockOn);
        msg.Priority.Should().Be(NotificationPriority.Normal);
        msg.Args.Should().BeEmpty();
    }

    [Fact]
    public void NotificationMessage_WithArgs_ShouldStoreArgs()
    {
        var msg = new NotificationMessage(NotificationType.RefreshRate, 144, "Hz");
        msg.Type.Should().Be(NotificationType.RefreshRate);
        msg.Args.Should().HaveCount(2);
        msg.Args[0].Should().Be(144);
        msg.Args[1].Should().Be("Hz");
    }

    [Fact]
    public void NotificationMessage_WithPriority_ShouldSetPriority()
    {
        var msg = new NotificationMessage(NotificationType.ACAdapterConnected, NotificationPriority.High);
        msg.Priority.Should().Be(NotificationPriority.High);
    }

    [Fact]
    public void NotificationMessage_ShouldImplementIMessage()
    {
        var msg = new NotificationMessage(NotificationType.CameraOn);
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void NotificationMessage_Equality_SameValues_ShouldBeEqual()
    {
        var a = new NotificationMessage(NotificationType.FnLockOn, 1, "test");
        var b = new NotificationMessage(NotificationType.FnLockOn, 1, "test");
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void NotificationMessage_Equality_DifferentType_ShouldNotBeEqual()
    {
        var a = new NotificationMessage(NotificationType.FnLockOn);
        var b = new NotificationMessage(NotificationType.FnLockOff);
        a.Should().NotBe(b);
    }

    [Fact]
    public void NotificationMessage_Equality_DifferentArgs_ShouldNotBeEqual()
    {
        var a = new NotificationMessage(NotificationType.RefreshRate, 144);
        var b = new NotificationMessage(NotificationType.RefreshRate, 60);
        a.Should().NotBe(b);
    }

    #endregion

    #region FeatureStateMessage Tests

    [Fact]
    public void FeatureStateMessage_WithEnumState_ShouldStoreState()
    {
        var msg = new FeatureStateMessage<FnLockState>(FnLockState.On);
        msg.State.Should().Be(FnLockState.On);
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void FeatureStateMessage_WithBoolState_ShouldStoreState()
    {
        var msg = new FeatureStateMessage<bool>(true);
        msg.State.Should().BeTrue();
    }

    [Fact]
    public void FeatureStateMessage_Equality_SameState_ShouldBeEqual()
    {
        var a = new FeatureStateMessage<PowerModeState>(PowerModeState.Performance);
        var b = new FeatureStateMessage<PowerModeState>(PowerModeState.Performance);
        a.Should().Be(b);
    }

    [Fact]
    public void FeatureStateMessage_Equality_DifferentState_ShouldNotBeEqual()
    {
        var a = new FeatureStateMessage<PowerModeState>(PowerModeState.Quiet);
        var b = new FeatureStateMessage<PowerModeState>(PowerModeState.Performance);
        a.Should().NotBe(b);
    }

    #endregion

    #region OsdChangedMessage Tests

    [Fact]
    public void OsdChangedMessage_ShouldStoreState()
    {
        var msg = new OsdChangedMessage(OsdState.Show);
        msg.State.Should().Be(OsdState.Show);
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void OsdChangedMessage_Equality_SameState_ShouldBeEqual()
    {
        var a = new OsdChangedMessage(OsdState.Toggle);
        var b = new OsdChangedMessage(OsdState.Toggle);
        a.Should().Be(b);
    }

    [Fact]
    public void OsdChangedMessage_Equality_DifferentState_ShouldNotBeEqual()
    {
        var a = new OsdChangedMessage(OsdState.Hidden);
        var b = new OsdChangedMessage(OsdState.Show);
        a.Should().NotBe(b);
    }

    #endregion

    #region OsdElementChangedMessage Tests

    [Fact]
    public void OsdElementChangedMessage_ShouldStoreItems()
    {
        var items = new List<OsdItem> { OsdItem.Fps, OsdItem.CpuTemperature };
        var msg = new OsdElementChangedMessage(items);
        msg.Items.Should().HaveCount(2);
        msg.Items.Should().Contain(OsdItem.Fps);
        msg.Items.Should().Contain(OsdItem.CpuTemperature);
        msg.Should().BeAssignableTo<IMessage>();
    }

    [Fact]
    public void OsdElementChangedMessage_EmptyItems_ShouldWork()
    {
        var msg = new OsdElementChangedMessage(new List<OsdItem>());
        msg.Items.Should().BeEmpty();
    }

    #endregion

    #region Additional Enum Edge Cases

    [Theory]
    [InlineData(SpectrumKeyboardBacklightDirection.LeftToRight)]
    [InlineData(SpectrumKeyboardBacklightDirection.RightToLeft)]
    public void SpectrumKeyboardBacklightDirection_ShouldBeDefined(SpectrumKeyboardBacklightDirection value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void SpectrumKeyboardBacklightDirection_ShouldHaveTwoMembers()
    {
        Enum.GetValues<SpectrumKeyboardBacklightDirection>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(SpectrumKeyboardBacklightClockwiseDirection.Clockwise)]
    [InlineData(SpectrumKeyboardBacklightClockwiseDirection.CounterClockwise)]
    public void SpectrumKeyboardBacklightClockwiseDirection_ShouldBeDefined(SpectrumKeyboardBacklightClockwiseDirection value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void SpectrumKeyboardBacklightClockwiseDirection_ShouldHaveTwoMembers()
    {
        Enum.GetValues<SpectrumKeyboardBacklightClockwiseDirection>().Should().HaveCount(3);
    }

    [Fact]
    public void OneLevelWhiteKeyboardBacklightState_ShouldHaveTwoMembers()
    {
        Enum.GetValues<OneLevelWhiteKeyboardBacklightState>().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(OneLevelWhiteKeyboardBacklightState.Off)]
    [InlineData(OneLevelWhiteKeyboardBacklightState.On)]
    public void OneLevelWhiteKeyboardBacklightState_ShouldBeDefined(OneLevelWhiteKeyboardBacklightState value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void NotificationPosition_AllValues_ShouldBeDefined()
    {
        var values = Enum.GetValues<NotificationPosition>();
        foreach (var v in values)
            Enum.IsDefined(v).Should().BeTrue();
    }

    #endregion
}

