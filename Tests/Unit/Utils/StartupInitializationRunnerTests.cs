using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class StartupInitializationRunnerTests
{
    // Generous per-step budget: these tests exercise ordering and failure
    // semantics, not timeouts (covered by StartupHealthGuardTests). The guard
    // schedules finite-timeout steps via Task.Run, and on saturated CI runners
    // thread-pool scheduling can lag by whole seconds, so tight budgets flake.
    private static readonly TimeSpan StepBudget = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RunAsync_AllStepsSucceed_SuccessTrueNoFailedSteps()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard);

        runner.RegisterStep("a", StepBudget, () => { });
        runner.RegisterStep("b", StepBudget, () => { });

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        result.FailedSteps.Should().BeEmpty();
        result.EnteredSafeMode.Should().BeFalse();
        result.SkippedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_NonCriticalFailure_ContinuesAndRecordsFailure()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard);

        runner.RegisterStep("a", StepBudget, () => { });
        runner.RegisterStep("b", StepBudget, () => throw new InvalidOperationException("b-fail"),
            isCritical: false);
        runner.RegisterStep("c", StepBudget, () => { });

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        result.FailedSteps.Should().ContainSingle().Which.Should().Be("b");
        result.SkippedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_CriticalFailure_ShortCircuitsRun()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard);

        var afterRan = false;

        runner.RegisterStep("first", StepBudget,
            () => throw new InvalidOperationException("first-fail"),
            isCritical: true);
        runner.RegisterStep("after", StepBudget, () => afterRan = true);

        var result = await runner.RunAsync();

        result.Success.Should().BeFalse();
        result.FailedSteps.Should().ContainSingle().Which.Should().Be("first");
        afterRan.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_SafeStart_True_SkipsNonCriticalSteps()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard, safeStart: true);

        var nonCriticalRan = false;

        runner.RegisterStep("critical", StepBudget, () => { }, isCritical: true);
        runner.RegisterStep("optional", StepBudget,
            () => nonCriticalRan = true,
            isCritical: false);

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        result.EnteredSafeMode.Should().BeTrue();
        result.SkippedSteps.Should().ContainSingle().Which.Should().Be("optional");
        nonCriticalRan.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_SafeStart_False_RunsAllSteps()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard, safeStart: false);

        runner.RegisterStep("a", StepBudget, () => { }, isCritical: false);
        runner.RegisterStep("b", StepBudget, () => { }, isCritical: true);

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        result.EnteredSafeMode.Should().BeFalse();
        result.SkippedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_EmptyRunner_ReturnsSuccessAndNoSteps()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard);

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        result.FailedSteps.Should().BeEmpty();
        result.SkippedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_AsyncStepsExecuteInOrder()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard);

        var observed = new List<string>();

        runner.RegisterStep("a", StepBudget,
            () => observed.Add("a-sync"));
        runner.RegisterStep("b", StepBudget, async () =>
        {
            await Task.Yield();
            observed.Add("b-async");
        });

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        observed.Should().Contain("a-sync");
        observed.Should().Contain("b-async");
    }

    [Fact]
    public async Task RunAsync_PropagatesFailureCountToGuard()
    {
        var guard = new StartupHealthGuard(consecutiveFailureThreshold: 2);
        var runner = new StartupInitializationRunner(guard);

        runner.RegisterStep("a", StepBudget,
            () => throw new InvalidOperationException("a-fail"),
            isCritical: false);
        runner.RegisterStep("b", StepBudget,
            () => throw new InvalidOperationException("b-fail"),
            isCritical: false);

        var result = await runner.RunAsync();

        result.Success.Should().BeTrue();
        guard.ConsecutiveFailureCount.Should().Be(2);
        guard.ShouldEnterSafeMode.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_HonorsCancellationToken()
    {
        var guard = new StartupHealthGuard();
        var runner = new StartupInitializationRunner(guard);

        runner.RegisterStep("never", StepBudget,
            () => Thread.Sleep(TimeSpan.FromSeconds(5)));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await runner.RunAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
