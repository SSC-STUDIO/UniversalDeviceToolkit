using System.Collections;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class ListExtensionsTests
{
    [Fact]
    public void ToArray_WithNonGenericList_ShouldCopyElements()
    {
        IList list = new ArrayList { "a", "b", "c" };

        var result = list.ToArray();

        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[1].Should().Be("b");
        result[2].Should().Be("c");
    }

    [Fact]
    public void ToArray_WithEmptyList_ShouldReturnEmptyArray()
    {
        IList list = new ArrayList();

        var result = list.ToArray();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ToArray_WithMixedTypes_ShouldCopyAll()
    {
        IList list = new ArrayList { 42, "hello", 3.14 };

        var result = list.ToArray();

        result.Should().HaveCount(3);
        result[0].Should().Be(42);
        result[1].Should().Be("hello");
        result[2].Should().Be(3.14);
    }
}
