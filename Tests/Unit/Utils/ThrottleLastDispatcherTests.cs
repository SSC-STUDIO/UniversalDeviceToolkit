using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ThrottleLastDispatcherTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidInterval_ShouldInitialize()
    {
        // Arrange & Act
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1));

        // Assert
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTag_ShouldInitialize()
    {
        // Arrange & Act
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1), "TestTag");

        // Assert
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomDelayProvider_ShouldInitialize()
    {
        // Arrange
        var mockDelayProvider = new Mock<IDelayProvider>();

        // Act
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1), null, mockDelayProvider.Object);

        // Assert
        dispatcher.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNegativeInterval_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Constructor_WithZeroInterval_ShouldInitialize()
    {
        // Arrange & Act
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.Zero);

        // Assert
        dispatcher.Should().NotBeNull();
    }

    #endregion

    #region DispatchAsync Tests

    [Fact]
    public async Task DispatchAsync_WhenFirstCall_ShouldExecuteAfterInterval()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50));
        var executed = false;
        var startTime = DateTime.UtcNow;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executed = true;
        });

        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        executed.Should().BeTrue();
        elapsedMs.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public async Task DispatchAsync_WhenCalledMultipleTimes_ShouldExecuteOnlyLast()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100));
        var lastExecutedValue = -1;

        // Act - Fire multiple calls rapidly
        Task? last = null;
        for (int i = 0; i < 5; i++)
        {
            var value = i;
            last = dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(10);
                lastExecutedValue = value;
            });
        }

        // Wait for all to complete
        if (last != null) await last;

        // Assert - Only the last call should have executed
        lastExecutedValue.Should().Be(4);
    }

    [Fact]
    public async Task DispatchAsync_WithZeroInterval_ShouldExecuteImmediately()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.Zero);
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
    public async Task DispatchAsync_WithNullTask_ShouldThrowArgumentNullException()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.DispatchAsync(null!));
    }

    #endregion

    #region Throttling Behavior Tests

    [Fact]
    public async Task DispatchAsync_RapidCalls_ShouldCancelPreviousCalls()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100));
        var executionOrder = new System.Collections.Generic.List<int>();

        // Act
        var tasks = new System.Collections.Generic.List<Task>();
        for (int i = 0; i < 5; i++)
        {
            var value = i;
            tasks.Add(dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(10);
                lock (executionOrder) executionOrder.Add(value);
            }));
        }

        // Wait for completion
        await Task.WhenAll(tasks);

        // Assert - Only the last call should have executed
        executionOrder.Should().ContainSingle().Which.Should().Be(4);
    }

    [Fact]
    public async Task DispatchAsync_WhenPreviousCallCancelled_ShouldNotThrow()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100));
        var executionCount = 0;

        // Act: rapid triggers; await all dispatches so completion is completely deterministic.
        var tasks = new System.Collections.Generic.List<Task>();
        for (var i = 0; i < 3; i++)
        {
            tasks.Add(dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                Interlocked.Increment(ref executionCount);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert: only the winning dispatch runs the callback
        executionCount.Should().Be(1);
    }

    #endregion

    #region Custom Delay Provider Tests

    [Fact]
    public async Task DispatchAsync_WithCustomDelayProvider_ShouldUseProvider()
    {
        // Arrange
        var delayCallCount = 0;
        var mockDelayProvider = new Mock<IDelayProvider>();
        mockDelayProvider.Setup(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref delayCallCount);
                await Task.Delay(10);
            });

        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50), null, mockDelayProvider.Object);
        var executed = false;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
        delayCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WithCustomDelayProvider_ShouldDelayCorrectly()
    {
        // Arrange
        var mockDelayProvider = new Mock<IDelayProvider>();
        mockDelayProvider.Setup(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100), null, mockDelayProvider.Object);
        var executed = false;

        // Act
        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executed = true;
        });

        // Assert
        executed.Should().BeTrue();
        mockDelayProvider.Verify(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_ShouldNotThrow()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1));

        // Act
        dispatcher.Dispose();

        // Assert - No exception means success
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1));

        // Act
        dispatcher.Dispose();
        dispatcher.Dispose();
        dispatcher.Dispose();

        // Assert - No exception means success
    }

    [Fact]
    public async Task DispatchAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1));
        dispatcher.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            dispatcher.DispatchAsync(async () => await Task.CompletedTask));
    }

    [Fact]
    public async Task Dispose_WhileWaiting_ShouldCancelPendingCall()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(10));
        var executed = false;

        // Act - Start a dispatch that will wait
        _ = dispatcher.DispatchAsync(async () =>
        {
            await Task.Delay(1000);
            executed = true;
        });

        // Dispose immediately
        dispatcher.Dispose();

        // Wait a bit
        await Task.Delay(100);

        // Assert - The call should have been cancelled
        executed.Should().BeFalse();
    }

    #endregion

    #region Concurrent Dispatch Tests

    [Fact]
    public async Task DispatchAsync_WhenCalledConcurrently_ShouldThrottleCorrectly()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100));
        var executionCount = 0;

        // Act
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref executionCount);
            }));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(200); // Wait for throttling to complete

        // Assert - Should execute only the last call
        executionCount.Should().Be(1);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task DispatchAsync_WhenTaskThrows_ShouldPropagateException()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            }));
    }

    [Fact]
    public async Task DispatchAsync_WhenPreviousCallThrows_ShouldStillExecuteNext()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50));
        var executionCount = 0;

        // Act
        try
        {
            await dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            });
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        await dispatcher.DispatchAsync(async () =>
        {
            await Task.CompletedTask;
            executionCount++;
        });

        // Assert
        executionCount.Should().Be(1);
    }

    #endregion

    #region IDelayProvider Interface Tests

    [Fact]
    public async Task DispatchAsync_WithNullDelayProvider_ShouldUseDefaultDelay()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50), null, null);
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

    #endregion

    #region Using Pattern Tests

    [Fact]
    public void ThrottleLastDispatcher_WhenUsedInUsingStatement_ShouldDispose()
    {
        // Arrange & Act
        using (var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1)))
        {
            dispatcher.Should().NotBeNull();
        }

        // Assert - Dispose was called without exception
    }

    [Fact]
    public void ThrottleLastDispatcher_WhenUsedInUsingStatementWithException_ShouldStillDispose()
    {
        // Arrange
        var disposed = false;

        // Act
        try
        {
            using (var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromSeconds(1)))
            {
                disposed = true;
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

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task DispatchAsync_WithVeryShortInterval_ShouldThrottle()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50));
        var executionCount = 0;

        // Act - Fire multiple calls rapidly
        Task? last = null;
        for (int i = 0; i < 10; i++)
        {
            last = dispatcher.DispatchAsync(async () =>
            {
                await Task.CompletedTask;
                Interlocked.Increment(ref executionCount);
            });
        }

        if (last != null) await last;

        // Assert - Should only execute the last call
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WithLongInterval_ShouldThrottle()
    {
        // Arrange
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100));
        var lastValue = -1;

        // Act
        var tasks = new System.Collections.Generic.List<Task>();
        for (int i = 0; i < 3; i++)
        {
            var value = i;
            tasks.Add(dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(10);
                lastValue = value;
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        lastValue.Should().Be(2);
    }

    #endregion
}
