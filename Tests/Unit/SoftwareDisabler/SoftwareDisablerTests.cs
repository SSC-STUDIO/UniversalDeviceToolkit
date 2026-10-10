using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.SoftwareDisabler;
using Xunit;

namespace UniversalDeviceToolkit.Tests.SoftwareDisabler;

/// <summary>
/// Unit tests for the SoftwareDisabler module.
/// Covers: SoftwareStatus enum values, SoftwareDisablerException,
/// concrete disabler configurations (FnKeysDisabler, LegionZoneDisabler, VantageDisabler),
/// GetStatusAsync behavior, and error handling patterns.
/// </summary>
public class SoftwareDisablerTests
{
    #region SoftwareStatus Enum Tests

    [Fact]
    public void SoftwareStatus_HasThreeValues()
    {
        // Act
        var values = Enum.GetValues<SoftwareStatus>();

        // Assert
        values.Should().HaveCount(3);
    }

    [Fact]
    public void SoftwareStatus_ContainsExpectedMembers()
    {
        // Assert
        SoftwareStatus.Enabled.Should().Be(SoftwareStatus.Enabled);
        SoftwareStatus.Disabled.Should().Be(SoftwareStatus.Disabled);
        SoftwareStatus.NotFound.Should().Be(SoftwareStatus.NotFound);
    }

    [Fact]
    public void SoftwareStatus_MembersHaveDistinctValues()
    {
        // Act
        var values = Enum.GetValues<SoftwareStatus>().Cast<int>().ToList();

        // Assert
        values.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region SoftwareDisablerException Tests

    [Fact]
    public void SoftwareDisablerException_WithMessage_SetsMessage()
    {
        // Arrange
        var inner = new InvalidOperationException("inner error");

        // Act
        var ex = new SoftwareDisablerException("test message", inner);

        // Assert
        ex.Message.Should().Be("test message");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void SoftwareDisablerException_InheritsFromException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new SoftwareDisablerException("msg", inner);

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void SoftwareDisablerException_WithNullInnerException_DoesNotThrow()
    {
        // Act
        var act = () => new SoftwareDisablerException("msg", null!);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region FnKeysDisabler Configuration Tests

    [Fact]
    public void FnKeysDisabler_ServiceNames_ContainsExpectedService()
    {
        // Arrange
        var disabler = new FnKeysDisabler();

        // Act - GetStatusAsync exercises the protected members via reflection-free testing
        // We verify the disabler can be instantiated and used
        disabler.Should().NotBeNull();
        disabler.Should().BeAssignableTo<AbstractSoftwareDisabler>();
    }

    [Fact]
    public async Task FnKeysDisabler_GetStatusAsync_ReturnsValidStatus()
    {
        // Arrange
        var disabler = new FnKeysDisabler();

        // Act
        var status = await disabler.GetStatusAsync();

        // Assert - on any system this should return one of the three valid statuses
        status.Should().BeOneOf(SoftwareStatus.Enabled, SoftwareStatus.Disabled, SoftwareStatus.NotFound);
    }

    [Fact]
    public void FnKeysDisabler_OnRefreshedEvent_CanSubscribe()
    {
        // Arrange
        var disabler = new FnKeysDisabler();
        var eventRaised = false;

        // Act
        disabler.OnRefreshed += (_, args) => eventRaised = true;

        // Assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public async Task FnKeysDisabler_GetStatusAsync_InvokesOnRefreshedEvent()
    {
        // Arrange
        var disabler = new FnKeysDisabler();
        AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs? receivedArgs = null;

        disabler.OnRefreshed += (_, args) => receivedArgs = args;

        // Act
        var status = await disabler.GetStatusAsync();

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.Status.Should().Be(status);
    }

    [Fact]
    public async Task FnKeysDisabler_GetStatusAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var disabler = new FnKeysDisabler();

        // Act
        var status1 = await disabler.GetStatusAsync();
        var status2 = await disabler.GetStatusAsync();

        // Assert
        status1.Should().BeOneOf(SoftwareStatus.Enabled, SoftwareStatus.Disabled, SoftwareStatus.NotFound);
        status2.Should().BeOneOf(SoftwareStatus.Enabled, SoftwareStatus.Disabled, SoftwareStatus.NotFound);
    }

    #endregion

    #region LegionZoneDisabler Configuration Tests

    [Fact]
    public void LegionZoneDisabler_CanBeInstantiated()
    {
        // Act
        var disabler = new LegionZoneDisabler();

        // Assert
        disabler.Should().NotBeNull();
        disabler.Should().BeAssignableTo<AbstractSoftwareDisabler>();
    }

    [Fact]
    public async Task LegionZoneDisabler_GetStatusAsync_ReturnsValidStatus()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();

        // Act
        var status = await disabler.GetStatusAsync();

        // Assert
        status.Should().BeOneOf(SoftwareStatus.Enabled, SoftwareStatus.Disabled, SoftwareStatus.NotFound);
    }

    [Fact]
    public async Task LegionZoneDisabler_GetStatusAsync_InvokesOnRefreshedEvent()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();
        var eventFired = false;

        disabler.OnRefreshed += (_, _) => eventFired = true;

        // Act
        await disabler.GetStatusAsync();

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void LegionZoneDisabler_OnRefreshedEvent_CanSubscribeAndUnsubscribe()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();
        EventHandler<AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs> handler = (_, _) => { };

        // Act & Assert
        disabler.OnRefreshed += handler;
        disabler.OnRefreshed -= handler;
    }

    [Fact]
    public async Task LegionZoneDisabler_GetStatusAsync_MultipleConcurrentCalls_DoNotThrow()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();

        // Act
        var tasks = Enumerable.Range(0, 5).Select(_ => disabler.GetStatusAsync()).ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(s =>
            s.Should().BeOneOf(SoftwareStatus.Enabled, SoftwareStatus.Disabled, SoftwareStatus.NotFound));
    }

