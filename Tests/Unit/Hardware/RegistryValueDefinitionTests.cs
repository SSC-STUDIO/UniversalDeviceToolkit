using Microsoft.Win32;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Optimization;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class RegistryValueDefinitionTests
{
    [Fact]
    public void Properties_ShouldRetainValues()
    {
        var def = new RegistryValueDefinition(
            "HKEY_CURRENT_USER",
            @"Software\Microsoft\Windows\CurrentVersion\Search",
            "SearchboxTaskbarMode",
            0,
            RegistryValueKind.DWord);

        def.Hive.Should().Be("HKEY_CURRENT_USER");
        def.SubKey.Should().Be(@"Software\Microsoft\Windows\CurrentVersion\Search");
        def.ValueName.Should().Be("SearchboxTaskbarMode");
        def.Value.Should().Be(0);
        def.Kind.Should().Be(RegistryValueKind.DWord);
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        var a = new RegistryValueDefinition("HKCU", @"Software\Test", "Value", 42, RegistryValueKind.DWord);
        var b = new RegistryValueDefinition("HKCU", @"Software\Test", "Value", 42, RegistryValueKind.DWord);
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new RegistryValueDefinition("HKCU", @"Software\Test", "Value", 42, RegistryValueKind.DWord);
        var b = new RegistryValueDefinition("HKCU", @"Software\Test", "Value", 43, RegistryValueKind.DWord);
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ToString_ShouldContainFields()
    {
        var def = new RegistryValueDefinition("HKLM", @"Software\MyApp", "Setting", 1, RegistryValueKind.DWord);
        var str = def.ToString();
        str.Should().Contain("HKLM");
        str.Should().Contain("MyApp");
        str.Should().Contain("Setting");
    }

    [Fact]
    public void GetHashCode_SameValues_ShouldMatch()
    {
        var a = new RegistryValueDefinition("HKCU", "Sub", "Name", "test", RegistryValueKind.String);
        var b = new RegistryValueDefinition("HKCU", "Sub", "Name", "test", RegistryValueKind.String);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}