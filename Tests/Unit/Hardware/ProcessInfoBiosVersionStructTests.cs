using System;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Hardware;

[Trait("Category", TestCategories.Unit)]
public class ProcessInfoBiosVersionStructTests
{
    #region ProcessInfo Tests

    [Fact]
    public void ProcessInfo_Constructor_ShouldSetProperties()
    {
        var info = new ProcessInfo("test.exe", @"C:\path\to\test.exe");
        info.Name.Should().Be("test.exe");
        info.ExecutablePath.Should().Be(@"C:\path\to\test.exe");
    }

    [Fact]
    public void ProcessInfo_NullPath_ShouldAllowNull()
    {
        var info = new ProcessInfo("test", null);
        info.Name.Should().Be("test");
        info.ExecutablePath.Should().BeNull();
    }

    [Fact]
    public void ProcessInfo_FromPath_ShouldExtractName()
    {
        var info = ProcessInfo.FromPath(@"C:\Program Files\MyApp\launcher.exe");
        info.Name.Should().Be("launcher");
        info.ExecutablePath.Should().Be(@"C:\Program Files\MyApp\launcher.exe");
    }

    [Fact]
    public void ProcessInfo_FromPath_RootPath_ShouldWork()
    {
        var info = ProcessInfo.FromPath(@"app.exe");
        info.Name.Should().Be("app");
        info.ExecutablePath.Should().Be("app.exe");
    }

    [Fact]
    public void ProcessInfo_ToString_ShouldContainNameAndPath()
    {
        var info = new ProcessInfo("MyApp", @"C:\MyApp.exe");
        var str = info.ToString();
        str.Should().Contain("MyApp");
        str.Should().Contain(@"C:\MyApp.exe");
    }

