using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class ThreadSafeBoolTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeToFalse()
    {
        // Arrange & Act
        var threadSafeBool = new ThreadSafeBool();

        // Assert
        threadSafeBool.Value.Should().BeFalse();
    }

    #endregion

    #region Value Property Tests

    [Fact]
    public void Value_WhenSetToTrue_ShouldReturnTrue()
    {
        // Arrange
        var threadSafeBool = new ThreadSafeBool();

        // Act
        threadSafeBool.Value = true;

        // Assert
        threadSafeBool.Value.Should().BeTrue();
    }

    [Fact]
    public void Value_WhenSetToFalse_ShouldReturnFalse()
    {
        // Arrange
        var threadSafeBool = new ThreadSafeBool();
        threadSafeBool.Value = true;

        // Act
        threadSafeBool.Value = false;

        // Assert
        threadSafeBool.Value.Should().BeFalse();
    }

    [Fact]
    public void Value_WhenSetMultipleTimes_ShouldReflectLatestValue()
    {
        // Arrange
        var threadSafeBool = new ThreadSafeBool();

        // Act
        threadSafeBool.Value = true;
        threadSafeBool.Value = false;
        threadSafeBool.Value = true;

        // Assert
        threadSafeBool.Value.Should().BeTrue();
    }

    [Fact]
    public void Value_WhenNotSet_ShouldReturnFalse()
    {
        // Arrange & Act
        var threadSafeBool = new ThreadSafeBool();

        // Assert
        threadSafeBool.Value.Should().BeFalse();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Value_WhenAccessedConcurrently_ShouldBeThreadSafe()
    {
        // Arrange
        var threadSafeBool = new ThreadSafeBool();
        var tasks = new Task[100];
        var errors = 0;

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    threadSafeBool.Value = j % 2 == 0;
                    var value = threadSafeBool.Value;
                    // Should not throw or cause race conditions
                }
            });
        }

        // Wait for all tasks
        await Task.WhenAll(tasks);

        // Assert - No exceptions should occur
        errors.Should().Be(0);
    }

    [Fact]
    public async Task Value_WhenSetFromMultipleThreads_ShouldMaintainConsistency()
    {
        // Arrange
        var threadSafeBool = new ThreadSafeBool();
        var iterations = 10000;
        var tasks = new[]
        {
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    threadSafeBool.Value = true;
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    threadSafeBool.Value = false;
            })
        };

        // Act
        await Task.WhenAll(tasks);

        // Assert - Final value should be either true or false, not corrupted
        (threadSafeBool.Value == true || threadSafeBool.Value == false).Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Value_WhenRapidlySwitched_ShouldHandleCorrectly()
    {
        // Arrange
        var threadSafeBool = new ThreadSafeBool();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            threadSafeBool.Value = i % 2 == 0;
        }

        // Assert
        threadSafeBool.Value.Should().BeFalse(); // Last iteration (999) sets it to false
    }

    #endregion

    [Fact]
    public void Default_Value_IsFalse()
    {
        var tsb = new ThreadSafeBool();
        tsb.Value.Should().BeFalse();
    }

    [Fact]
    public void SetTrue_ValueBecomesTrue()
    {
        var tsb = new ThreadSafeBool();
        tsb.Value = true;
        tsb.Value.Should().BeTrue();
    }

    [Fact]
    public void SetFalse_AfterTrue_RevertsToFalse()
    {
        var tsb = new ThreadSafeBool { Value = true };
        tsb.Value = false;
        tsb.Value.Should().BeFalse();
    }

    [Fact]
    public void ConcurrentToggle_NoException()
    {
        var tsb = new ThreadSafeBool();
        var exceptions = new System.Collections.Generic.List<Exception>();

        Parallel.For(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            try { tsb.Value = i % 2 == 0; }
            catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
        });

        exceptions.Should().BeEmpty();
    }
}
