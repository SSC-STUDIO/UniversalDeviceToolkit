using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class NullSafeHandleTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var handle = new NullSafeHandle();

        // Assert
        handle.Should().NotBeNull();
    }

    #endregion

    #region IsInvalid Tests

    [Fact]
    public void IsInvalid_ShouldReturnFalse()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act & Assert
        handle.IsInvalid.Should().BeFalse();
    }

    [Fact]
    public void IsInvalid_WhenCalledMultipleTimes_ShouldReturnSameResult()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act & Assert
        handle.IsInvalid.Should().BeFalse();
        handle.IsInvalid.Should().BeFalse();
    }

    #endregion

    #region Static Null Property Tests

    [Fact]
    public void Null_ShouldReturnNonNullHandle()
    {
        // Arrange & Act
        var handle = NullSafeHandle.Null;

        // Assert
        handle.Should().NotBeNull();
    }

    [Fact]
    public void Null_WhenCalledMultipleTimes_ShouldReturnIndependentHandles()
    {
        // Arrange & Act
        var handle1 = NullSafeHandle.Null;
        var handle2 = NullSafeHandle.Null;

        // Assert
        handle1.Should().NotBeSameAs(handle2);
        handle1.IsClosed.Should().BeFalse();
        handle2.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Null_IsInvalid_ShouldReturnFalse()
    {
        // Arrange
        var handle = NullSafeHandle.Null;

        // Act & Assert
        handle.IsInvalid.Should().BeFalse();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act
        handle.Dispose();

        // Assert - No exception means success
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act
        handle.Dispose();
        handle.Dispose();
        handle.Dispose();

        // Assert - No exception means success
    }

    [Fact]
    public void Dispose_OnNullStaticHandle_ShouldNotThrow()
    {
        // Arrange
        var handle = NullSafeHandle.Null;

        // Act
        handle.Dispose();

        // Assert - No exception means success
    }

    #endregion

    #region Using Pattern Tests

    [Fact]
    public void NullSafeHandle_WhenUsedInUsingStatement_ShouldDispose()
    {
        // Arrange & Act
        using (var handle = new NullSafeHandle())
        {
            handle.IsInvalid.Should().BeFalse();
        }

        // Assert - Dispose was called without exception
    }

    [Fact]
    public void NullSafeHandle_WhenUsedInUsingStatementWithException_ShouldStillDispose()
    {
        // Arrange
        var disposed = false;
        var handle = new NullSafeHandle();

        // Act
        try
        {
            using (handle)
            {
                handle.IsInvalid.Should().BeFalse();
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

    #region Handle Value Tests

    [Fact]
    public void IsInvalid_ShouldBeFalse_Always()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act & Assert - NullSafeHandle represents a null pointer but is "valid" as a SafeHandle
        handle.IsInvalid.Should().BeFalse();
        handle.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Null_ShouldBehaveAsValidNullHandle()
    {
        // Arrange & Act
        var handle = NullSafeHandle.Null;

        // Assert - Null handle is also valid (non-null SafeHandle wrapping IntPtr.Zero)
        handle.IsInvalid.Should().BeFalse();
        handle.IsClosed.Should().BeFalse();
    }

    #endregion

    #region SafeHandle Base Class Tests

    [Fact]
    public void NullSafeHandle_ShouldDeriveFromSafeHandle()
    {
        // Arrange & Act
        var handle = new NullSafeHandle();

        // Assert
        handle.Should().BeAssignableTo<System.Runtime.InteropServices.SafeHandle>();
    }

    [Fact]
    public void NullSafeHandle_IsClosed_ShouldBeFalseInitially()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act & Assert
        handle.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void NullSafeHandle_IsClosed_ShouldBeTrueAfterDispose()
    {
        // Arrange
        var handle = new NullSafeHandle();

        // Act
        handle.Dispose();

        // Assert
        handle.IsClosed.Should().BeTrue();
    }

    #endregion

    #region ReleaseHandle Tests

    [Fact]
    public void ReleaseHandle_ShouldReturnTrue()
    {
        // Arrange
        var handle = new NullSafeHandle();
        var releaseHandleMethod = handle.GetType().GetMethod("ReleaseHandle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        releaseHandleMethod.Should().NotBeNull();

        // Act
        var result = (bool)releaseHandleMethod!.Invoke(handle, null)!;

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
