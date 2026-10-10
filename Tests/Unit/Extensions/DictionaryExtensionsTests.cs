using System.Collections.Generic;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Extensions;

[Trait("Category", TestCategories.Unit)]
public class DictionaryExtensionsTests
{
    [Fact]
    public void AsReadOnlyDictionary_ShouldWrapDictionary()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        var ro = dict.AsReadOnlyDictionary();

        ro.Should().ContainKey("a").WhoseValue.Should().Be(1);
        ro.Should().ContainKey("b").WhoseValue.Should().Be(2);
        ro.Count.Should().Be(2);
    }

    [Fact]
    public void AddRange_ShouldMergeItems()
    {
        var source = new Dictionary<string, int> { ["a"] = 1 };
        var items = new Dictionary<string, int> { ["b"] = 2, ["c"] = 3 };

        source.AddRange(items);

        source.Should().HaveCount(3);
        source["b"].Should().Be(2);
        source["c"].Should().Be(3);
    }

    [Fact]
    public void AddRange_WithDuplicateKey_ShouldThrow()
    {
        var source = new Dictionary<string, int> { ["a"] = 1 };
        var items = new Dictionary<string, int> { ["a"] = 2 };

        var act = () => source.AddRange(items);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetValueOrNull_WithExistingKey_ShouldReturnValue()
    {
        var dict = new Dictionary<string, int> { ["x"] = 42 };
        dict.GetValueOrNull("x").Should().Be(42);
    }

    [Fact]
    public void GetValueOrNull_WithMissingKey_ShouldReturnNull()
    {
        var dict = new Dictionary<string, int>();
        dict.GetValueOrNull("missing").Should().BeNull();
    }
}
