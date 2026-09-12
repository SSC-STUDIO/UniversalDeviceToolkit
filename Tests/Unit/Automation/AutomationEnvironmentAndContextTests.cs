using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Trait("Category", TestCategories.Unit)]
public class AutomationEnvironmentAndContextTests
{
    #region AutomationContext Tests

    [Fact]
    public void AutomationContext_Defaults_ShouldBeNull()
    {
        var ctx = new AutomationContext();
        ctx.LastRunOutput.Should().BeNull();
    }

    [Fact]
    public void AutomationContext_SetOutput_ShouldRetainValue()
    {
        var ctx = new AutomationContext { LastRunOutput = "output text" };
        ctx.LastRunOutput.Should().Be("output text");
    }

    #endregion

    #region AutomationEnvironment Boolean Setters Tests

    [Fact]
    public void AutomationEnvironment_AcAdapterConnected_True_ShouldSetTrue()
    {
        var env = new AutomationEnvironment();
        env.AcAdapterConnected = true;
        env.Dictionary.Should().ContainKey("LLT_IS_AC_ADAPTER_CONNECTED");
        env.Dictionary["LLT_IS_AC_ADAPTER_CONNECTED"].Should().Be("TRUE");
        env.Dictionary.Should().ContainKey("UDT_IS_AC_ADAPTER_CONNECTED");
        env.Dictionary["UDT_IS_AC_ADAPTER_CONNECTED"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_AcAdapterConnected_False_ShouldSetFalse()
    {
        var env = new AutomationEnvironment();
        env.AcAdapterConnected = false;
        env.Dictionary["LLT_IS_AC_ADAPTER_CONNECTED"].Should().Be("FALSE");
    }

    [Fact]
    public void AutomationEnvironment_LowPowerAcAdapter_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.LowPowerAcAdapter = true;
        env.Dictionary["LLT_IS_AC_ADAPTER_LOW_POWER"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_DisplayOn_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.DisplayOn = true;
        env.Dictionary["LLT_IS_DISPLAY_ON"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_ExternalDisplayConnected_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.ExternalDisplayConnected = true;
        env.Dictionary["LLT_IS_EXTERNAL_DISPLAY_CONNECTED"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_GameRunning_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.GameRunning = true;
        env.Dictionary["LLT_IS_GAME_RUNNING"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_HDROn_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.HDROn = true;
        env.Dictionary["LLT_IS_HDR_ON"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_LidOpen_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.LidOpen = true;
        env.Dictionary["LLT_IS_LID_OPEN"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_Startup_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.Startup = true;
        env.Dictionary["LLT_STARTUP"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_Resume_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.Resume = true;
        env.Dictionary["LLT_RESUME"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_ProcessesStarted_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.ProcessesStarted = true;
        env.Dictionary["LLT_PROCESSES_STARTED"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_DeviceConnected_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.DeviceConnected = true;
        env.Dictionary["LLT_DEVICE_CONNECTED"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_IsSunset_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.IsSunset = true;
        env.Dictionary["LLT_IS_SUNSET"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_IsSunrise_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.IsSunrise = true;
        env.Dictionary["LLT_IS_SUNRISE"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_UserActive_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.UserActive = true;
        env.Dictionary["LLT_IS_USER_ACTIVE"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_WiFiConnected_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.WiFiConnected = true;
        env.Dictionary["LLT_WIFI_CONNECTED"].Should().Be("TRUE");
    }

    [Fact]
    public void AutomationEnvironment_SessionLocked_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.SessionLocked = true;
        env.Dictionary["LLT_SESSION_LOCKED"].Should().Be("TRUE");
    }

    #endregion

    #region AutomationEnvironment PowerMode Tests

    [Theory]
    [InlineData(PowerModeState.Quiet, "1", "QUIET")]
    [InlineData(PowerModeState.Balance, "2", "BALANCE")]
    [InlineData(PowerModeState.Performance, "3", "PERFORMANCE")]
    [InlineData(PowerModeState.GodMode, "255", "CUSTOM")]
    public void AutomationEnvironment_PowerMode_ShouldSetCorrectly(PowerModeState mode, string expectedCode, string expectedName)
    {
        var env = new AutomationEnvironment();
        env.PowerMode = mode;
        env.Dictionary["LLT_POWER_MODE"].Should().Be(expectedCode);
        env.Dictionary["LLT_POWER_MODE_NAME"].Should().Be(expectedName);
        env.Dictionary["UDT_POWER_MODE"].Should().Be(expectedCode);
        env.Dictionary["UDT_POWER_MODE_NAME"].Should().Be(expectedName);
    }

    #endregion

    #region AutomationEnvironment String/Array Setters Tests

    [Fact]
    public void AutomationEnvironment_WiFiSsid_ShouldSetCorrectly()
    {
        var env = new AutomationEnvironment();
        env.WiFiSsid = "MyNetwork";
        env.Dictionary["LLT_WIFI_SSID"].Should().Be("MyNetwork");
    }

    [Fact]
    public void AutomationEnvironment_WiFiSsid_Null_ShouldSetNull()
    {
        var env = new AutomationEnvironment();
        env.WiFiSsid = null;
        env.Dictionary["LLT_WIFI_SSID"].Should().BeNull();
    }

    [Fact]
    public void AutomationEnvironment_DeviceInstanceIds_ShouldJoinWithComma()
    {
        var env = new AutomationEnvironment();
        env.DeviceInstanceIds = new[] { "id1", "id2", "id3" };
        env.Dictionary["LLT_DEVICE_INSTANCE_IDS"].Should().Be("id1,id2,id3");
    }

    [Fact]
    public void AutomationEnvironment_DeviceInstanceIds_Empty_ShouldSetNull()
    {
        var env = new AutomationEnvironment();
        env.DeviceInstanceIds = Array.Empty<string>();
        env.Dictionary["LLT_DEVICE_INSTANCE_IDS"].Should().BeEmpty();
    }

    [Fact]
    public void AutomationEnvironment_Time_ShouldFormatCorrectly()
    {
        var env = new AutomationEnvironment();
        env.Time = new Time(14, 30);
        env.Dictionary["LLT_TIME"].Should().Be("14:30");
    }

    [Fact]
    public void AutomationEnvironment_Time_Null_ShouldSetNull()
    {
        var env = new AutomationEnvironment();
        env.Time = null;
        env.Dictionary["LLT_TIME"].Should().BeNull();
    }

    [Fact]
    public void AutomationEnvironment_Days_ShouldJoinUppercase()
    {
        var env = new AutomationEnvironment();
        env.Days = new[] { DayOfWeek.Monday, DayOfWeek.Friday };
        env.Dictionary["LLT_DAYS"].Should().Be("MONDAY,FRIDAY");
    }

    [Fact]
    public void AutomationEnvironment_Days_Empty_ShouldSetNull()
    {
        var env = new AutomationEnvironment();
        env.Days = Array.Empty<DayOfWeek>();
        env.Dictionary["LLT_DAYS"].Should().BeNull();
    }

    [Fact]
    public void AutomationEnvironment_Period_ShouldFormatAsSeconds()
    {
        var env = new AutomationEnvironment();
        env.Period = TimeSpan.FromMinutes(5);
        env.Dictionary["LLT_PERIOD"].Should().Be("300");
    }

    [Fact]
    public void AutomationEnvironment_Processes_ShouldJoinNames()
    {
        var env = new AutomationEnvironment();
        var processes = new[]
        {
            new ProcessInfo("game.exe", null),
            new ProcessInfo("browser.exe", null)
        };
        env.Processes = processes;
        env.Dictionary["LLT_PROCESSES"].Should().Be("game.exe,browser.exe");
    }

    #endregion

    #region AutomationEnvironment UDT Alias Tests

    [Fact]
    public void AutomationEnvironment_EverySet_ShouldCreateUdtAlias()
    {
        var env = new AutomationEnvironment();
        env.AcAdapterConnected = true;
        env.Dictionary.Should().ContainKey("UDT_IS_AC_ADAPTER_CONNECTED");
        env.GameRunning = true;
        env.Dictionary.Should().ContainKey("UDT_IS_GAME_RUNNING");
        env.HDROn = true;
        env.Dictionary.Should().ContainKey("UDT_IS_HDR_ON");
    }

    #endregion

    #region AutomationSettingsStore Defaults Tests

    [Fact]
    public void AutomationSettingsStore_Defaults_ShouldHaveExpectedValues()
    {
        var store = new AutomationSettings.AutomationSettingsStore();
        store.IsEnabled.Should().BeFalse();
        store.Pipelines.Should().BeEmpty();
    }

    #endregion
}