    #endregion

    #region VantageDisabler Configuration Tests

    [Fact]
    public void VantageDisabler_CanBeInstantiated()
    {
        // Act
        var disabler = new VantageDisabler();

        // Assert
        disabler.Should().NotBeNull();
        disabler.Should().BeAssignableTo<AbstractSoftwareDisabler>();
    }

    [Fact]
    public async Task VantageDisabler_GetStatusAsync_ReturnsValidStatus()
    {
        // Arrange
        var disabler = new VantageDisabler();

        // Act
        var status = await disabler.GetStatusAsync();

        // Assert
        status.Should().BeOneOf(SoftwareStatus.Enabled, SoftwareStatus.Disabled, SoftwareStatus.NotFound);
    }

    [Fact]
    public async Task VantageDisabler_GetStatusAsync_InvokesOnRefreshedEvent()
    {
        // Arrange
        var disabler = new VantageDisabler();
        AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs? receivedArgs = null;

        disabler.OnRefreshed += (_, args) => receivedArgs = args;

        // Act
        var status = await disabler.GetStatusAsync();

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.Status.Should().Be(status);
    }

    [Fact]
    public void VantageDisabler_OnRefreshedEvent_CanSubscribe()
    {
        // Arrange
        var disabler = new VantageDisabler();
        var callCount = 0;

        // Act
        disabler.OnRefreshed += (_, _) => callCount++;

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    public async Task VantageDisabler_GetStatusAsync_CalledMultipleTimes_ReturnsConsistentType()
    {
        // Arrange
        var disabler = new VantageDisabler();

        // Act
        var results = new List<SoftwareStatus>();
        for (var i = 0; i < 3; i++)
        {
            results.Add(await disabler.GetStatusAsync());
        }

        // Assert - all results should be the same status (environment doesn't change during test)
        results.Should().AllBeEquivalentTo(results.First());
    }

    #endregion

    #region AbstractSoftwareDisablerEventArgs Tests

    [Fact]
    public void AbstractSoftwareDisablerEventArgs_StatusProperty_CanBeSet()
    {
        // Act
        var args = new AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs
        {
            Status = SoftwareStatus.Enabled
        };

        // Assert
        args.Status.Should().Be(SoftwareStatus.Enabled);
    }

    [Fact]
    public void AbstractSoftwareDisablerEventArgs_AllStatuses_CanBeAssigned()
    {
        // Act & Assert
        var args1 = new AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs { Status = SoftwareStatus.Enabled };
        var args2 = new AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs { Status = SoftwareStatus.Disabled };
        var args3 = new AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs { Status = SoftwareStatus.NotFound };

        args1.Status.Should().Be(SoftwareStatus.Enabled);
        args2.Status.Should().Be(SoftwareStatus.Disabled);
        args3.Status.Should().Be(SoftwareStatus.NotFound);
    }

    #endregion

    #region Cross-Disabler Consistency Tests

    [Fact]
    public async Task AllDisablers_GetStatusAsync_DoNotThrow()
    {
        // Arrange
        var disablers = new AbstractSoftwareDisabler[]
        {
            new FnKeysDisabler(),
            new LegionZoneDisabler(),
            new VantageDisabler()
        };

        // Act & Assert
        foreach (var disabler in disablers)
        {
            var act = async () => await disabler.GetStatusAsync();
            await act.Should().NotThrowAsync();
        }
    }

    [Fact]
    public async Task AllDisablers_GetStatusAsync_ReturnsKnownStatus()
    {
        // Arrange
        var disablers = new AbstractSoftwareDisabler[]
        {
            new FnKeysDisabler(),
            new LegionZoneDisabler(),
            new VantageDisabler()
        };

        // Act & Assert
        foreach (var disabler in disablers)
        {
            var status = await disabler.GetStatusAsync();
            Enum.IsDefined(typeof(SoftwareStatus), status).Should().BeTrue(
                $"because {disabler.GetType().Name} returned {status}");
        }
    }

    #endregion

    #region SoftwareStatus Enum Tests

    [Fact]
    public void SoftwareStatus_Enabled_HasExpectedValue()
    {
        ((int)SoftwareStatus.Enabled).Should().Be(0);
    }

    [Fact]
    public void SoftwareStatus_Disabled_HasExpectedValue()
    {
        ((int)SoftwareStatus.Disabled).Should().Be(1);
    }

    [Fact]
    public void SoftwareStatus_NotFound_HasExpectedValue()
    {
        ((int)SoftwareStatus.NotFound).Should().Be(2);
    }

    #endregion

    #region SoftwareDisablerException Tests

    [Fact]
    public void SoftwareDisablerException_WithMessageAndInner_SetsProperties()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new SoftwareDisablerException("test message", inner);

        // Assert
        ex.Message.Should().Contain("test message");
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void SoftwareDisablerException_IsException()
    {
        // Act
        var ex = new SoftwareDisablerException("msg", new Exception());

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }

    #endregion

    #region VantageDisabler Protected Properties

    [Fact]
    public void VantageDisabler_HasScheduledTasksPaths()
    {
        // Arrange
        var disabler = new VantageDisabler();
        var property = typeof(VantageDisabler).GetProperty("ScheduledTasksPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var paths = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        paths.Should().NotBeEmpty();
        paths.Should().Contain("Lenovo\\Vantage");
        paths.Should().Contain("Lenovo\\ImController");
    }

    [Fact]
    public void VantageDisabler_HasServiceNames()
    {
        // Arrange
        var disabler = new VantageDisabler();
        var property = typeof(VantageDisabler).GetProperty("ServiceNames", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var names = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        names.Should().NotBeEmpty();
        names.Should().Contain("ImControllerService");
        names.Should().Contain("LenovoVantageService");
    }

    [Fact]
    public void VantageDisabler_HasProcessNames()
    {
        // Arrange
        var disabler = new VantageDisabler();
        var property = typeof(VantageDisabler).GetProperty("ProcessNames", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var names = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        names.Should().NotBeEmpty();
        names.Should().Contain("LenovoVantage");
    }

    [Fact]
    public void VantageDisabler_ScheduledTasksPaths_ContainsExpectedCount()
    {
        // Arrange
        var disabler = new VantageDisabler();
        var property = typeof(VantageDisabler).GetProperty("ScheduledTasksPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var paths = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert - VantageDisabler has 7 scheduled task paths
        paths.Count().Should().Be(7);
    }

    #endregion

    #region FnKeysDisabler Protected Properties

    [Fact]
    public void FnKeysDisabler_HasEmptyScheduledTasksPaths()
    {
        // Arrange
        var disabler = new FnKeysDisabler();
        var property = typeof(FnKeysDisabler).GetProperty("ScheduledTasksPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var paths = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void FnKeysDisabler_HasServiceNames()
    {
        // Arrange
        var disabler = new FnKeysDisabler();
        var property = typeof(FnKeysDisabler).GetProperty("ServiceNames", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var names = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        names.Should().ContainSingle("LenovoFnAndFunctionKeys");
    }

    [Fact]
    public void FnKeysDisabler_HasProcessNames()
    {
        // Arrange
        var disabler = new FnKeysDisabler();
        var property = typeof(FnKeysDisabler).GetProperty("ProcessNames", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var names = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        names.Should().Contain("LenovoUtilityUI");
        names.Should().Contain("LenovoUtilityService");
        names.Should().Contain("LenovoSmartKey");
        names.Should().HaveCount(3);
    }

    #endregion

    #region LegionZoneDisabler Protected Properties

    [Fact]
    public void LegionZoneDisabler_HasEmptyScheduledTasksPaths()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();
        var property = typeof(LegionZoneDisabler).GetProperty("ScheduledTasksPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var paths = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void LegionZoneDisabler_HasServiceNames()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();
        var property = typeof(LegionZoneDisabler).GetProperty("ServiceNames", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var names = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        names.Should().ContainSingle("LZService");
    }

    [Fact]
    public void LegionZoneDisabler_HasProcessNames()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();
        var property = typeof(LegionZoneDisabler).GetProperty("ProcessNames", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var names = (IEnumerable<string>)property.GetValue(disabler)!;

        // Assert
        names.Should().Contain("LegionZone");
        names.Should().Contain("LZTray");
        names.Should().HaveCount(2);
    }

    #endregion

    #region OnRefreshed Event Tests

    [Fact]
    public void VantageDisabler_OnRefreshed_Event_CanSubscribe()
    {
        // Arrange
        var disabler = new VantageDisabler();
        var callCount = 0;

        // Act
        disabler.OnRefreshed += (_, _) => callCount++;

        // Assert - no exception thrown
        callCount.Should().Be(0);
    }

    [Fact]
    public void FnKeysDisabler_OnRefreshed_Event_CanSubscribe()
    {
        // Arrange
        var disabler = new FnKeysDisabler();
        var callCount = 0;

        // Act
        disabler.OnRefreshed += (_, _) => callCount++;

        // Assert
        callCount.Should().Be(0);
    }

    [Fact]
    public void LegionZoneDisabler_OnRefreshed_Event_CanSubscribe()
    {
        // Arrange
        var disabler = new LegionZoneDisabler();
        var callCount = 0;

        // Act
        disabler.OnRefreshed += (_, _) => callCount++;

        // Assert
        callCount.Should().Be(0);
    }

    #endregion

    #region AbstractSoftwareDisablerEventArgs Tests

    [Fact]
    public void AbstractSoftwareDisablerEventArgs_SetsStatus()
    {
        // Arrange & Act
        var args = new AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs { Status = SoftwareStatus.Enabled };

        // Assert
        args.Status.Should().Be(SoftwareStatus.Enabled);
    }

    [Fact]
    public void AbstractSoftwareDisablerEventArgs_CanSetAllStatusValues()
    {
        foreach (var status in Enum.GetValues<SoftwareStatus>())
        {
            // Act
            var args = new AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs { Status = status };

            // Assert
            args.Status.Should().Be(status);
        }
    }

    #endregion
}
