using System.Collections;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Extensions;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

/// <summary>
/// Extension helper edge cases kept from bulk enum/extension padding files.
/// (LogoInfoFormat zero-flags coverage lives under Extensions/.)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ExtensionEdgeCaseTests
{
    [Fact]
    public void Enumerable_ForEach_ShouldExecuteInOrder()
    {
        var results = new List<int>();
        new List<int> { 1, 2, 3 }.ForEach(item => results.Add(item));
        results.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Enumerable_ForEach_Empty_ShouldNotExecute()
    {
        var count = 0;
        new List<int>().ForEach(_ => count++);
        count.Should().Be(0);
    }

    [Fact]
    public void Dictionary_AddRange_ShouldAddAllItems()
    {
        var dict = new Dictionary<int, string>();
        dict.AddRange(new Dictionary<int, string> { [1] = "a", [2] = "b", [3] = "c" });
        dict.Should().HaveCount(3);
    }

    [Fact]
    public void Dictionary_AsReadOnlyDictionary_ShouldRejectMutation()
    {
        var ro = new Dictionary<string, int> { ["x"] = 1 }.AsReadOnlyDictionary();
        var act = () => ((ICollection<KeyValuePair<string, int>>)ro).Add(new("y", 2));
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void List_ToArray_ShouldPreserveNullElements()
    {
        IList list = new ArrayList { null, "test", null };
        var result = list.ToArray();
        // Avoid Equal(null, ...) — FluentAssertions treats a leading null as the expectation sequence.
        result.Should().BeEquivalentTo(new object?[] { null, "test", null });
    }

    [Fact]
    public void PInvoke_ThrowIfWin32Error_ZeroCode_ShouldThrowGenericException()
    {
        var act = () => PInvokeExtensions.ThrowIfWin32Error(0, "test operation");
        act.Should().Throw<Exception>()
            .WithMessage("*failed but Win32 didn't catch an error*");
    }
}
