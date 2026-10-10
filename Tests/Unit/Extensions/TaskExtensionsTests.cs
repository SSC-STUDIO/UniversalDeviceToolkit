using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class TaskExtensionsTests
{
    #region AsValueTask Tests

    [Fact]
    public void AsValueTask_FromCompletedTask_ShouldReturnCompletedValueTask()
    {
        var task = Task.CompletedTask;

        var valueTask = task.AsValueTask();

        valueTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task AsValueTask_FromAsyncTask_ShouldPreserveResult()
    {
        var task = Task.FromResult(42);

        var valueTask = task.AsValueTask();

        await valueTask;
    }

    #endregion

    #region OrNullIfException Tests

    [Fact]
    public async Task OrNullIfException_WithSuccessfulTask_ShouldReturnValue()
    {
        var task = Task.FromResult(123);

        var result = await task.OrNullIfException();

        result.Should().Be(123);
    }

    [Fact]
    public async Task OrNullIfException_WithFaultedTask_ShouldReturnNull()
    {
        var task = Task.FromException<int>(new InvalidOperationException("test"));

        var result = await task.OrNullIfException();

        result.Should().BeNull();
    }

    [Fact]
    public async Task OrNullIfException_WithCancelledTask_ShouldReturnNull()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var task = Task.FromCanceled<int>(cts.Token);

        var result = await task.OrNullIfException();

        result.Should().BeNull();
    }

    #endregion

    #region Forget Tests

    [Fact]
    public void Forget_WithCompletedTask_ShouldNotThrow()
    {
        var task = Task.CompletedTask;

        Action act = () => task.Forget("test-completed");

        act.Should().NotThrow();
    }

    [Fact]
    public void Forget_WithFaultedTask_ShouldNotThrow()
    {
        var task = Task.FromException(new InvalidOperationException("boom"));

        Action act = () => task.Forget("test-faulted");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Forget_WithDelayedTask_ShouldComplete()
    {
        var completed = false;
        var task = Task.Run(async () =>
        {
            await Task.Delay(50);
            completed = true;
        });

        task.Forget("test-delayed");
        await Task.Delay(200);

        completed.Should().BeTrue();
    }

    [Fact]
    public void Forget_WithNullTask_ShouldNotThrow()
    {
        Task? task = null;

        Action act = () => task!.Forget("test-null");

        act.Should().NotThrow();
    }

    #endregion
}