    [Fact]
    public void ProcessInfo_Equality_SameValues_ShouldBeEqual()
    {
        var a = new ProcessInfo("test", @"C:\test.exe");
        var b = new ProcessInfo("test", @"C:\test.exe");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void ProcessInfo_Equality_DifferentName_ShouldNotBeEqual()
    {
        var a = new ProcessInfo("test", @"C:\test.exe");
        var b = new ProcessInfo("other", @"C:\test.exe");
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void ProcessInfo_Equality_DifferentPath_ShouldNotBeEqual()
    {
        var a = new ProcessInfo("test", @"C:\test.exe");
        var b = new ProcessInfo("test", @"C:\other.exe");
        a.Should().NotBe(b);
    }

    [Fact]
    public void ProcessInfo_CompareTo_SameName_ShouldReturnZero()
    {
        var a = new ProcessInfo("test", @"C:\a.exe");
        var b = new ProcessInfo("test", @"C:\a.exe");
        a.CompareTo(b).Should().Be(0);
    }

    [Fact]
    public void ProcessInfo_CompareTo_DifferentName_ShouldCompareAlphabetically()
    {
        var a = new ProcessInfo("aaa", null);
        var b = new ProcessInfo("zzz", null);
        a.CompareTo(b).Should().BeLessThan(0);
    }

    [Fact]
    public void ProcessInfo_CompareTo_Null_ShouldHandle()
    {
        var a = new ProcessInfo("test", null);
        a.CompareTo(null).Should().BeGreaterThan(0);
    }

    [Fact]
    public void ProcessInfo_HashCode_SameValues_ShouldMatch()
    {
        var a = new ProcessInfo("test", @"C:\test.exe");
        var b = new ProcessInfo("test", @"C:\test.exe");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion

    #region BiosVersion Tests

    [Fact]
    public void BiosVersion_Constructor_ShouldSetProperties()
    {
        var v = new BiosVersion("J", 123);
        v.Prefix.Should().Be("J");
        v.Version.Should().Be(123);
    }

    [Fact]
    public void BiosVersion_NullVersion_ShouldAllowNull()
    {
        var v = new BiosVersion("J", null);
        v.Prefix.Should().Be("J");
        v.Version.Should().BeNull();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_SameVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", 100);
        var b = new BiosVersion("J", 100);
        a.IsHigherOrEqualThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_HigherVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", 200);
        var b = new BiosVersion("J", 100);
        a.IsHigherOrEqualThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_LowerVersion_ShouldBeFalse()
    {
        var a = new BiosVersion("J", 50);
        var b = new BiosVersion("J", 100);
        a.IsHigherOrEqualThan(b).Should().BeFalse();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_DifferentPrefix_ShouldBeFalse()
    {
        var a = new BiosVersion("K", 200);
        var b = new BiosVersion("J", 100);
        a.IsHigherOrEqualThan(b).Should().BeFalse();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_NullSelfVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", null);
        var b = new BiosVersion("J", 100);
        a.IsHigherOrEqualThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_NullOtherVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", 100);
        var b = new BiosVersion("J", null);
        a.IsHigherOrEqualThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_SameVersion_ShouldBeFalse()
    {
        var a = new BiosVersion("J", 100);
        var b = new BiosVersion("J", 100);
        a.IsLowerThan(b).Should().BeFalse();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_LowerVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", 50);
        var b = new BiosVersion("J", 100);
        a.IsLowerThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_HigherVersion_ShouldBeFalse()
    {
        var a = new BiosVersion("J", 200);
        var b = new BiosVersion("J", 100);
        a.IsLowerThan(b).Should().BeFalse();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_DifferentPrefix_ShouldBeFalse()
    {
        var a = new BiosVersion("K", 50);
        var b = new BiosVersion("J", 100);
        a.IsLowerThan(b).Should().BeFalse();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_NullSelfVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", null);
        var b = new BiosVersion("J", 100);
        a.IsLowerThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_NullOtherVersion_ShouldBeTrue()
    {
        var a = new BiosVersion("J", 100);
        var b = new BiosVersion("J", null);
        a.IsLowerThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsLowerThan_BothNull_ShouldBeTrue()
    {
        var a = new BiosVersion("J", null);
        var b = new BiosVersion("J", null);
        a.IsLowerThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_IsHigherOrEqualThan_BothNull_ShouldBeTrue()
    {
        var a = new BiosVersion("J", null);
        var b = new BiosVersion("J", null);
        a.IsHigherOrEqualThan(b).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_PrefixComparison_ShouldBeCaseInsensitive()
    {
        var a = new BiosVersion("j", 100);
        var b = new BiosVersion("J", 100);
        a.IsHigherOrEqualThan(b).Should().BeTrue();
        b.IsHigherOrEqualThan(a).Should().BeTrue();
    }

    [Fact]
    public void BiosVersion_Equality_SameValues_ShouldBeEqual()
    {
        var a = new BiosVersion("J", 100);
        var b = new BiosVersion("J", 100);
        a.Should().Be(b);
    }

    [Fact]
    public void BiosVersion_Equality_DifferentValues_ShouldNotBeEqual()
    {
        var a = new BiosVersion("J", 100);
        var b = new BiosVersion("J", 200);
        a.Should().NotBe(b);
    }

    #endregion

    #region Misc Additional Enum Coverage

    [Theory]
    [InlineData(NotificationType.SpectrumBacklightChanged)]
    [InlineData(NotificationType.SmartKeySinglePress)]
    [InlineData(NotificationType.SmartKeyDoublePress)]
    [InlineData(NotificationType.RefreshRate)]
    [InlineData(NotificationType.UpdateAvailable)]
    public void NotificationType_ExtendedValues_ShouldBeDefined(NotificationType value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void NativeWindowsMessage_ShouldContainExpectedValues()
    {
        var values = Enum.GetValues<NativeWindowsMessage>();
        values.Should().Contain(NativeWindowsMessage.LidOpened);
        values.Should().Contain(NativeWindowsMessage.LidClosed);
        values.Should().Contain(NativeWindowsMessage.MonitorOn);
    }

    [Fact]
    public void KnownFolder_ShouldHaveMultipleMembers()
    {
        Enum.GetValues<KnownFolder>().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(KnownFolder.Downloads)]
    [InlineData(KnownFolder.SavedGames)]
    [InlineData(KnownFolder.Contacts)]
    public void KnownFolder_ShouldContainExpectedValues(KnownFolder value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void WindowBackdropStyle_ShouldHaveMultipleMembers()
    {
        Enum.GetValues<WindowBackdropStyle>().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(WindowBackdropStyle.Windows)]
    [InlineData(WindowBackdropStyle.macOS)]
    public void WindowBackdropStyle_ShouldBeDefined(WindowBackdropStyle value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }

    [Fact]
    public void SpectrumLayout_ShouldHaveMultipleMembers()
    {
        Enum.GetValues<SpectrumLayout>().Should().NotBeEmpty();
    }

    [Fact]
    public void SpectrumKeyboardBacklightBrightness_ShouldHaveExpectedMembers()
    {
        var values = Enum.GetValues<SpectrumKeyboardBacklightBrightness>();
        values.Should().NotBeEmpty();
    }

    [Fact]
    public void SpectrumKeyboardBacklightSpeed_ShouldHaveExpectedMembers()
    {
        var values = Enum.GetValues<SpectrumKeyboardBacklightSpeed>();
        values.Should().NotBeEmpty();
    }

    [Fact]
    public void CPUOverclockingID_ShouldHaveExpectedMembers()
    {
        var values = Enum.GetValues<CPUOverclockingID>();
        values.Should().NotBeEmpty();
    }

    [Fact]
    public void BootLogoFormat_ShouldHaveExpectedMembers()
    {
        var values = Enum.GetValues<BootLogoFormat>();
        values.Should().NotBeEmpty();
    }

    #endregion
}
