using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ThrottleFirstDispatcherTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidInterval_ShouldInitialize()
    {
        // Arrange & Act
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));

        // Assert
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidInterval_ShouldSetInterval()
    {
        // Arrange
        var interval = TimeSpan.FromSeconds(2);

        // Act
        var dispatcher = new ThrottleFirstDispatcher(interval);

        // Assert
        dispatcher.Interval.Should().Be(interval);
    }

    [Fact]
    public void Constructor_WithTag_ShouldInitialize()
    {
        // Arrange & Act
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1), "TestTag");

        // Assert
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNegativeInterval_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ThrottleFirstDispatcher(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Constructor_WithZeroInterval_ShouldInitialize()
    {
        // Arrange & Act
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.Zero);

        // Assert
        dispatcher.Interval.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region DispatchAsync Tests

    [Fact]
    public async Task DispatchAsync_WhenFirstCall_ShouldExecuteImmediately()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));
        var executed = false;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WhenCalledWithinInterval_ShouldThrottle()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));
        var executionCount = 0;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Assert - Second call should be throttled
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenCalledAfterInterval_ShouldExecute()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromMilliseconds(50));
        var executionCount = 0;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        await Task.Delay(100);

        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Assert - Both calls should execute
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task DispatchAsync_WithZeroInterval_ShouldExecuteAllCalls()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.Zero);
        var executionCount = 0;

        // Act
        for (int i = 0; i < 10; i++)
        {
            await dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                executionCount++;
            });
        }

        // Assert - With zero interval, all calls should execute
        executionCount.Should().Be(10);
    }

    [Fact]
    public async Task DispatchAsync_WithNullTask_ShouldThrowArgumentNullException()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.DispatchAsync(null!));
    }

    #endregion

    #region ResetAsync Tests

    [Fact]
    public async Task ResetAsync_ShouldAllowNextCallToExecute()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));
        var executionCount = 0;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        await dispatcher.ResetAsync();

        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Assert - Both calls should execute after reset
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task ResetAsync_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));

        // Act
        await dispatcher.ResetAsync();
        await dispatcher.ResetAsync();
        await dispatcher.ResetAsync();

        // Assert - No exception means success
    }

    #endregion

    #region Concurrent Dispatch Tests

    [Fact]
    public async Task DispatchAsync_WhenCalledConcurrently_ShouldThrottleCorrectly()
    {
        // Arrange - Keep the window well above scheduler stalls on loaded CI runners.
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(5));
        var executionCount = 0;

        // Act - Fire all dispatches truly concurrently from different threads
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            // Use Task.Run to ensure each dispatch starts from a different thread context
            // This creates true concurrency where all dispatches contend for the lock simultaneously
            tasks[i] = Task.Run(() => dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                Interlocked.Increment(ref executionCount);
            }));
        }

        // Wait for all dispatches to complete
        await Task.WhenAll(tasks);

        // Assert - Should throttle to only allow first call
        // Note: Due to DateTime.UtcNow precision (~16ms), some concurrent calls
        // might see the same timestamp as the first call and get throttled
        executionCount.Should().Be(1);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task DispatchAsync_WhenTaskThrows_ShouldPropagateException()
    {
        // Arrange
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(1));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            }));
    }

    [Fact]
    public async Task DispatchAsync_WhenTaskThrows_ShouldStillRecordExecutionTime()
    {
        // Arrange — long window so exception unwinding plus slow CI runners cannot
        // let the second dispatch slip past the throttle (same fix as
        // DispatchAsync_MultipleCallsOverTime_ShouldOnlyExecuteFirst; 500ms flaked
        // on GitHub runners with the dispatch pair measured at 781ms).
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(5));
        var executionCount = 0;

        // Act
        try
        {
            await dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                executionCount++;
                throw new InvalidOperationException("Test exception");
            });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Next call within interval should be throttled
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Assert
        executionCount.Should().Be(1);
    }

    #endregion

    #region Interval Property Tests

    [Fact]
    public void Interval_ShouldReturnConstructorValue()
    {
        // Arrange
        var expectedInterval = TimeSpan.FromMilliseconds(500);

        // Act
        var dispatcher = new ThrottleFirstDispatcher(expectedInterval);

        // Assert
        dispatcher.Interval.Should().Be(expectedInterval);
    }

    [Fact]
    public async Task Interval_ShouldNotChangeAfterDispatch()
    {
        // Arrange
        var expectedInterval = TimeSpan.FromSeconds(1);
        var dispatcher = new ThrottleFirstDispatcher(expectedInterval);

        // Act
        await dispatcher.DispatchAsync(async () => await Task.CompletedTask);

        // Assert
        dispatcher.Interval.Should().Be(expectedInterval);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task DispatchAsync_WhenCalledImmediately_ShouldThrottle()
    {
        // Arrange - A long window makes the immediate-call contract deterministic on slow CI.
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(5));
        var executionCount = 0;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Immediately call again - should be throttled
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Assert - Only first call should execute
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_MultipleCallsOverTime_ShouldOnlyExecuteFirst()
    {
        // Arrange — long window so slow CI Task.Delay jitter cannot open a second slot.
        var dispatcher = new ThrottleFirstDispatcher(TimeSpan.FromSeconds(5));
        var executionCount = 0;

        // Act - Call multiple times with small gaps, all within the throttle window
        for (int i = 0; i < 5; i++)
        {
            await dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                Interlocked.Increment(ref executionCount);
            });
            await Task.Delay(5);
        }

        // Assert
        executionCount.Should().Be(1);
    }

    #endregion
}
