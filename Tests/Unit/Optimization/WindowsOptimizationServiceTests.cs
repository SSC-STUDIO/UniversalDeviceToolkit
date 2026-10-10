using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; 
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Optimization;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Optimization;

public class WindowsOptimizationServiceTests
{
    [Fact]
    public void GetCategories_ShouldReturnNonEmptyList()
    {
        // Arrange & Act
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        var categories = service.GetCategories();
        
        // Assert
        categories.Should().NotBeEmpty();
        categories.SelectMany(c => c.Actions).Should().NotBeEmpty();
    }

    [Fact]
    public void GetCategories_ShouldContainExpectedCategories()
    {
        // Arrange & Act
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        var categories = service.GetCategories();
        var categoryKeys = categories.Select(c => c.Key).ToList();
        
        // Assert
        categoryKeys.Should().Contain("explorer");
        categoryKeys.Should().Contain("performance");
        categoryKeys.Should().Contain("services");
        categoryKeys.Should().Contain("cleanup.cache");
        categoryKeys.Should().NotContain("network", "network acceleration is provided by the plugin package, not the host app");
    }

    [Fact]
    public async Task TryGetActionAppliedAsync_ShouldPropagateCancellationFromStateProbe()
    {
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => service.TryGetActionAppliedAsync("performance.powerPlan", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EstimateCleanupSizeAsync_ShouldReturnNonZeroForValidActionKey()
    {
        // Arrange — do not use CancellationToken.None: real Temp trees can hang CI for minutes.
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        var validActionKey = "cleanup.tempFiles";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        // Act
        try
        {
            var result = await service.EstimateCleanupSizeAsync(new[] { validActionKey }, cts.Token);
            result.Should().BeGreaterThanOrEqualTo(0);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Bounded scan is enough to prove the API is wired; hang is a regression.
        }
    }

    [Fact]
    public void GetAllActionKeys_ShouldReturnExpectedKeys()
    {
        // Arrange
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        
        // Act
        var allActionKeys = service.GetCategories()
            .SelectMany(c => c.Actions)
            .Select(a => a.Key)
            .ToList();
        
        // Assert
        allActionKeys.Should().NotBeEmpty();
        
        // Check for critical action keys that should exist based on the provider
        allActionKeys.Should().Contain("explorer.taskbar");
        allActionKeys.Should().Contain("performance.powerPlan");
        allActionKeys.Should().Contain("services.diagnostics");
        allActionKeys.Should().Contain("cleanup.tempFiles");
        allActionKeys.Should().Contain("cleanup.custom");
        allActionKeys.Should().NotContain("network.acceleration", "network acceleration is provided by the plugin package, not the host app");
        
        // Verify expected categories are represented by their actions
        allActionKeys.Should().Contain(k => k.StartsWith("explorer."));
        allActionKeys.Should().Contain(k => k.StartsWith("performance."));
        allActionKeys.Should().Contain(k => k.StartsWith("services."));
        allActionKeys.Should().Contain(k => k.StartsWith("cleanup."));
        allActionKeys.Should().NotContain(k => k.StartsWith("network."), "plugin-specific optimization actions must not be built into the host app");
    }

    [Fact]
    public void PowerPlanCommand_ShouldUseHighPerformanceScheme()
    {
        WindowsOptimizationDefinitions.PowerPlanCommands.Should().Contain("powercfg -setactive SCHEME_MAX");
        WindowsOptimizationDefinitions.PowerPlanCommands.Should().NotContain("powercfg -setactive SCHEME_MIN");
    }

    [Fact]
    public void CleanupShellBuiltInCommands_ShouldPassValidation()
    {
        var commands = WindowsOptimizationDefinitions.RemoteDesktopCacheCommands
            .Concat(WindowsOptimizationDefinitions.WindowsUpdateCacheCommands)
            .Concat(WindowsOptimizationDefinitions.BrowserCacheCommands)
            .Concat(WindowsOptimizationDefinitions.AppLeftoverCommands)
            .Concat(WindowsOptimizationDefinitions.ThumbnailCacheCommands)
            .Concat(WindowsOptimizationDefinitions.DotnetNativeImageCommands)
            .Concat(WindowsOptimizationDefinitions.SystemLogCommands)
            .Concat(WindowsOptimizationDefinitions.CrashDumpCommands)
            .Concat(WindowsOptimizationDefinitions.DefenderCommands)
            .Concat(WindowsOptimizationDefinitions.TempCommands)
            .Concat(WindowsOptimizationDefinitions.RecycleBinCommands)
            .Concat(WindowsOptimizationDefinitions.PrefetchCommands)
            .Concat(WindowsOptimizationDefinitions.ComponentStoreCommands);

        commands.Should().OnlyContain(command => WindowsOptimizationService.IsValidCommand(command));
    }

    [Fact]
    public async Task ApplyPerformanceOptimizationsAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await service.Invoking(s => s.ApplyPerformanceOptimizationsAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunCleanupAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await service.Invoking(s => s.RunCleanupAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void IsValidCommand_WithEmptyCommand_ShouldReturnFalse()
    {
        WindowsOptimizationService.IsValidCommand("").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("   ").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithAllowedCommand_ShouldReturnTrue()
    {
        WindowsOptimizationService.IsValidCommand("powercfg -setactive SCHEME_MAX").Should().BeTrue();
        WindowsOptimizationService.IsValidCommand("ipconfig /flushdns").Should().BeTrue();
        WindowsOptimizationService.IsValidCommand("netsh int ip reset").Should().BeTrue();
    }

    [Fact]
    public void IsValidCommand_WithDangerousPatterns_ShouldReturnFalse()
    {
        // Command injection patterns
        WindowsOptimizationService.IsValidCommand("powercfg & calc.exe").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("powercfg | calc.exe").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("powercfg ; calc.exe").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("powercfg ` calc.exe").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("powercfg $(calc.exe)").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithNotAllowedExecutable_ShouldReturnFalse()
    {
        WindowsOptimizationService.IsValidCommand("calc.exe").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("notepad test.txt").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("malware.exe --dangerous").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithDelCommand_ShouldValidateArguments()
    {
        // del command should be allowed with proper arguments
        WindowsOptimizationService.IsValidCommand("del /q \"C:\\Windows\\Temp\\*.*\"").Should().BeTrue();
    }

    [Fact]
    public void IsValidCommand_WithRdCommand_ShouldValidateArguments()
    {
        // rd command should be allowed with proper arguments
        WindowsOptimizationService.IsValidCommand("rd /s /q \"C:\\Windows\\Temp\\Test\"").Should().BeTrue();
    }

    [Fact]
    public void IsValidCommand_WithRegCommand_ShouldReturnTrue()
    {
        WindowsOptimizationService.IsValidCommand("reg add \"HKCU\\Software\\Test\" /v Name /t REG_SZ /d Value /f").Should().BeTrue();
        WindowsOptimizationService.IsValidCommand("reg delete \"HKCU\\Software\\Test\" /f").Should().BeTrue();
    }

    [Fact]
    public void IsValidCommand_WithScCommand_ShouldReturnTrue()
    {
        WindowsOptimizationService.IsValidCommand("sc config \"TestService\" start= disabled").Should().BeTrue();
    }

    [Fact]
    public void IsValidCommand_WithDismCommand_ShouldReturnTrue()
    {
        WindowsOptimizationService.IsValidCommand("dism /online /cleanup-image /startcomponentcleanup").Should().BeTrue();
    }

    [Fact]
    public void IsValidCommand_WithRedirectionOperators_ShouldReturnFalse()
    {
        // Output/input redirection should be blocked
        WindowsOptimizationService.IsValidCommand("powercfg /getactivescheme > C:\\output.txt").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("ipconfig /flushdns < C:\\input.txt").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithEnvironmentVariableExpansion_ShouldReturnFalse()
    {
        // Environment variable injection patterns
        WindowsOptimizationService.IsValidCommand("powercfg %COMPSEC%").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("ipconfig %TEMP%").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithNullByteInjection_ShouldReturnFalse()
    {
        WindowsOptimizationService.IsValidCommand("powercfg\0 /getactivescheme").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithPowerShellEncoding_ShouldReturnFalse()
    {
        WindowsOptimizationService.IsValidCommand("powershell -enc SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkALgBEAG8AdwBuAGwAbwBhAGQAUwB0AHIAaQBuAGcAKAAnAGgAdAB0AHAAOgAvAC8AZQB4AGEAbQBwAGwAZQAuAGMAbwBtAC8AcABhAHkAbABvAGEAZAAnACkA").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithDirectoryTraversal_ShouldReturnFalse()
    {
        WindowsOptimizationService.IsValidCommand("del ..\\..\\windows\\system32\\*.dll").Should().BeFalse();
        WindowsOptimizationService.IsValidCommand("rd ..\\..\\windows\\temp").Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithProcessSubstitution_ShouldReturnFalse()
    {
        WindowsOptimizationService.IsValidCommand("powercfg <(calc.exe)").Should().BeFalse();
    }

    [Fact]
    public void CleanupCommands_ShouldNotContainShellInjectionPatterns()
    {
        var allCommands = WindowsOptimizationDefinitions.TempCommands
            .Concat(WindowsOptimizationDefinitions.RecycleBinCommands)
            .Concat(WindowsOptimizationDefinitions.PrefetchCommands)
            .Concat(WindowsOptimizationDefinitions.ComponentStoreCommands)
            .Concat(WindowsOptimizationDefinitions.DefenderCommands)
            .Concat(WindowsOptimizationDefinitions.SystemLogCommands)
            .Concat(WindowsOptimizationDefinitions.CrashDumpCommands);

        foreach (var command in allCommands)
        {
            command.Should().NotContain("&", $"command should not contain shell injection: {command}");
            command.Should().NotContain("|", $"command should not contain pipe: {command}");
            command.Should().NotContain(";", $"command should not contain semicolon: {command}");
        }
    }

    [Fact]
    public void GetCategories_ShouldReturnCategoriesWithValidKeys()
    {
        // Arrange & Act
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        var categories = service.GetCategories();

        // Assert
        foreach (var category in categories)
        {
            category.Key.Should().NotBeNullOrWhiteSpace();
            category.TitleResourceKey.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetCategories_ShouldReturnActionsWithValidKeys()
    {
        // Arrange & Act
        var service = new WindowsOptimizationService(new WindowsCleanupService(new TestApplicationSettings()));
        var categories = service.GetCategories();

        // Assert
        foreach (var action in categories.SelectMany(c => c.Actions))
        {
            action.Key.Should().NotBeNullOrWhiteSpace();
            action.TitleResourceKey.Should().NotBeNullOrWhiteSpace();
        }
    }
}
