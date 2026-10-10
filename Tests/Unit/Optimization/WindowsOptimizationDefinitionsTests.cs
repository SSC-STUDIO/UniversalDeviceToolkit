using FluentAssertions;
using UniversalDeviceToolkit.Lib.Optimization;
using Microsoft.Win32;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Optimization;

[Trait("Category", TestCategories.Unit)]
public class WindowsOptimizationDefinitionsTests
{
    #region RegistryValueDefinition Tests

    [Fact]
    public void RegistryValueDefinition_Init_ShouldSetAllFields()
    {
        var def = new RegistryValueDefinition(
            "HKEY_CURRENT_USER", @"Software\Test", "Value", 42, RegistryValueKind.DWord);

        def.Hive.Should().Be("HKEY_CURRENT_USER");
        def.SubKey.Should().Be(@"Software\Test");
        def.ValueName.Should().Be("Value");
        def.Value.Should().Be(42);
        def.Kind.Should().Be(RegistryValueKind.DWord);
    }

    [Fact]
    public void RegistryValueDefinition_IsRecord_ShouldSupportEquality()
    {
        var a = new RegistryValueDefinition("HKCU", "Key", "Name", 1, RegistryValueKind.DWord);
        var b = new RegistryValueDefinition("HKCU", "Key", "Name", 1, RegistryValueKind.DWord);
        a.Should().Be(b);
    }

    [Fact]
    public void RegistryValueDefinition_DifferentValues_ShouldNotBeEqual()
    {
        var a = new RegistryValueDefinition("HKCU", "Key", "Name", 1, RegistryValueKind.DWord);
        var b = new RegistryValueDefinition("HKCU", "Key", "Name", 2, RegistryValueKind.DWord);
        a.Should().NotBe(b);
    }

    #endregion

    #region Reg Helper Method Tests

    [Fact]
    public void Reg_ShouldCreateRegistryValueDefinition()
    {
        var def = WindowsOptimizationDefinitions.Reg(
            "HKEY_LOCAL_MACHINE", @"SOFTWARE\Test", "Setting", "hello", RegistryValueKind.String);

        def.Hive.Should().Be("HKEY_LOCAL_MACHINE");
        def.SubKey.Should().Be(@"SOFTWARE\Test");
        def.ValueName.Should().Be("Setting");
        def.Value.Should().Be("hello");
        def.Kind.Should().Be(RegistryValueKind.String);
    }

    #endregion

    #region Definition Lists Tests

    [Fact]
    public void ExplorerTaskbarTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.ExplorerTaskbarTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void ExplorerTaskbarTweaks_ShouldAllBeHKCU()
    {
        foreach (var def in WindowsOptimizationDefinitions.ExplorerTaskbarTweaks)
            def.Hive.Should().Be("HKEY_CURRENT_USER");
    }

    [Fact]
    public void TelemetryTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.TelemetryTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void MultimediaTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.MultimediaTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void MemoryTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.MemoryTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void NotificationTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.NotificationTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void ExplorerVisibilityTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.ExplorerVisibilityTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void ExplorerSuggestionsTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.ExplorerSuggestionsTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void ExplorerResponsivenessTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.ExplorerResponsivenessTweaks.Should().NotBeEmpty();
    }

    [Fact]
    public void StartMenuDisableTweaks_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.StartMenuDisableTweaks.Should().NotBeEmpty();
    }

    #endregion

    #region Service Names Tests

    [Fact]
    public void DiagnosticsServices_ShouldContainKnownServices()
    {
        WindowsOptimizationDefinitions.DiagnosticsServices.Should().Contain("DiagTrack");
    }

    [Fact]
    public void SysMainService_ShouldContainSysMain()
    {
        WindowsOptimizationDefinitions.SysMainService.Should().Contain("SysMain");
    }

    [Fact]
    public void SearchService_ShouldContainWSearch()
    {
        WindowsOptimizationDefinitions.SearchService.Should().Contain("WSearch");
    }

    [Fact]
    public void RemoteRegistryService_ShouldContainRemoteRegistry()
    {
        WindowsOptimizationDefinitions.RemoteRegistryService.Should().Contain("RemoteRegistry");
    }

    [Fact]
    public void ErrorReportingService_ShouldContainWerSvc()
    {
        WindowsOptimizationDefinitions.ErrorReportingService.Should().Contain("WerSvc");
    }

    #endregion

    #region Cleanup Commands Tests

    [Fact]
    public void RemoteDesktopCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.RemoteDesktopCacheCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void WindowsUpdateCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.WindowsUpdateCacheCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void BrowserCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.BrowserCacheCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void ThumbnailCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.ThumbnailCacheCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void DotnetNativeImageCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.DotnetNativeImageCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void TempCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.TempCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void RecycleBinCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.RecycleBinCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void PrefetchCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.PrefetchCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void ShaderCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.ShaderCacheCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void InstantMessagingCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.InstantMessagingCacheCommands.Should().NotBeEmpty();
    }

    [Fact]
    public void DevCacheCommands_ShouldNotBeEmpty()
    {
        WindowsOptimizationDefinitions.DevCacheCommands.Should().NotBeEmpty();
    }

    #endregion
}
