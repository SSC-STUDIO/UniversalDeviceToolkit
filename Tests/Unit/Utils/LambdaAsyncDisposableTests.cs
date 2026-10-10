using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class LambdaAsyncDisposableTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidAction_ShouldInitialize()
    {
        // Arrange & Act
        var disposed = false;
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            disposed = true;
        });

        // Assert
        disposable.Should().NotBeNull();
        disposed.Should().BeFalse();
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_ShouldInvokeAction()
    {
        // Arrange
        var disposed = false;
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            disposed = true;
        });

        // Act
        await disposable.DisposeAsync();

        // Assert
        disposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldInvokeActionMultipleTimes()
    {
        // Arrange
        var disposeCount = 0;
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            disposeCount++;
        });

        // Act
        await disposable.DisposeAsync();
        await disposable.DisposeAsync();
        await disposable.DisposeAsync();

        // Assert
        disposeCount.Should().Be(3);
    }

    [Fact]
    public async Task DisposeAsync_WithComplexAction_ShouldExecuteCorrectly()
    {
        // Arrange
        var state = new TestState();
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            state.Counter++;
            state.Message = "Disposed";
        });

        // Act
        await disposable.DisposeAsync();

        // Assert
        state.Counter.Should().Be(1);
        state.Message.Should().Be("Disposed");
    }

    [Fact]
    public async Task DisposeAsync_ShouldSuppressFinalize()
    {
        // Arrange
        var disposed = false;
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            disposed = true;
        });

        // Act
        await disposable.DisposeAsync();

        // Assert
        disposed.Should().BeTrue();
    }

    #endregion

    #region Async Pattern Tests

    [Fact]
    public async Task LambdaAsyncDisposable_WhenUsedInAwaitUsingStatement_ShouldDisposeAutomatically()
    {
        // Arrange
        var disposed = false;

        // Act
        await using (var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            disposed = true;
        }))
        {
            // Do some work
            disposed.Should().BeFalse();
        }

        // Assert
        disposed.Should().BeTrue();
    }

    [Fact]
    public async Task LambdaAsyncDisposable_WhenUsedInAwaitUsingStatementWithException_ShouldStillDispose()
    {
        // Arrange
        var disposed = false;

        // Act
        try
        {
            await using (var disposable = new LambdaAsyncDisposable(async () =>
            {
                await Task.Delay(1);
                disposed = true;
            }))
            {
                throw new InvalidOperationException("Test exception");
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        disposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_WithAsyncDelay_ShouldWaitForCompletion()
    {
        // Arrange
        var disposed = false;
        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDisposeToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var disposable = new LambdaAsyncDisposable(async () =>
        {
            disposeStarted.SetResult();
            await allowDisposeToComplete.Task;
            disposed = true;
        });

        // Act
        var disposeTask = disposable.DisposeAsync().AsTask();
        await disposeStarted.Task;

        // Assert
        disposeTask.IsCompleted.Should().BeFalse();
        disposed.Should().BeFalse();

        allowDisposeToComplete.SetResult();
        await disposeTask;
        disposed.Should().BeTrue();
    }

    #endregion

    #region Action Execution Tests

    [Fact]
    public async Task DisposeAsync_WithActionThatThrows_ShouldPropagateException()
    {
        // Arrange
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Test exception");
        });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => disposable.DisposeAsync().AsTask());
        exception.Message.Should().Be("Test exception");
    }

    [Fact]
    public async Task DisposeAsync_WithMultipleAsyncActions_ShouldExecuteInOrder()
    {
        // Arrange
        var executionOrder = new System.Collections.Generic.List<int>();
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            executionOrder.Add(1);
            await Task.Delay(1);
            executionOrder.Add(2);
            await Task.Delay(1);
            executionOrder.Add(3);
        });

        // Act
        await disposable.DisposeAsync();

        // Assert
        executionOrder.Should().ContainInOrder(1, 2, 3);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrently_ShouldExecuteAllCalls()
    {
        // Arrange
        var disposeCount = 0;
        var disposable = new LambdaAsyncDisposable(async () =>
        {
            await Task.Delay(1);
            Interlocked.Increment(ref disposeCount);
        });

        // Act
        var tasks = new[]
        {
            disposable.DisposeAsync().AsTask(),
            disposable.DisposeAsync().AsTask(),
            disposable.DisposeAsync().AsTask()
        };

        await Task.WhenAll(tasks);

        // Assert
        disposeCount.Should().Be(3);
    }

    #endregion

    #region Helper Classes

    private class TestState
    {
        public int Counter { get; set; }
        public string Message { get; set; } = "";
    }

    #endregion
}
