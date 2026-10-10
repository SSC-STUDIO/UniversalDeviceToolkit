using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class EnumerableExtensionsTests
{
    #region IsEmpty Tests

    [Fact]
    public void IsEmpty_WhenCollectionIsEmpty_ShouldReturnTrue()
    {
        // Arrange
        var collection = new List<int>();

        // Act
        var result = collection.IsEmpty();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhenCollectionHasElements_ShouldReturnFalse()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };

        // Act
        var result = collection.IsEmpty();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_WhenArrayIsEmpty_ShouldReturnTrue()
    {
        // Arrange
        var array = Array.Empty<int>();

        // Act
        var result = array.IsEmpty();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhenArrayHasElements_ShouldReturnFalse()
    {
        // Arrange
        var array = new[] { 1, 2, 3 };

        // Act
        var result = array.IsEmpty();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_WhenIEnumerableIsEmpty_ShouldReturnTrue()
    {
        // Arrange
        IEnumerable<int> enumerable = Enumerable.Empty<int>();

        // Act
        var result = enumerable.IsEmpty();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhenIEnumerableHasElements_ShouldReturnFalse()
    {
        // Arrange
        IEnumerable<int> enumerable = Enumerable.Range(1, 3);

        // Act
        var result = enumerable.IsEmpty();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_WhenStringCollectionIsEmpty_ShouldReturnTrue()
    {
        // Arrange
        var collection = new List<string>();

        // Act
        var result = collection.IsEmpty();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhenStringCollectionHasElements_ShouldReturnFalse()
    {
        // Arrange
        var collection = new List<string> { "a", "b", "c" };

        // Act
        var result = collection.IsEmpty();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ForEach Tests

    [Fact]
    public void ForEach_WhenCollectionHasElements_ShouldExecuteActionForEach()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };
        var sum = 0;

        // Act
        collection.ForEach(item => sum += item);

        // Assert
        sum.Should().Be(6);
    }

    [Fact]
    public void ForEach_WhenCollectionIsEmpty_ShouldNotExecuteAction()
    {
        // Arrange
        var collection = new List<int>();
        var executed = false;

        // Act
        collection.ForEach(item => executed = true);

        // Assert
        executed.Should().BeFalse();
    }

    [Fact]
    public void ForEach_WithActionModifyingCollection_ShouldExecuteForAll()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };
        var results = new List<int>();

        // Act
        collection.ForEach(item => results.Add(item * 2));

        // Assert
        results.Should().ContainInOrder(2, 4, 6);
    }

    [Fact]
    public void ForEach_WithStringCollection_ShouldExecuteForEach()
    {
        // Arrange
        var collection = new List<string> { "a", "b", "c" };
        var concatenated = "";

        // Act
        collection.ForEach(item => concatenated += item);

        // Assert
        concatenated.Should().Be("abc");
    }

    [Fact]
    public void ForEach_WithArray_ShouldExecuteForEach()
    {
        // Arrange
        var array = new[] { 1, 2, 3 };
        var count = 0;

        // Act
        array.ForEach(item => count++);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void ForEach_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => collection.ForEach(null!));
    }

    #endregion

    #region Split Tests

    [Fact]
    public void Split_WhenCollectionSizeIsMultipleOfSize_ShouldSplitCorrectly()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5, 6 };
        var size = 2;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().ContainInOrder(1, 2);
        result[1].Should().ContainInOrder(3, 4);
        result[2].Should().ContainInOrder(5, 6);
    }

    [Fact]
    public void Split_WhenCollectionSizeIsNotMultipleOfSize_ShouldSplitWithRemainder()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5 };
        var size = 2;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().ContainInOrder(1, 2);
        result[1].Should().ContainInOrder(3, 4);
        result[2].Should().ContainInOrder(5);
    }

    [Fact]
    public void Split_WhenCollectionIsEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var collection = new List<int>();
        var size = 2;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_WhenSizeIsLargerThanCollection_ShouldReturnSingleBatch()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };
        var size = 10;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Split_WhenSizeIsOne_ShouldReturnEachElementInSeparateBatch()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3 };
        var size = 1;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().ContainInOrder(1);
        result[1].Should().ContainInOrder(2);
        result[2].Should().ContainInOrder(3);
    }

    [Fact]
    public void Split_WithStringCollection_ShouldSplitCorrectly()
    {
        // Arrange
        var collection = new List<string> { "a", "b", "c", "d", "e" };
        var size = 2;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().ContainInOrder("a", "b");
        result[1].Should().ContainInOrder("c", "d");
        result[2].Should().ContainInOrder("e");
    }

    [Fact]
    public void Split_WithLargeCollection_ShouldSplitCorrectly()
    {
        // Arrange
        var collection = new List<int>(Enumerable.Range(1, 100));
        var size = 10;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        result.Should().HaveCount(10);
        result[0].Should().HaveCount(10);
        result[9].Should().HaveCount(10);
    }

    [Fact]
    public void Split_PreservesOriginalOrder()
    {
        // Arrange
        var collection = new List<int> { 1, 2, 3, 4, 5, 6 };
        var size = 3;

        // Act
        var result = collection.Split(size)
            .Select(batch => batch.ToList())
            .ToList();

        // Assert
        var flattened = result.SelectMany(x => x);
        flattened.Should().ContainInOrder(1, 2, 3, 4, 5, 6);
    }

    #endregion
}
