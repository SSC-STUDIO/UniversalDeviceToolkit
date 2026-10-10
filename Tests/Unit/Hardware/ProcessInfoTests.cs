using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class ProcessInfoTests
{
    #region FromPath Tests

    [Fact]
    public void FromPath_WithExePath_ShouldExtractName()
    {
        var info = ProcessInfo.FromPath(@"C:\Program Files\app\myapp.exe");
        info.Name.Should().Be("myapp");
        info.ExecutablePath.Should().Be(@"C:\Program Files\app\myapp.exe");
    }

    [Fact]
    public void FromPath_WithDllPath_ShouldExtractName()
    {
        var info = ProcessInfo.FromPath(@"C:\lib\helper.dll");
        info.Name.Should().Be("helper");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WhenSameNameAndPath_ShouldBeEqual()
    {
        var a = new ProcessInfo("notepad", @"C:\notepad.exe");
        var b = new ProcessInfo("notepad", @"C:\notepad.exe");
        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    [Fact]
    public void Equals_WhenDifferentName_ShouldNotBeEqual()
    {
        var a = new ProcessInfo("notepad", @"C:\notepad.exe");
        var b = new ProcessInfo("calc", @"C:\notepad.exe");
        a.Equals(b).Should().BeFalse();
    }

    #endregion

    #region CompareTo Tests

    [Fact]
    public void CompareTo_WhenNamesDifferByCase_ShouldTreatAsEqual()
    {
        var a = new ProcessInfo("Notepad", @"C:\notepad.exe");
        var b = new ProcessInfo("notepad", @"C:\notepad.exe");
        a.CompareTo(b).Should().Be(0);
    }

    [Fact]
    public void CompareTo_WhenAlphabeticallyLessThan_ShouldReturnNegative()
    {
        var a = new ProcessInfo("alpha", null);
        var b = new ProcessInfo("beta", null);
        a.CompareTo(b).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_WhenAlphabeticallyGreaterThan_ShouldReturnPositive()
    {
        var a = new ProcessInfo("beta", null);
        var b = new ProcessInfo("alpha", null);
        a.CompareTo(b).Should().BePositive();
    }

    [Fact]
    public void CompareTo_WhenNull_ShouldNotBeZero()
    {
        var a = new ProcessInfo("alpha", null);
        a.CompareTo(null).Should().NotBe(0);
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void Operators_LessThan_ShouldWorkCorrectly()
    {
        var a = new ProcessInfo("alpha", null);
        var b = new ProcessInfo("beta", null);
        (a < b).Should().BeTrue();
        (b < a).Should().BeFalse();
    }

    [Fact]
    public void Operators_GreaterThan_ShouldWorkCorrectly()
    {
        var a = new ProcessInfo("beta", null);
        var b = new ProcessInfo("alpha", null);
        (a > b).Should().BeTrue();
    }

    [Fact]
    public void Operators_LessThanOrEqual_WithEqual_ShouldBeTrue()
    {
        var a = new ProcessInfo("alpha", null);
        var b = new ProcessInfo("alpha", null);
        (a <= b).Should().BeTrue();
    }

    [Fact]
    public void Operators_GreaterThanOrEqual_WithEqual_ShouldBeTrue()
    {
        var a = new ProcessInfo("alpha", null);
        var b = new ProcessInfo("alpha", null);
        (a >= b).Should().BeTrue();
    }

    #endregion
}
