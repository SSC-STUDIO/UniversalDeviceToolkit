using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Optimization;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Optimization;

public class WindowsOptimizationActionDefinitionContractTests
{
    [Fact]
    public void WindowsOptimizationActionDefinition_StoresAllConstructorArguments()
    {
        Func<CancellationToken, Task> execute = _ => Task.CompletedTask;
        Func<CancellationToken, Task>? rollback = _ => Task.CompletedTask;
        Func<CancellationToken, Task<bool>>? isApplied = _ => Task.FromResult(true);

        var action = new WindowsOptimizationActionDefinition(
            "test.const",
            "Title",
            "Description",
            execute,
            Recommended: false,
            IsAppliedAsync: isApplied,
            RollbackAsync: rollback);

        action.Key.Should().Be("test.const");
        action.TitleResourceKey.Should().Be("Title");
        action.DescriptionResourceKey.Should().Be("Description");
        action.Recommended.Should().BeFalse();
        action.ExecuteAsync.Should().BeSameAs(execute);
        action.IsAppliedAsync.Should().BeSameAs(isApplied);
        action.RollbackAsync.Should().BeSameAs(rollback);
    }

    [Fact]
    public void WindowsOptimizationActionDefinition_RollbackAsyncDefaultsToNull_WhenNotProvided()
    {
        var action = new WindowsOptimizationActionDefinition(
            "test.default",
            "Title",
            "Description",
            _ => Task.CompletedTask);

        action.RollbackAsync.Should().BeNull();
    }

    [Fact]
    public void WindowsOptimizationActionDefinition_RecommendedDefaultsToTrue_WhenNotProvided()
    {
        var action = new WindowsOptimizationActionDefinition(
            "test.recommended",
            "Title",
            "Description",
            _ => Task.CompletedTask);

        action.Recommended.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_InvokesProvidedDelegate()
    {
        var executed = false;
        var action = new WindowsOptimizationActionDefinition(
            "test.exec",
            "Title",
            "Description",
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            });

        await action.ExecuteAsync(CancellationToken.None);

        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesException()
    {
        var action = new WindowsOptimizationActionDefinition(
            "test.throw",
            "Title",
            "Description",
            _ => throw new InvalidOperationException("synthetic"));

        var act = async () => await action.ExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RollbackAsync_InvokesProvidedDelegate()
    {
        var rolledBack = false;
        var action = new WindowsOptimizationActionDefinition(
            "test.rollback",
            "Title",
            "Description",
            _ => Task.CompletedTask,
            RollbackAsync: _ =>
            {
                rolledBack = true;
                return Task.CompletedTask;
            });

        await action.RollbackAsync!(CancellationToken.None);

        rolledBack.Should().BeTrue();
    }

    [Fact]
    public async Task IsAppliedAsync_ReturnsDelegateResult_WhenProvided()
    {
        var action = new WindowsOptimizationActionDefinition(
            "test.isapplied",
            "Title",
            "Description",
            _ => Task.CompletedTask,
            IsAppliedAsync: _ => Task.FromResult(true));

        var result = await action.IsAppliedAsync!(CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_CancellationToken_IsObserved()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var action = new WindowsOptimizationActionDefinition(
            "test.cancel",
            "Title",
            "Description",
            ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var act = async () => await action.ExecuteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

public class WindowsOptimizationActionDefinitionSnapshotTests
{
    [Fact]
    public void BuiltInOptimizationActions_AllExposeRollbackBehavior()
    {
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));

        var optimizationActions = service.GetCategories()
            .Where(category => !category.Key.StartsWith("cleanup.", StringComparison.OrdinalIgnoreCase))
            .SelectMany(category => category.Actions)
            .ToList();

        optimizationActions.Should().NotBeEmpty();
        optimizationActions.Should().OnlyContain(action => action.RollbackAsync != null);
    }

    [Fact]
    public void CreateRegistryAction_PreservesOriginalValue_BeforeApplying()
    {
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));

        var category = service.GetCategories()
            .FirstOrDefault(c => c.Actions.Any(a => a.Key == "explorer.taskbar"));

        category.Should().NotBeNull("explorer.taskbar is a registry-backed action defined in the standard set");

        var action = category!.Actions.First(a => a.Key == "explorer.taskbar");
        action.RollbackAsync.Should().NotBeNull("registry-backed actions must support rollback to preserve original values");
    }

    [Fact]
    public void CreateServiceAction_PreservesOriginalStartValue_BeforeApplying()
    {
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));

        var category = service.GetCategories()
            .FirstOrDefault(c => c.Actions.Any(a => a.Key == "services.diagnostics"));

        category.Should().NotBeNull();

        var action = category!.Actions.First(a => a.Key == "services.diagnostics");
        action.RollbackAsync.Should().NotBeNull("service-backed actions must support rollback to restore original Start values");
    }

    [Fact]
    public void CreateCommandAction_ProvidesNoOpRollback_ForIrreversibleCommands()
    {
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));

        var category = service.GetCategories()
            .FirstOrDefault(c => c.Actions.Any(a => a.Key == "cleanup.tempFiles"));

        category.Should().NotBeNull();

        var action = category!.Actions.First(a => a.Key == "cleanup.tempFiles");
        action.RollbackAsync.Should().NotBeNull("command actions always expose a RollbackAsync slot");
        action.RollbackAsync!.Method.ReturnType.Should().Be<Task>("rollback delegate returns Task");
    }

    [Fact]
    public void CreateCommandAction_ProvidesNoOpRollback_ForPowerPlan()
    {
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));

        var category = service.GetCategories()
            .FirstOrDefault(c => c.Actions.Any(a => a.Key == "performance.powerPlan"));

        category.Should().NotBeNull();

        var action = category!.Actions.First(a => a.Key == "performance.powerPlan");
        action.RollbackAsync.Should().NotBeNull("powercfg has a no-op rollback delegate, not null");
    }
}
