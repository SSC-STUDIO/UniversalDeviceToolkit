using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using UniversalDeviceToolkit.Lib.System;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Trait("Category", TestCategories.Unit)]
public sealed class AutomationModelAndNotificationPolicyTests
{
    [Fact]
    public void BatteryPercentageTrigger_SerializesRoundTrip()
    {
        var trigger = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.BelowOrEqual,
            25,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMinutes(2),
            BatteryChargeFilter.Discharging);

        var copy = (BatteryPercentageAutomationPipelineTrigger)trigger.DeepCopy();
        copy.Comparison.Should().Be(BatteryPercentageComparison.BelowOrEqual);
        copy.Threshold.Should().Be(25);
        copy.Duration.Should().Be(TimeSpan.FromSeconds(3));
        copy.Cooldown.Should().Be(TimeSpan.FromMinutes(2));
        copy.ChargeFilter.Should().Be(BatteryChargeFilter.Discharging);
        copy.Should().NotBeSameAs(trigger);
    }

    [SkippableFact]
    public async Task BatteryPercentageTrigger_NoData_ReturnsFalseSafely()
    {
        // DeepCopy path does not throw; IsMatchingState must tolerate missing battery by returning false.
        Skip.If(Battery.IsBatteryMonitoringSupported(), "battery monitoring is available on this machine; the no-data path cannot be exercised");

        var trigger = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.AboveOrEqual,
            50,
            TimeSpan.Zero,
            TimeSpan.Zero,
            BatteryChargeFilter.Any);

        var result = await trigger.IsMatchingState();
        result.Should().BeFalse();
    }

    [Fact]
    public void NotificationTypePolicyStore_Defaults_AreStable()
    {
        var defaults = NotificationTypePolicyStore.CreateDefaults();
        defaults.Should().ContainKey("UpdateAvailable");
        defaults["UpdateAvailable"].Enabled.Should().BeTrue();
        defaults["CapsNumLock"].Enabled.Should().BeFalse();
        defaults["AutomationNotification"].Severity.Should().Be(NotificationPriority.Normal);
        defaults["AutomationNotification"].Persist.Should().BeFalse();

        var missing = NotificationTypePolicyStore.GetOrDefault(null, "Missing", legacyEnabled: false);
        missing.Enabled.Should().BeFalse();
        missing.Persist.Should().BeFalse();
    }

    [Fact]
    public void GetOrDefault_ReturnsCopy_SoMutatingResultDoesNotChangeStore()
    {
        var policies = NotificationTypePolicyStore.CreateDefaults();
        var policy = NotificationTypePolicyStore.GetOrDefault(policies, "CapsNumLock");
        policy.Enabled = true;
        policies["CapsNumLock"].Enabled.Should().BeFalse();
    }

    [Fact]
    public void GetOrDefault_LooksUpPolicyKeysCaseInsensitively()
    {
        var policies = new Dictionary<string, NotificationTypePolicy>
        {
            ["capsnumlock"] = new() { Enabled = true, Persist = true, Severity = NotificationPriority.High }
        };

        var policy = NotificationTypePolicyStore.GetOrDefault(policies, "CapsNumLock", legacyEnabled: false);
        policy.Enabled.Should().BeTrue();
        policy.Persist.Should().BeTrue();
        policy.Severity.Should().Be(NotificationPriority.High);
    }

    [Fact]
    public void EnsurePolicies_SyncsEnabledFromLegacyTogglesAndFillsMissingKeys()
    {
        var notifications = new ApplicationSettings.Notifications
        {
            CapsNumLock = true,
            PowerMode = true,
            TypePolicies = new Dictionary<string, NotificationTypePolicy>
            {
                ["CapsNumLock"] = new() { Enabled = false, Persist = true, Severity = NotificationPriority.High }
            }
        };

        var policies = NotificationTypePolicyStore.EnsurePolicies(notifications);

        policies["CapsNumLock"].Enabled.Should().BeTrue();
        policies["CapsNumLock"].Persist.Should().BeTrue();
        policies["CapsNumLock"].Severity.Should().Be(NotificationPriority.High);
        policies["PowerMode"].Enabled.Should().BeTrue();
        policies["UpdateAvailable"].Enabled.Should().BeTrue();
        policies.ContainsKey("automationnotification").Should().BeTrue();
    }

    [Fact]
    public void Resolve_UsesLegacyToggleForEnabledEvenWhenPolicyEnabledDiffers()
    {
        var notifications = new ApplicationSettings.Notifications
        {
            CameraLock = false,
            TypePolicies = new Dictionary<string, NotificationTypePolicy>
            {
                ["CameraLock"] = new() { Enabled = true, Persist = true, Severity = NotificationPriority.Low }
            }
        };

        var policy = NotificationTypePolicyStore.Resolve(notifications, "CameraLock");
        policy.Enabled.Should().BeFalse();
        policy.Persist.Should().BeTrue();
        policy.Severity.Should().Be(NotificationPriority.Low);
    }

    [Fact]
    public void ShouldShow_RespectsGlobalSuppressAndLegacyCategoryToggle()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore
        {
            DontShowNotifications = true,
            Notifications = { CameraLock = true }
        };
        NotificationTypePolicyStore.ShouldShow(store, NotificationType.CameraOn).Should().BeFalse();

        store.DontShowNotifications = false;
        store.Notifications.CameraLock = false;
        NotificationTypePolicyStore.EnsurePolicies(store.Notifications);
        NotificationTypePolicyStore.ShouldShow(store, NotificationType.CameraOff).Should().BeFalse();

        store.Notifications.CameraLock = true;
        NotificationTypePolicyStore.ShouldShow(store, NotificationType.CameraOn).Should().BeTrue();
    }

    [Fact]
    public void ToPolicyKey_AllNotificationTypes_MapToKnownPolicies()
    {
        var defaults = NotificationTypePolicyStore.CreateDefaults();
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            var key = NotificationTypePolicyStore.ToPolicyKey(type);
            defaults.ContainsKey(key).Should().BeTrue($"unmapped notification type {type} produced policy key {key}");
        }
    }

    [Theory]
    [InlineData(NotificationType.CapsLockOn, "CapsNumLock")]
    [InlineData(NotificationType.NumLockOff, "CapsNumLock")]
    [InlineData(NotificationType.ACAdapterConnectedLowWattage, "ACAdapter")]
    [InlineData(NotificationType.ITSModeGeek, "PowerMode")]
    [InlineData(NotificationType.WhiteKeyboardBacklightOff, "KeyboardBacklight")]
    [InlineData(NotificationType.SmartKeyDoublePress, "SmartKey")]
    public void ToPolicyKey_MapsGroupedTypes(NotificationType type, string expectedKey)
    {
        NotificationTypePolicyStore.ToPolicyKey(type).Should().Be(expectedKey);
    }

    [Fact]
    public void ApplicationSettings_NotificationPosition_DefaultsBottomRight()
    {
        var store = new ApplicationSettings.ApplicationSettingsStore();
        store.NotificationPosition.Should().Be(NotificationPosition.BottomRight);
        store.Notifications.TypePolicies.Should().NotBeEmpty();
    }

    [Fact]
    public void ShowHideMainWindowSteps_DeepCopy()
    {
        IAutomationStep show = new ShowMainWindowAutomationStep();
        IAutomationStep hide = new HideMainWindowAutomationStep();
        show.DeepCopy().Should().BeOfType<ShowMainWindowAutomationStep>();
        hide.DeepCopy().Should().BeOfType<HideMainWindowAutomationStep>();
    }
}
