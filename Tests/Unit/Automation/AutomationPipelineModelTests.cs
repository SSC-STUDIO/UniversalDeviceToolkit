using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Trait("Category", TestCategories.Unit)]
public class AutomationPipelineModelTests
{
    #region AutomationPipeline Defaults Tests

    [Fact]
    public void AutomationPipeline_Defaults_ShouldHaveExpectedValues()
    {
        var pipeline = new AutomationPipeline();
        pipeline.Id.Should().NotBe(Guid.Empty);
        pipeline.Name.Should().BeNull();
        pipeline.IconName.Should().BeNull();
        pipeline.Trigger.Should().BeNull();
        pipeline.Steps.Should().BeEmpty();
        pipeline.IsExclusive.Should().BeTrue();
    }

    [Fact]
    public void AutomationPipeline_NameConstructor_ShouldSetName()
    {
        var pipeline = new AutomationPipeline("My Pipeline");
        pipeline.Name.Should().Be("My Pipeline");
    }

    [Fact]
    public void AutomationPipeline_TriggerConstructor_ShouldSetTrigger()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        var pipeline = new AutomationPipeline(trigger);
        pipeline.Trigger.Should().BeSameAs(trigger);
    }

    [Fact]
    public void AutomationPipeline_Id_ShouldBeUnique()
    {
        var p1 = new AutomationPipeline();
        var p2 = new AutomationPipeline();
        p1.Id.Should().NotBe(p2.Id);
    }

    #endregion

    #region AutomationPipeline AllTriggers Tests

    [Fact]
    public void AutomationPipeline_AllTriggers_NullTrigger_ShouldBeEmpty()
    {
        var pipeline = new AutomationPipeline();
        pipeline.AllTriggers.Should().BeEmpty();
    }

    [Fact]
    public void AutomationPipeline_AllTriggers_SingleTrigger_ShouldReturnIt()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        var pipeline = new AutomationPipeline(trigger);
        pipeline.AllTriggers.Should().ContainSingle().Which.Should().BeSameAs(trigger);
    }

    #endregion

    #region ACAdapterConnectedAutomationPipelineTrigger Tests

    [Fact]
    public void ACAdapterConnectedTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ACAdapterConnectedTrigger_UpdateEnvironment_ShouldSetAcAdapter()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_AC_ADAPTER_CONNECTED"].Should().Be("TRUE");
    }

    [Fact]
    public void ACAdapterConnectedTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
        copy.Should().BeOfType<ACAdapterConnectedAutomationPipelineTrigger>();
    }

    [Fact]
    public void ACAdapterConnectedTrigger_Equals_SameType_ShouldBeTrue()
    {
        var t1 = new ACAdapterConnectedAutomationPipelineTrigger();
        var t2 = new ACAdapterConnectedAutomationPipelineTrigger();
        t1.Equals(t2).Should().BeTrue();
    }

    [Fact]
    public void ACAdapterConnectedTrigger_Equals_DifferentType_ShouldBeFalse()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        trigger.Equals("not a trigger").Should().BeFalse();
    }

    [Fact]
    public void ACAdapterConnectedTrigger_Equals_Null_ShouldBeFalse()
    {
        var trigger = new ACAdapterConnectedAutomationPipelineTrigger();
        trigger.Equals(null).Should().BeFalse();
    }

    #endregion

    #region ACAdapterDisconnectedAutomationPipelineTrigger Tests

    [Fact]
    public void ACAdapterDisconnectedTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new ACAdapterDisconnectedAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ACAdapterDisconnectedTrigger_UpdateEnvironment_ShouldSetAcAdapterFalse()
    {
        var trigger = new ACAdapterDisconnectedAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_AC_ADAPTER_CONNECTED"].Should().Be("FALSE");
    }

    [Fact]
    public void ACAdapterDisconnectedTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new ACAdapterDisconnectedAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region GamesAreRunningAutomationPipelineTrigger Tests

    [Fact]
    public void GamesAreRunningTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new GamesAreRunningAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GamesAreRunningTrigger_UpdateEnvironment_ShouldSetGameRunning()
    {
        var trigger = new GamesAreRunningAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_GAME_RUNNING"].Should().Be("TRUE");
    }

    [Fact]
    public void GamesAreRunningTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new GamesAreRunningAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region GamesStopAutomationPipelineTrigger Tests

    [Fact]
    public void GamesStopTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new GamesStopAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GamesStopTrigger_UpdateEnvironment_ShouldSetGameRunningFalse()
    {
        var trigger = new GamesStopAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_GAME_RUNNING"].Should().Be("FALSE");
    }

    [Fact]
    public void GamesStopTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new GamesStopAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region DisplayOnAutomationPipelineTrigger Tests

    [Fact]
    public void DisplayOnTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new DisplayOnAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DisplayOnTrigger_UpdateEnvironment_ShouldSetDisplayOn()
    {
        var trigger = new DisplayOnAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_DISPLAY_ON"].Should().Be("TRUE");
    }

    [Fact]
    public void DisplayOnTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new DisplayOnAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region DisplayOffAutomationPipelineTrigger Tests

    [Fact]
    public void DisplayOffTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new DisplayOffAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DisplayOffTrigger_UpdateEnvironment_ShouldSetDisplayOff()
    {
        var trigger = new DisplayOffAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_DISPLAY_ON"].Should().Be("FALSE");
    }

    [Fact]
    public void DisplayOffTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new DisplayOffAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region ExternalDisplayConnectedAutomationPipelineTrigger Tests

    [Fact]
    public void ExternalDisplayConnectedTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new ExternalDisplayConnectedAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ExternalDisplayConnectedTrigger_UpdateEnvironment_ShouldSetExternalDisplay()
    {
        var trigger = new ExternalDisplayConnectedAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_EXTERNAL_DISPLAY_CONNECTED"].Should().Be("TRUE");
    }

    [Fact]
    public void ExternalDisplayConnectedTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new ExternalDisplayConnectedAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region ExternalDisplayDisconnectedAutomationPipelineTrigger Tests

    [Fact]
    public void ExternalDisplayDisconnectedTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new ExternalDisplayDisconnectedAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ExternalDisplayDisconnectedTrigger_UpdateEnvironment_ShouldSetExternalDisplayFalse()
    {
        var trigger = new ExternalDisplayDisconnectedAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_IS_EXTERNAL_DISPLAY_CONNECTED"].Should().Be("FALSE");
    }

    [Fact]
    public void ExternalDisplayDisconnectedTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new ExternalDisplayDisconnectedAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region WiFiDisconnectedAutomationPipelineTrigger Tests

    [Fact]
    public void WiFiDisconnectedTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new WiFiDisconnectedAutomationPipelineTrigger();
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WiFiDisconnectedTrigger_UpdateEnvironment_ShouldSetWiFiFalse()
    {
        var trigger = new WiFiDisconnectedAutomationPipelineTrigger();
        var env = new AutomationEnvironment();
        trigger.UpdateEnvironment(env);
        env.Dictionary["LLT_WIFI_CONNECTED"].Should().Be("FALSE");
    }

    [Fact]
    public void WiFiDisconnectedTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new WiFiDisconnectedAutomationPipelineTrigger();
        var copy = trigger.DeepCopy();
        copy.Should().NotBeSameAs(trigger);
    }

    #endregion

    #region UserInactivityAutomationPipelineTrigger Tests

    [Fact]
    public void UserInactivityTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new UserInactivityAutomationPipelineTrigger(TimeSpan.FromMinutes(5));
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void UserInactivityTrigger_InactivityTimeSpan_ShouldRetainValue()
    {
        var trigger = new UserInactivityAutomationPipelineTrigger(TimeSpan.FromMinutes(10));
        trigger.InactivityTimeSpan.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void UserInactivityTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new UserInactivityAutomationPipelineTrigger(TimeSpan.FromMinutes(5));
        var copy = trigger.DeepCopy(TimeSpan.FromMinutes(15));
        copy.Should().NotBeSameAs(trigger);
        copy.InactivityTimeSpan.Should().Be(TimeSpan.FromMinutes(15));
    }

    #endregion

    #region WiFiConnectedAutomationPipelineTrigger Tests

    [Fact]
    public void WiFiConnectedTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new WiFiConnectedAutomationPipelineTrigger(new[] { "HomeWiFi" });
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WiFiConnectedTrigger_Ssids_ShouldRetainValues()
    {
        var trigger = new WiFiConnectedAutomationPipelineTrigger(new[] { "Net1", "Net2" });
        trigger.Ssids.Should().HaveCount(2);
    }

    [Fact]
    public void WiFiConnectedTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new WiFiConnectedAutomationPipelineTrigger(new[] { "Net1" });
        var copy = trigger.DeepCopy(new[] { "Net2" });
        copy.Should().NotBeSameAs(trigger);
        copy.Ssids.Should().ContainSingle("Net2");
    }

    #endregion

    #region PeriodicAutomationPipelineTrigger Tests

    [Fact]
    public void PeriodicTrigger_DisplayName_ShouldNotBeNullOrEmpty()
    {
        var trigger = new PeriodicAutomationPipelineTrigger(TimeSpan.FromMinutes(1));
        trigger.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PeriodicTrigger_Period_ShouldRetainValue()
    {
        var trigger = new PeriodicAutomationPipelineTrigger(TimeSpan.FromHours(1));
        trigger.Period.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void PeriodicTrigger_DeepCopy_ShouldReturnNewInstance()
    {
        var trigger = new PeriodicAutomationPipelineTrigger(TimeSpan.FromMinutes(5));
        var copy = trigger.DeepCopy(TimeSpan.FromMinutes(10));
        copy.Should().NotBeSameAs(trigger);
        copy.Period.Should().Be(TimeSpan.FromMinutes(10));
    }

    #endregion
}