using Microsoft.Win32;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Optimization;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Optimization;

[Trait("Category", TestCategories.Unit)]
public class WindowsOptimizationHelperTests
{
    #region RegistryValueEquals

    [Fact]
    public void RegistryValueEquals_DWord_EqualValues_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(42, 42, RegistryValueKind.DWord);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_DWord_DifferentValues_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(42, 43, RegistryValueKind.DWord);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_QWord_EqualValues_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(100L, 100L, RegistryValueKind.QWord);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_QWord_DifferentValues_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(100L, 200L, RegistryValueKind.QWord);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_String_EqualValues_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals("hello", "hello", RegistryValueKind.String);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_String_DifferentValues_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals("hello", "world", RegistryValueKind.String);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_ExpandString_EqualValues_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals("%TEMP%", "%TEMP%", RegistryValueKind.ExpandString);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_ExpandString_DifferentValues_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals("%TEMP%", "%USER%", RegistryValueKind.ExpandString);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_String_CaseSensitive_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals("Hello", "hello", RegistryValueKind.String);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_DWord_IntVsLong_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(42, 42L, RegistryValueKind.DWord);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_DWord_StringInput_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals("not-a-number", 42, RegistryValueKind.DWord);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_Binary_SameReference_ShouldReturnTrue()
    {
        var arr = new byte[] { 1, 2, 3 };
        var result = WindowsOptimizationHelper.RegistryValueEquals(arr, arr, RegistryValueKind.Binary);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_Binary_DifferentReferences_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }, RegistryValueKind.Binary);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_Binary_DifferentArrays_ShouldReturnFalse()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }, RegistryValueKind.Binary);
        result.Should().BeFalse();
    }

    [Fact]
    public void RegistryValueEquals_String_NullValues_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(null!, null!, RegistryValueKind.String);
        result.Should().BeTrue();
    }

    [Fact]
    public void RegistryValueEquals_DWord_ZeroVsZero_ShouldReturnTrue()
    {
        var result = WindowsOptimizationHelper.RegistryValueEquals(0, 0, RegistryValueKind.DWord);
        result.Should().BeTrue();
    }

    #endregion
}