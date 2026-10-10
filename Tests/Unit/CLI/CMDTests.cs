using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.System;
using Xunit;

namespace UniversalDeviceToolkit.Tests.CLI;

public class CMDTests
{
    #region Basic Execution

    [Fact]
    public async Task RunAsync_WithValidCommand_ShouldReturnSuccess()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo test", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("test");
        output.Should().NotContain("UNC paths are not supported");
        output.Should().NotContain("current directory");
    }

    [Fact]
    public async Task RunAsync_WithInvalidFile_ShouldThrowException()
    {
        Func<Task> act = async () => await CMD.RunAsync("nonexistent.exe", "");

        await act.Should().ThrowAsync<Win32Exception>();
    }

    [Fact]
    public async Task RunAsync_WithDangerousInput_ShouldThrowArgumentException()
    {
        Func<Task> act = async () => await CMD.RunAsync("cmd.exe", "& del /f /q *.*");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithCreateNoWindowFalse_ShouldSucceed()
    {
        var (exitCode, _) = await CMD.RunAsync("cmd.exe", "/c echo test", createNoWindow: false, waitForExit: true);

        exitCode.Should().Be(0);
    }

    #endregion

    #region WaitForExit Behavior

    [Fact]
    public async Task RunAsync_WithWaitForExitFalse_ShouldReturnImmediately()
    {
        var startTime = DateTime.UtcNow;
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c ping -n 20 127.0.0.1 >nul", waitForExit: false);
        var elapsed = DateTime.UtcNow - startTime;

        exitCode.Should().Be(-1);
        output.Should().BeEmpty();
        elapsed.TotalSeconds.Should().BeLessThan(1);
    }

    [Fact]
    public async Task RunAsync_WithWaitForExitFalse_AndLargeBackgroundOutput_ShouldStillFinish()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var markerPath = Path.Combine(tempDir, "done.txt");
        var scriptPath = Path.Combine(tempDir, "background-output.cmd");
        await File.WriteAllTextAsync(scriptPath,
            $"@echo off{Environment.NewLine}for /L %%i in (1,1,2000) do @echo line%%i{Environment.NewLine}echo done>{markerPath}{Environment.NewLine}");

        try
        {
            var (exitCode, output) = await CMD.RunAsync("cmd.exe", $"/c \"{scriptPath}\"", waitForExit: false);

            exitCode.Should().Be(-1);
            output.Should().BeEmpty();

            var completed = await WaitForFileAsync(markerPath, TimeSpan.FromSeconds(20));
            completed.Should().BeTrue("background processes should not block on unread redirected output");
        }
        finally
        {
            await RetryDeleteAsync(scriptPath);
            await RetryDeleteAsync(markerPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
        }
    }

    #endregion

    #region Environment Variables

    [Theory]
    [InlineData("/c echo %TEST_VAR%", "TEST_VAR=test_value", "test_value")]
    [InlineData("/c echo %VAR1% %VAR2% %VAR3%", "VAR1=value1,VAR2=value2,VAR3=value3", "value1")]
    [InlineData("/c echo %TEST_VAR%", "TEST_VAR=value with spaces", "value with spaces")]
    public async Task RunAsync_WithEnvironmentVariables_ShouldExpand(string arguments, string envValue, string expected)
    {
        var envDict = new Dictionary<string, string?>();
        foreach (var pair in envValue.Split(','))
        {
            var parts = pair.Split('=', 2);
            envDict[parts[0]] = parts.Length > 1 ? parts[1] : envValue;
        }

        var (exitCode, output) = await CMD.RunAsync("cmd.exe", arguments, environment: envDict, waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain(expected);
    }

    [Fact]
    public async Task RunAsync_WithNullEnvironmentVariableValue_ShouldNotFail()
    {
        var environment = new Dictionary<string, string?> { { "TEST_VAR", null } };

        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo test", environment: environment, waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("test");
    }

    [Fact]
    public async Task RunAsync_WithInvalidEnvironmentVariableKey_ShouldThrow()
    {
        var environment = new Dictionary<string, string?> { { "INVALID_KEY!", "value" } };

        Func<Task> act = async () => await CMD.RunAsync("cmd.exe", "/c echo test", environment: environment, waitForExit: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithDangerousEnvironmentValue_ShouldThrow()
    {
        var environment = new Dictionary<string, string?> { { "TEST_VAR", "value & del /f /q *.*" } };

        Func<Task> act = async () => await CMD.RunAsync("cmd.exe", "/c echo test", environment: environment, waitForExit: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithEmptyEnvironmentDictionary_ShouldSucceed()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo test", environment: new Dictionary<string, string?>(), waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("test");
    }

    [Fact]
    public async Task RunAsync_WithNullEnvironmentDictionary_ShouldSucceed()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo test", environment: null, waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("test");
    }

    #endregion

    #region Argument Validation

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task RunAsync_WithInvalidFileName_ShouldThrow(string? file)
    {
        Func<Task> act = async () => await CMD.RunAsync(file!, "/c echo test");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithNullArguments_ShouldThrow()
    {
        Func<Task> act = async () => await CMD.RunAsync("cmd.exe", null, waitForExit: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_WithWhitespaceArguments_ShouldHandle()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "   /c   echo   test   ", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("test");
    }

    #endregion

    #region Command Chaining Rejection

    [Theory]
    [InlineData("/c echo line1 & echo line2 & echo line3")]
    [InlineData("/c echo line1 && echo line2 && echo line3")]
    [InlineData("/c echo test123 | find \"test\"")]
    [InlineData("/c echo test123 | findstr \"test\"")]
    [InlineData("/c (echo c && echo a && echo b) | sort")]
    [InlineData("/c shift && echo shifted")]
    [InlineData("/c color 0A && echo color set")]
    [InlineData("/c title Test Window && echo title set")]
    [InlineData("/c cls && echo cleared")]
    [InlineData("/c prompt $P$G && echo prompt set")]
    [InlineData("/c pushd %TEMP% && echo %CD% && popd && echo %CD%")]
    [InlineData("/c setlocal && set TEST=local && echo %TEST% && endlocal && echo %TEST%")]
    [InlineData("/c setlocal enabledelayedexpansion && set VAR=test && echo !VAR! && endlocal")]
    [InlineData("/c set VAR=hello && echo %VAR% world")]
    [InlineData("/c set VAR=hello && echo %VAR:~0,3%")]
    [InlineData("/c set VAR=hello world && echo %VAR:world=there%")]
    [InlineData("/c exit 0 && echo errorlevel=%ERRORLEVEL%")]
    [InlineData("/c echo test | timeout /t 1 /nobreak")]
    [InlineData("/c echo test | choice /c yn /t 1 /d y")]
    public async Task RunAsync_WithCommandChainingOperators_ShouldThrow(string arguments)
    {
        Func<Task> act = async () => await CMD.RunAsync("cmd.exe", arguments, waitForExit: true);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ContainsDangerousInput

    [Theory]
    [InlineData(">nul 2>&1")]
    [InlineData(">&1")]
    [InlineData(">&2")]
    [InlineData("1>&2")]
    [InlineData("2>&1")]
    [InlineData(">output.txt")]
    [InlineData("<input.txt")]
    [InlineData(">>append.txt")]
    [InlineData("echo test^&done")]
    [InlineData(null)]
    [InlineData("")]
    public void ContainsDangerousInput_WithSafeInput_ShouldReturnFalse(string? input)
    {
        CMD.ContainsDangerousInput(input!).Should().BeFalse($"Input '{input}' should be considered safe");
    }

    [Theory]
    [InlineData("command1 & command2")]
    [InlineData("command1 && command2")]
    [InlineData("command1 || command2")]
    [InlineData("command1 | command2")]
    [InlineData("command1; command2")]
    [InlineData("$()")]
    [InlineData("`command`")]
    [InlineData("echo test 2>&1 & whoami")]
    [InlineData("echo test 2>&1&whoami")]
    [InlineData("echo test&whoami")]
    public void ContainsDangerousInput_WithDangerousInput_ShouldReturnTrue(string input)
    {
        CMD.ContainsDangerousInput(input).Should().BeTrue($"Input '{input}' should be considered dangerous");
    }

    #endregion

    #region Exit Codes

    [Theory]
    [InlineData(5, "/c exit 5")]
    [InlineData(1, "/c exit /b 1")]
    [InlineData(255, "/c exit /b 255")]
    public async Task RunAsync_WithExitCode_ShouldReturnCorrectCode(int expectedExitCode, string arguments)
    {
        var (exitCode, _) = await CMD.RunAsync("cmd.exe", arguments, waitForExit: true);

        exitCode.Should().Be(expectedExitCode);
    }

    #endregion

    #region Special Output

    [Fact]
    public async Task RunAsync_WithSpecialCharacters_ShouldHandle()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo test!@#$%^&*()", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("test");
    }

    [Fact]
    public async Task RunAsync_WithUnicodeCharacters_ShouldHandle()
    {
        var (exitCode, _) = await CMD.RunAsync("cmd.exe", "/c echo 测试中文", waitForExit: true);

        exitCode.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithErrorOutput_ShouldCapture()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo error message >&2", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("error message");
    }

    [Fact]
    public async Task RunAsync_WithLargeOutput_ShouldHandle()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c for /L %i in (1,1,1000) do @echo line%i", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("line1");
        output.Should().Contain("line1000");
    }

    [Fact]
    public async Task RunAsync_WithEmptyOutput_ShouldReturnEmpty()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c rem This is a comment with no output", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_WithQuotedArguments_ShouldHandle()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", @"/c echo ""quoted string""", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("quoted string");
    }

    [Fact]
    public async Task RunAsync_WithTabInOutput_ShouldHandle()
    {
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", "/c echo col1\tcol2\tcol3", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain("col1");
    }

    [Fact]
    public async Task RunAsync_WithVeryLongArguments_ShouldHandle()
    {
        var longString = new string('a', 8000);
        var (exitCode, output) = await CMD.RunAsync("cmd.exe", $"/c echo {longString}", waitForExit: true);

        exitCode.Should().Be(0);
        output.Should().Contain(longString.Substring(0, 100));
    }

    #endregion

    #region Cancellation & Long-Running

    [Fact]
    public async Task RunAsync_WithCancellation_ShouldCancel()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = async () => await CMD.RunAsync("cmd.exe", "/c ping -n 20 127.0.0.1 >nul", waitForExit: true, token: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_WithLongRunningCommand_ShouldComplete()
    {
        var startTime = DateTime.UtcNow;
        var (exitCode, _) = await CMD.RunAsync("cmd.exe", "/c ping -n 3 127.0.0.1 >nul", waitForExit: true);
        var elapsed = DateTime.UtcNow - startTime;

        exitCode.Should().Be(0);
        elapsed.TotalSeconds.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    private static async Task<bool> WaitForFileAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
                return true;
            await Task.Delay(100);
        }
        return File.Exists(path);
    }

    private static async Task RetryDeleteAsync(string path, int maxAttempts = 5)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (!File.Exists(path))
                return;
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (i < maxAttempts - 1)
            {
                await Task.Delay(200);
            }
        }
    }
}
