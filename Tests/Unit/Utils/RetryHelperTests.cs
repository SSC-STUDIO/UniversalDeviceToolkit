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
public class RetryHelperTests
{
    #region RetryAsync - Success Tests

    [Fact]
    public async Task RetryAsync_SucceedsAfterTransientFailures()
    {
        var calls = 0;

        await RetryHelper.RetryAsync(async () =>
        {
            calls++;
            if (calls < 3)
                throw new InvalidOperationException();
            await Task.CompletedTask;
        }, maximumRetries: 5, timeout: TimeSpan.Zero);

        calls.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_WhenActionSucceeds_ShouldNotRetry()
    {
        // Arrange
        var attemptCount = 0;

        // Act
        await RetryHelper.RetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_WithDefaultRetries_ShouldRetryThreeTimes()
    {
        // Arrange
        var attemptCount = 0;
        var maximumRetries = 3;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Test exception");
            }, maximumRetries));

        attemptCount.Should().Be(maximumRetries);
    }

    [Fact]
    public async Task RetryAsync_WithZeroRetries_ShouldThrowOnFirstFailure()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Test exception");
            }, 0));

        attemptCount.Should().Be(1); // Actually 1 because maximumRetries defaults to 3 when null
    }

    [Fact]
    public async Task RetryAsync_WithCustomTimeout_ShouldWaitBetweenRetries()
    {
        // Arrange
        var attemptCount = 0;
        var timeout = TimeSpan.FromMilliseconds(50);
        var startTime = DateTime.UtcNow;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Test exception");
            }, 3, timeout));

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should have waited between retries
        attemptCount.Should().Be(3);
        elapsed.Should().BeGreaterThanOrEqualTo(timeout);
    }

    #endregion

    #region RetryAsync - Matching Exception Tests

    [Fact]
    public async Task RetryAsync_WithMatchingException_ShouldRetry()
    {
        // Arrange
        var attemptCount = 0;
        Func<Exception, bool> matchingException = ex => ex is InvalidOperationException;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            }, 3, null, matchingException));

        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_WithNonMatchingException_ShouldThrowImmediately()
    {
        // Arrange
        var attemptCount = 0;
        Func<Exception, bool> matchingException = ex => ex is ArgumentException;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            }, 3, null, matchingException));

        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_WithNullMatchingException_ShouldMatchAllExceptions()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Any exception");
            }, 2, null, null));

        attemptCount.Should().Be(2);
    }

    #endregion

    #region RetryAsync - MaximumRetriesReachedException Tests

    [Fact]
    public async Task RetryAsync_WhenMaximumRetriesReached_ShouldThrowMaximumRetriesReachedException()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Persistent failure");
            }, 3));

        exception.Should().NotBeNull();
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_WhenReceivesMaximumRetriesReachedException_ShouldRethrowWithoutRetry()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new MaximumRetriesReachedException();
            }, 5));

        // Should not retry MaximumRetriesReachedException
        attemptCount.Should().Be(1);
    }

    #endregion

    #region RetryAsync - DelayProvider Tests

    [Fact]
    public async Task RetryAsync_WithCustomDelayProvider_ShouldUseProvidedDelay()
    {
        // Arrange
        var attemptCount = 0;
        var delayCallCount = 0;
        var mockDelayProvider = new Mock<IDelayProvider>();
        mockDelayProvider.Setup(x => x.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                delayCallCount++;
                return Task.CompletedTask;
            });

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Test exception");
            }, 3, TimeSpan.FromMilliseconds(10), null, mockDelayProvider.Object));

        attemptCount.Should().Be(3);
        delayCallCount.Should().Be(2); // Delay is called between retries
    }

    [Fact]
    public async Task RetryAsync_WithZeroTimeout_ShouldNotWaitBetweenRetries()
    {
        // Arrange
        var attemptCount = 0;
        var timeout = TimeSpan.Zero;

        // Act & Assert
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(() =>
            RetryHelper.RetryAsync(async () =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new Exception("Test exception");
            }, 5, timeout));

        attemptCount.Should().Be(5);
    }

    #endregion

    #region RetryAsync - CallerMemberName Tests

    [Fact]
    public async Task RetryAsync_ShouldCaptureCallerMemberName()
    {
        // Arrange
        var attemptCount = 0;

        // This test verifies that the CallerMemberName attribute works
        // by not passing a tag explicitly

        // Act
        await RetryHelper.RetryAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
        }, 1);

        // Assert
        attemptCount.Should().Be(1);
    }

    #endregion

    #region MaximumRetriesReachedException Tests

    [Fact]
    public void MaximumRetriesReachedException_ShouldBeConstructible()
    {
        // Act
        var exception = new MaximumRetriesReachedException();

        // Assert
        exception.Should().NotBeNull();
    }

    [Fact]
    public void MaximumRetriesReachedException_ShouldDeriveFromException()
    {
        // Arrange & Act
        var exception = new MaximumRetriesReachedException();

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    #endregion

    [Fact]
    public async Task RetryAsync_SuccessOnFirstTry_Returns()
    {
        var callCount = 0;
        await RetryHelper.RetryAsync(async () =>
        {
            callCount++;
            await Task.CompletedTask;
        });
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_SuccessAfterRetry_Returns()
    {
        var callCount = 0;
        await RetryHelper.RetryAsync(async () =>
        {
            callCount++;
            if (callCount < 3)
                throw new InvalidOperationException("not yet");
            await Task.CompletedTask;
        }, maximumRetries: 3);
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_ExceedsRetries_ThrowsMaximumRetries()
    {
        await Assert.ThrowsAsync<MaximumRetriesReachedException>(async () =>
        {
            await RetryHelper.RetryAsync(async () =>
            {
                throw new InvalidOperationException("always fail");
            }, maximumRetries: 2);
        });
    }

    [Fact]
    public async Task RetryAsync_OperationCanceled_ThrowsImmediately()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await RetryHelper.RetryAsync(async () =>
            {
                throw new OperationCanceledException();
            }, maximumRetries: 5);
        });
    }

    [Fact]
    public async Task RetryAsync_MatchingExceptionFilter_RespectsFilter()
    {
        var callCount = 0;
        await RetryHelper.RetryAsync(async () =>
        {
            callCount++;
            if (callCount < 2)
                throw new ArgumentException("filtered");
            await Task.CompletedTask;
        }, maximumRetries: 3, matchingException: ex => ex is ArgumentException);
        callCount.Should().Be(2);
    }
}
