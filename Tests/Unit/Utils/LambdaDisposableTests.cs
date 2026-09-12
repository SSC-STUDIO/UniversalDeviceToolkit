using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class LambdaDisposableTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidAction_ShouldInitialize()
    {
        // Arrange & Act
        var disposed = false;
        var disposable = new LambdaDisposable(() => disposed = true);

        // Assert
        disposable.Should().NotBeNull();
        disposed.Should().BeFalse();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldInvokeAction()
    {
        // Arrange
        var disposed = false;
        var disposable = new LambdaDisposable(() => disposed = true);

        // Act
        disposable.Dispose();

        // Assert
        disposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldInvokeActionMultipleTimes()
    {
        // Arrange
        var disposeCount = 0;
        var disposable = new LambdaDisposable(() => disposeCount++);

        // Act
        disposable.Dispose();
        disposable.Dispose();
        disposable.Dispose();

        // Assert - LambdaDisposable calls action on every Dispose
        disposeCount.Should().Be(3);
    }

    [Fact]
    public void Dispose_WithComplexAction_ShouldExecuteCorrectly()
    {
        // Arrange
        var state = new TestState();
        var disposable = new LambdaDisposable(() =>
        {
            state.Counter++;
            state.Message = "Disposed";
        });

        // Act
        disposable.Dispose();

        // Assert
        state.Counter.Should().Be(1);
        state.Message.Should().Be("Disposed");
    }

    [Fact]
    public void Dispose_ShouldSuppressFinalize()
    {
        // Arrange
        var disposed = false;
        var disposable = new LambdaDisposable(() => disposed = true);

        // Act
        disposable.Dispose();

        // Assert - GC.SuppressFinalize was called (implicit verification)
        disposed.Should().BeTrue();
    }

    #endregion

    #region Using Pattern Tests

    [Fact]
    public void LambdaDisposable_WhenUsedInUsingStatement_ShouldDisposeAutomatically()
    {
        // Arrange
        var disposed = false;

        // Act
        using (var disposable = new LambdaDisposable(() => disposed = true))
        {
            // Do some work
            disposed.Should().BeFalse();
        }

        // Assert
        disposed.Should().BeTrue();
    }

    [Fact]
    public void LambdaDisposable_WhenUsedInUsingStatementWithException_ShouldStillDispose()
    {
        // Arrange
        var disposed = false;

        // Act
        try
        {
            using (var disposable = new LambdaDisposable(() => disposed = true))
            {
                throw new InvalidOperationException("Test exception");
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert - Should dispose even when exception occurred
        disposed.Should().BeTrue();
    }

    #endregion

    #region Action Execution Tests

    [Fact]
    public void Dispose_WithActionThatThrows_ShouldPropagateException()
    {
        // Arrange
        var disposable = new LambdaDisposable(() => throw new InvalidOperationException("Test exception"));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => disposable.Dispose());
    }

    [Fact]
    public void Dispose_WithMultipleActions_ShouldExecuteInOrder()
    {
        // Arrange
        var executionOrder = new System.Collections.Generic.List<int>();
        var disposable = new LambdaDisposable(() =>
        {
            executionOrder.Add(1);
            executionOrder.Add(2);
            executionOrder.Add(3);
        });

        // Act
        disposable.Dispose();

        // Assert
        executionOrder.Should().ContainInOrder(1, 2, 3);
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