using System;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ThreadSafeCounterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeToZero()
    {
        // Arrange & Act
        var counter = new ThreadSafeCounter();

        // Assert
        counter.Value.Should().Be(0);
    }

    #endregion

    #region Increment Tests

    [Fact]
    public void Increment_ShouldIncreaseCounter()
    {
        // Arrange
        var counter = new ThreadSafeCounter();

        // Act
        counter.Increment();

        // Assert
        counter.Value.Should().Be(1);
    }

    [Fact]
    public void Increment_WhenCalledMultipleTimes_ShouldIncreaseCounterMultipleTimes()
    {
        // Arrange
        var counter = new ThreadSafeCounter();

        // Act
        counter.Increment();
        counter.Increment();
        counter.Increment();

        // Assert
        counter.Value.Should().Be(3);
    }

    [Fact]
    public void Increment_ShouldAllowCounterToGoHigh()
    {
        // Arrange
        var counter = new ThreadSafeCounter();
        var increments = 100;

        // Act
        for (int i = 0; i < increments; i++)
            counter.Increment();

        // Assert
        counter.Value.Should().Be(increments);

        // Decrement all
        for (int i = 0; i < increments; i++)
            counter.Decrement();

        counter.Value.Should().Be(0);
    }

    #endregion

    #region Decrement Tests

    [Fact]
    public void Decrement_WhenCounterIsZero_ShouldStayAtZero()
    {
        // Arrange
        var counter = new ThreadSafeCounter();

        // Act
        counter.Decrement();

        // Assert
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void Decrement_WhenCalledMultipleTimesWithZero_ShouldStayAtZero()
    {
        // Arrange
        var counter = new ThreadSafeCounter();

        // Act
        counter.Decrement();
        counter.Decrement();
        counter.Decrement();

        // Assert
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void Decrement_ShouldDecreaseCounterByOne()
    {
        // Arrange
        var counter = new ThreadSafeCounter();
        counter.Increment();
        counter.Increment();

        // Act & Assert
        counter.Decrement();
        counter.Value.Should().Be(1);

        counter.Decrement();
        counter.Value.Should().Be(0);

        counter.Decrement();
        counter.Value.Should().Be(0); // Clamped at 0
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Counter_WhenAccessedConcurrently_ShouldBeThreadSafe()
    {
        // Arrange
        var counter = new ThreadSafeCounter();
        var iterations = 1000;
        for (var i = 0; i < iterations; i++)
            counter.Increment();

        var tasks = new[]
        {
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    counter.Increment();
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < iterations / 2; i++)
                    counter.Decrement();
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < iterations / 2; i++)
                    counter.Decrement();
            })
        };

        // Act
        await Task.WhenAll(tasks);

        // Assert - The initial balance prevents zero-clamp discards while operations race.
        counter.Value.Should().Be(iterations);
    }

    [Fact]
    public async Task Increment_WhenCalledFromMultipleThreads_ShouldNotOverflow()
    {
        // Arrange
        var counter = new ThreadSafeCounter();
        var tasks = new Task[10];

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                    counter.Increment();
            });
        }

        await Task.WhenAll(tasks);

        // Assert - Should have incremented 1000 times
        counter.Value.Should().Be(1000);

        // Decrement 1000 times to verify
        for (int i = 0; i < 1000; i++)
            counter.Decrement();

        // Counter should be 0 now
        counter.Value.Should().Be(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Counter_WhenAlternatingIncrementDecrement_ShouldHandleCorrectly()
    {
        // Arrange
        var counter = new ThreadSafeCounter();

        // Act
        for (int i = 0; i < 100; i++)
        {
            counter.Increment();
            counter.Decrement();
        }

        // Assert
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void Counter_WhenDecrementingMoreThanIncrementing_ShouldNotGoNegative()
    {
        // Arrange
        var counter = new ThreadSafeCounter();
        counter.Increment();
        counter.Increment();

        // Act - Decrement more times than we incremented
        counter.Decrement(); // 2 -> 1
        counter.Decrement(); // 1 -> 0
        counter.Decrement(); // 0 -> 0 (stays at 0)
        counter.Decrement(); // 0 -> 0

        // Assert
        counter.Value.Should().Be(0);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ShouldSetCounterToZero()
    {
        // Arrange
        var counter = new ThreadSafeCounter();
        counter.Increment();
        counter.Increment();
        counter.Increment();

        // Act
        counter.Reset();

        // Assert
        counter.Value.Should().Be(0);
    }

    #endregion

    [Fact]
    public void Decrement_FromZero_StaysAtZero()
    {
        var counter = new ThreadSafeCounter();
        counter.Decrement();
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void IncrementThenDecrement_ReturnsToZero()
    {
        var counter = new ThreadSafeCounter();
        counter.Increment();
        counter.Decrement();
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void DoubleDecrement_FromZero_DoesNotGoNegative()
    {
        var counter = new ThreadSafeCounter();
        counter.Decrement();
        counter.Decrement();
        counter.Value.Should().Be(0);
    }

    [Fact]
    public void ConcurrentIncrementDecrement_NoException()
    {
        var counter = new ThreadSafeCounter();
        var exceptions = new System.Collections.Generic.List<Exception>();

        Parallel.For(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            try
            {
                if (i % 2 == 0) counter.Increment();
                else counter.Decrement();
            }
            catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
        });

        exceptions.Should().BeEmpty();
    }
}
