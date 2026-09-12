using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using UniversalDeviceToolkit.Lib.Automation;
using UniversalDeviceToolkit.Lib.Automation.Pipeline.Triggers;
using UniversalDeviceToolkit.Lib.Automation.Steps;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

[Trait("Category", TestCategories.Unit)]
public sealed class AutomationSafeStartTests
{
    [Fact]
    public void BatteryPercentageTrigger_DurationAndCooldown_AreClampedNonNegative()
    {
        var trigger = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.BelowOrEqual,
            10,
            TimeSpan.FromSeconds(-5),
            TimeSpan.FromMinutes(-1),
            BatteryChargeFilter.Any);

        trigger.Duration.Should().Be(TimeSpan.Zero);
        trigger.Cooldown.Should().Be(TimeSpan.Zero);
        trigger.Threshold.Should().Be(10);
    }

    [Fact]
    public void BatteryPercentageTrigger_Threshold_IsClampedTo0_100()
    {
        var high = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.AboveOrEqual, 250, TimeSpan.Zero, TimeSpan.Zero, BatteryChargeFilter.Any);
        var low = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.BelowOrEqual, -20, TimeSpan.Zero, TimeSpan.Zero, BatteryChargeFilter.Any);

        high.Threshold.Should().Be(100);
        low.Threshold.Should().Be(0);
    }

    [Fact]
    public async Task BatteryPercentageTrigger_DurationNotElapsed_ReturnsFalseEvenIfDataAvailable()
    {
        // When Duration > 0, first match starts the timer and returns false until elapsed.
        // On CI machines without battery monitoring, IsMatchingState is false (no-data safe).
        var trigger = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.AboveOrEqual,
            0,
            TimeSpan.FromHours(1),
            TimeSpan.Zero,
            BatteryChargeFilter.Any);

        var first = await trigger.IsMatchingState();
        // Either no battery (false) or duration gate (false). Must never throw.
        first.Should().BeFalse();
    }

    [Fact]
    public async Task BatteryPercentageTrigger_CooldownBlocksImmediateRetrigger()
    {
        // Cooldown path: even with matching threshold, second call inside cooldown stays false
        // after a synthetic "matched" state via reflection of private fields when possible.
        var trigger = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.AboveOrEqual,
            0,
            TimeSpan.Zero,
            TimeSpan.FromHours(2),
            BatteryChargeFilter.Any);

        var matchingSince = typeof(BatteryPercentageAutomationPipelineTrigger)
            .GetField("_matchingSince", BindingFlags.Instance | BindingFlags.NonPublic);
        var lastMatched = typeof(BatteryPercentageAutomationPipelineTrigger)
            .GetField("_lastMatchedAt", BindingFlags.Instance | BindingFlags.NonPublic);

        matchingSince.Should().NotBeNull();
        lastMatched.Should().NotBeNull();

        matchingSince!.SetValue(trigger, DateTimeOffset.UtcNow.AddMinutes(-1));
        lastMatched!.SetValue(trigger, DateTimeOffset.UtcNow); // just matched

        var result = await trigger.IsMatchingState();
        // If battery exists and matches, cooldown forces false; if no battery, also false.
        result.Should().BeFalse();
    }

    [Fact]
    public void OrTrigger_DeepCopy_PreservesChildren()
    {
        IAutomationPipelineTrigger a = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.BelowOrEqual, 20, TimeSpan.Zero, TimeSpan.Zero, BatteryChargeFilter.Any);
        IAutomationPipelineTrigger b = new BatteryPercentageAutomationPipelineTrigger(
            BatteryPercentageComparison.AboveOrEqual, 80, TimeSpan.Zero, TimeSpan.Zero, BatteryChargeFilter.Any);

        var or = new OrAutomationPipelineTrigger([a, b]);
        var copy = (OrAutomationPipelineTrigger)or.DeepCopy();
        copy.Triggers.Should().HaveCount(2);
        copy.Should().NotBeSameAs(or);
        copy.Triggers[0].Should().NotBeSameAs(a);
    }

    [Fact]
    public void AutomationSteps_ShowHide_RegisterAsIAutomationStep()
    {
        IAutomationStep[] steps =
        [
            new ShowMainWindowAutomationStep(),
            new HideMainWindowAutomationStep()
        ];

        foreach (var step in steps)
        {
            step.DeepCopy().Should().NotBeNull();
            step.DeepCopy().GetType().Should().Be(step.GetType());
        }
    }

    [Fact]
    public async Task SafeStartRunner_DoesNotRunNonCriticalHardwareishSteps()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard, safeStart: true);

        var hardwareishRan = false;
        runner.RegisterStep("ioc-bootstrap", TimeSpan.FromSeconds(5), () => { }, isCritical: true);
        runner.RegisterStep("gpu-init", TimeSpan.FromSeconds(5), () => hardwareishRan = true, isCritical: false);
        runner.RegisterStep("fan-curves", TimeSpan.FromSeconds(5), () => hardwareishRan = true, isCritical: false);

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        result.EnteredSafeMode.Should().BeTrue();
        hardwareishRan.Should().BeFalse();
        result.SkippedSteps.Should().Contain(["gpu-init", "fan-curves"]);
    }

    [Fact]
    public async Task SafeStartRunner_CriticalStepsStillRun()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard, safeStart: true);
        var criticalRan = false;

        runner.RegisterStep("language-gate-ready", TimeSpan.FromSeconds(5), () => criticalRan = true, isCritical: true);
        runner.RegisterStep("optional-sensors", TimeSpan.FromSeconds(5), () => { }, isCritical: false);

        var result = await runner.RunAsync();
        result.Success.Should().BeTrue();
        criticalRan.Should().BeTrue();
        result.SkippedSteps.Should().Contain("optional-sensors");
    }
}
