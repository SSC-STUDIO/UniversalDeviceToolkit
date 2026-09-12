using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class ProcessInfoComparisonTests
{
    [Fact]
    public void CompareTo_SameNameDifferentPath_ShouldCompareByPath()
    {
        var info1 = new ProcessInfo("app", @"C:\A\app.exe");
        var info2 = new ProcessInfo("app", @"C:\B\app.exe");

        info1.CompareTo(info2).Should().BeLessThan(0);
        info2.CompareTo(info1).Should().BeGreaterThan(0);
    }

    [Fact]
    public void CompareTo_DifferentName_ShouldCompareByName()
    {
        var info1 = new ProcessInfo("aaa", @"C:\aaa.exe");
        var info2 = new ProcessInfo("zzz", @"C:\zzz.exe");

        info1.CompareTo(info2).Should().BeLessThan(0);
    }

    [Fact]
    public void CompareTo_CaseInsensitive_ShouldNotDiffer()
    {
        var info1 = new ProcessInfo("App", @"C:\App.exe");
        var info2 = new ProcessInfo("app", @"C:\app.exe");

        info1.CompareTo(info2).Should().Be(0);
    }

    [Fact]
    public void Operators_LessAndGreater_ShouldWork()
    {
        var info1 = new ProcessInfo("aaa", @"C:\aaa.exe");
        var info2 = new ProcessInfo("zzz", @"C:\zzz.exe");

        (info1 < info2).Should().BeTrue();
        (info2 > info1).Should().BeTrue();
        (info1 <= info2).Should().BeTrue();
        (info2 >= info1).Should().BeTrue();
    }

    [Fact]
    public void Operators_SameValues_ShouldBeEqual()
    {
        var info1 = new ProcessInfo("app", @"C:\app.exe");
        var info2 = new ProcessInfo("app", @"C:\app.exe");

        (info1 == info2).Should().BeTrue();
        (info1 != info2).Should().BeFalse();
        (info1 <= info2).Should().BeTrue();
        (info1 >= info2).Should().BeTrue();
    }

    [Fact]
    public void FromPath_WithSpaces_ShouldExtractName()
    {
        var info = ProcessInfo.FromPath(@"C:\Program Files\My App\tool.exe");
        info.Name.Should().Be("tool");
    }
}
