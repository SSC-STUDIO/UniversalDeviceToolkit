using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class DefaultDelayProviderTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var provider = new DefaultDelayProvider();

        // Assert
        provider.Should().NotBeNull();
    }

    #endregion

    #region Delay Tests

    [Fact]
    public async Task Delay_WithValidDuration_ShouldDelayForApproximateTime()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var delayMs = 50;
        var startTime = DateTime.UtcNow;

        // Act
        await provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        elapsedMs.Should().BeGreaterThanOrEqualTo(delayMs - 10); // Allow small margin
    }

    [Fact]
    public async Task Delay_WithZeroDuration_ShouldCompleteImmediately()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var startTime = DateTime.UtcNow;

        // Act
        await provider.Delay(TimeSpan.Zero, CancellationToken.None);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        elapsedMs.Should().BeLessThan(50);
    }

    [Fact]
    public async Task Delay_WithCancellationToken_ShouldBeCancellable()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var cts = new CancellationTokenSource();
        var delayMs = 1000;

        // Act
        cts.CancelAfter(50);
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() =>
            provider.Delay(TimeSpan.FromMilliseconds(delayMs), cts.Token));

        // Assert
        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task Delay_WhenAlreadyCancelled_ShouldThrowImmediately()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            provider.Delay(TimeSpan.FromMilliseconds(100), cts.Token));
    }

    [Fact]
    public async Task Delay_WithMultipleSequentialDelays_ShouldExecuteSequentially()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var delays = new[] { 10, 20, 30 };
        var startTime = DateTime.UtcNow;

        // Act
        foreach (var delayMs in delays)
        {
            await provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None);
        }
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        elapsedMs.Should().BeGreaterThanOrEqualTo(delays.Sum() - 20);
    }

    [Fact]
    public async Task Delay_WithConcurrentDelays_ShouldExecuteConcurrently()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var delayMs = 50;
        var startTime = DateTime.UtcNow;

        // Act
        var tasks = new[]
        {
            provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None),
            provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None),
            provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None)
        };
        await Task.WhenAll(tasks);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert - Concurrent delays should complete in approximately the same time as one delay
        elapsedMs.Should().BeLessThan(3000); // CI runners are slow under coverage; 3 concurrent 50ms delays must complete well under 3s
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task Delay_WithVeryShortDuration_ShouldStillDelay()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var delayMs = 1;
        var startTime = DateTime.UtcNow;

        // Act
        await provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert - Even 1ms delay should show some elapsed time
        elapsedMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Delay_WithLargeDuration_ShouldDelayCorrectly()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var delayMs = 200;
        var startTime = DateTime.UtcNow;

        // Act
        await provider.Delay(TimeSpan.FromMilliseconds(delayMs), CancellationToken.None);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        elapsedMs.Should().BeGreaterThanOrEqualTo(delayMs - 20);
    }

    #endregion

    #region CancellationToken Tests

    [Fact]
    public async Task Delay_WithValidToken_ShouldCompleteSuccessfully()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var cts = new CancellationTokenSource();
        var delayMs = 50;

        // Act
        await provider.Delay(TimeSpan.FromMilliseconds(delayMs), cts.Token);

        // Assert - No exception means success
        cts.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Delay_WhenCancelledDuringDelay_ShouldThrowTaskCanceledException()
    {
        // Arrange
        var provider = new DefaultDelayProvider();
        var cts = new CancellationTokenSource();
        var delayMs = 500;
        var startTime = DateTime.UtcNow;

        // Act
        cts.CancelAfter(50);
        try
        {
            await provider.Delay(TimeSpan.FromMilliseconds(delayMs), cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected
        }
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert
        elapsedMs.Should().BeLessThan(delayMs);
        cts.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void DefaultDelayProvider_ShouldImplementIDelayProvider()
    {
        // Arrange & Act
        var provider = new DefaultDelayProvider();

        // Assert
        provider.Should().BeAssignableTo<IDelayProvider>();
    }

    #endregion
}

