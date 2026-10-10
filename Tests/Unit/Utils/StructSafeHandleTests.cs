using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Trait("Category", TestCategories.Unit)]
public class StructSafeHandleTests
{
    #region Test Structs

    [StructLayout(LayoutKind.Sequential)]
    private struct SimpleStruct
    {
        public int Value;
        public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ComplexStruct
    {
        public int IntValue;
        public long LongValue;
        public float FloatValue;
        public byte ByteValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EmptyStruct
    {
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithSimpleStruct_ShouldInitialize()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42, DoubleValue = 3.14 };

        // Act
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Assert
        handle.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithComplexStruct_ShouldInitialize()
    {
        // Arrange
        var str = new ComplexStruct
        {
            IntValue = 100,
            LongValue = 1000000L,
            FloatValue = 2.5f,
            ByteValue = 255
        };

        // Act
        var handle = new StructSafeHandle<ComplexStruct>(str);

        // Assert
        handle.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyStruct_ShouldInitialize()
    {
        // Arrange
        var str = new EmptyStruct();

        // Act
        var handle = new StructSafeHandle<EmptyStruct>(str);

        // Assert
        handle.Should().NotBeNull();
    }

    #endregion

    #region IsInvalid Tests

    [Fact]
    public void IsInvalid_ShouldReturnFalse()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act & Assert
        handle.IsInvalid.Should().BeFalse();
    }

    [Fact]
    public void IsInvalid_WhenCalledMultipleTimes_ShouldReturnSameResult()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act & Assert
        handle.IsInvalid.Should().BeFalse();
        handle.IsInvalid.Should().BeFalse();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act
        handle.Dispose();

        // Assert - No exception means success
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act
        handle.Dispose();
        handle.Dispose();
        handle.Dispose();

        // Assert - No exception means success
    }

    [Fact]
    public void Dispose_ShouldFreeMemory()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Get handle pointer before dispose
        var handleValueBefore = handle.DangerousGetHandle();

        // Act
        handle.Dispose();

        // Assert - Handle should be closed
        handle.IsClosed.Should().BeTrue();
    }

    #endregion

    #region IsClosed Tests

    [Fact]
    public void IsClosed_ShouldBeFalseInitially()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act & Assert
        handle.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void IsClosed_ShouldBeTrueAfterDispose()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act
        handle.Dispose();

        // Assert
        handle.IsClosed.Should().BeTrue();
    }

    #endregion

    #region Using Pattern Tests

    [Fact]
    public void StructSafeHandle_WhenUsedInUsingStatement_ShouldDispose()
    {
        // Arrange & Act
        using (var handle = new StructSafeHandle<SimpleStruct>(new SimpleStruct { Value = 42 }))
        {
            handle.IsInvalid.Should().BeFalse();
        }

        // Assert - Dispose was called without exception
    }

    [Fact]
    public void StructSafeHandle_WhenUsedInUsingStatementWithException_ShouldStillDispose()
    {
        // Arrange
        var handleCreated = false;
        var disposed = false;

        // Act
        try
        {
            using (var handle = new StructSafeHandle<SimpleStruct>(new SimpleStruct { Value = 42 }))
            {
                handleCreated = true;
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
        handleCreated.Should().BeTrue();
        disposed.Should().BeTrue();
    }

    #endregion

    #region SafeHandle Base Class Tests

    [Fact]
    public void StructSafeHandle_ShouldDeriveFromSafeHandle()
    {
        // Arrange & Act
        var handle = new StructSafeHandle<SimpleStruct>(new SimpleStruct());

        // Assert
        handle.Should().BeAssignableTo<SafeHandle>();
    }

    #endregion

    #region ReleaseHandle Tests

    [Fact]
    public void ReleaseHandle_ShouldReturnTrue()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);
        var releaseHandleMethod = handle.GetType().GetMethod("ReleaseHandle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        releaseHandleMethod.Should().NotBeNull();

        // Act
        var result = (bool)releaseHandleMethod!.Invoke(handle, null)!;

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public void Constructor_ShouldAllocateMemoryForStruct()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42, DoubleValue = 3.14 };
        var expectedSize = Marshal.SizeOf<SimpleStruct>();

        // Act
        var handle = new StructSafeHandle<SimpleStruct>(str);
        var handlePtr = handle.DangerousGetHandle();

        // Assert
        handlePtr.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void Constructor_ShouldCopyStructDataToMemory()
    {
        // Arrange
        var str = new SimpleStruct { Value = 100, DoubleValue = 2.5 };

        // Act
        var handle = new StructSafeHandle<SimpleStruct>(str);
        var handlePtr = handle.DangerousGetHandle();

        // Read back the struct from memory
        var readStruct = Marshal.PtrToStructure<SimpleStruct>(handlePtr);

        // Assert
        readStruct.Value.Should().Be(str.Value);
        readStruct.DoubleValue.Should().Be(str.DoubleValue);
    }

    [Fact]
    public void Constructor_WithDifferentValues_ShouldStoreCorrectly()
    {
        // Arrange
        var str1 = new SimpleStruct { Value = 42, DoubleValue = 1.0 };
        var str2 = new SimpleStruct { Value = 100, DoubleValue = 2.0 };

        // Act
        var handle1 = new StructSafeHandle<SimpleStruct>(str1);
        var handle2 = new StructSafeHandle<SimpleStruct>(str2);

        var ptr1 = handle1.DangerousGetHandle();
        var ptr2 = handle2.DangerousGetHandle();

        var read1 = Marshal.PtrToStructure<SimpleStruct>(ptr1);
        var read2 = Marshal.PtrToStructure<SimpleStruct>(ptr2);

        // Assert
        read1.Value.Should().Be(42);
        read2.Value.Should().Be(100);
        read1.DoubleValue.Should().Be(1.0);
        read2.DoubleValue.Should().Be(2.0);
    }

    #endregion

    #region Multiple Instances Tests

    [Fact]
    public void MultipleHandles_ShouldHaveDifferentAddresses()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };

        // Act
        var handle1 = new StructSafeHandle<SimpleStruct>(str);
        var handle2 = new StructSafeHandle<SimpleStruct>(str);

        var ptr1 = handle1.DangerousGetHandle();
        var ptr2 = handle2.DangerousGetHandle();

        // Assert
        ptr1.Should().NotBe(ptr2);

        // Cleanup
        handle1.Dispose();
        handle2.Dispose();
    }

    [Fact]
    public void MultipleHandles_ShouldAllBeValid()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };

        // Act
        var handles = new[]
        {
            new StructSafeHandle<SimpleStruct>(str),
            new StructSafeHandle<SimpleStruct>(str),
            new StructSafeHandle<SimpleStruct>(str)
        };

        // Assert
        handles.Should().AllSatisfy(h => h.IsInvalid.Should().BeFalse());

        // Cleanup
        foreach (var h in handles)
            h.Dispose();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Dispose_WhenCalledConcurrently_ShouldNotThrow()
    {
        // Arrange
        var str = new SimpleStruct { Value = 42 };
        var handle = new StructSafeHandle<SimpleStruct>(str);

        // Act
        var tasks = new System.Threading.Tasks.Task[]
        {
            System.Threading.Tasks.Task.Run(() => handle.Dispose()),
            System.Threading.Tasks.Task.Run(() => handle.Dispose()),
            System.Threading.Tasks.Task.Run(() => handle.Dispose())
        };

        await System.Threading.Tasks.Task.WhenAll(tasks);

        // Assert - No exception means success
        handle.IsClosed.Should().BeTrue();
    }

    #endregion
}
