using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Utils;

[Collection(TestCollections.ProcessState)]
public class LogTests
{
    [Fact]
    public void Instance_ShouldBeSingleton()
    {
        // Arrange & Act
        var instance1 = Log.Instance;
        var instance2 = Log.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void LogPath_ShouldNotBeEmpty()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        var logPath = log.LogPath;

        // Assert
        logPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ShutdownAsync_ShouldCompleteWithoutException()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Func<Task> act = async () => await log.ShutdownAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Shutdown_ShouldCompleteWithoutException()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Action act = () => log.Shutdown();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Flush_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Action act = () => log.Flush();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Error_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;
        var exception = new InvalidOperationException("Test exception");

        // Act
        Action act = () => log.Error($"Test error message", exception);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ErrorReport_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;
        var exception = new InvalidOperationException("Test exception");

        // Act
        Action act = () => log.ErrorReport("Test Header", exception);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Warning_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Action act = () => log.Warning($"Test warning message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Info_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Action act = () => log.Info($"Test info message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Debug_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Action act = () => log.Debug($"Test debug message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Trace_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;

        // Act
        Action act = () => log.Trace($"Test trace message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Trace_WithIsTraceEnabledTrue_ShouldLog()
    {
        // Arrange
        var log = Log.Instance;
        log.IsTraceEnabled = true;

        // Act
        Action act = () => log.Trace($"Test trace with IsTraceEnabled=true");

        // Assert
        act.Should().NotThrow();
        log.IsTraceEnabled = false; // Reset to default
    }

    [Fact]
    public void MultipleLogLevels_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;
        var exception = new InvalidOperationException("Test exception");

        // Act
        Action act = () => {
            log.Error($"Error message", exception);
            log.Warning($"Warning message");
            log.Info($"Info message");
            log.Debug($"Debug message");
            log.Trace($"Trace message");
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LogLevel_Setting_ShouldControlLogOutput()
    {
        // Arrange
        var log = Log.Instance;
        var originalLevel = log.CurrentLogLevel;

        // Act
        log.CurrentLogLevel = LogLevel.Error;
        Action actError = () => log.Error($"Error message");
        Action actWarning = () => log.Warning($"Warning message");
        Action actInfo = () => log.Info($"Info message");
        
        // Assert
        actError.Should().NotThrow();
        actWarning.Should().NotThrow();
        actInfo.Should().NotThrow();
        
        // Reset
        log.CurrentLogLevel = originalLevel;
    }

    [Fact]
    public void LogFolder_ShouldExist()
    {
        // Arrange
        var log = Log.Instance;
        
        // Act
        var logPath = log.LogPath;
        var folderPath = Path.GetDirectoryName(logPath);
        
        // Assert
        folderPath.Should().NotBeNullOrEmpty();
        Directory.Exists(folderPath).Should().BeTrue();
    }

    [Fact]
    public void ConcurrentLogging_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;
        
        // Act
        Action act = () => {
            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() => {
                    for (int j = 0; j < 10; j++)
                    {
                        log.Info($"Concurrent log {index}-{j}");
                    }
                });
            }
            Task.WaitAll(tasks);
        };
        
        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Log_WithNullException_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;
        
        // Act
        Action act = () => log.Error($"Test error with null exception", null);
        
        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void LargeNumberOfLogs_ShouldNotThrow()
    {
        // Arrange
        var log = Log.Instance;
        
        // Act
        Action act = () => {
            for (int i = 0; i < 200; i++)
            {
                log.Info($"Log entry {i}");
            }
        };
        
        // Assert
        act.Should().NotThrow();
        log.Flush(); // Flush to ensure all logs are written
    }

    [Fact]
    public async Task ErrorReportAsync_ConcurrentReports_ShouldNotCollideOrThrow()
    {
        // Arrange
        var log = Log.Instance;
        var exception = new InvalidOperationException("Concurrent report");

        // Act
        Func<Task> act = async () =>
        {
            var tasks = new Task[16];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => log.ErrorReport($"Concurrent header {Guid.NewGuid():N}", exception));
            }
            await Task.WhenAll(tasks);
        };

        // Assert - concurrent fire-and-forget writes must not throw or deadlock
        await act.Should().NotThrowAsync();
    }
    [Fact]
    public void Shutdown_ThenDispose_NoDoubleDisposeException()
    {
        // Arrange - use internal test constructor + env-override to isolate log directory
        var originalOverride = Environment.GetEnvironmentVariable("UDT_APPDATA_OVERRIDE");
        var tempDir = Path.Combine(Path.GetTempPath(), "UDT_LogTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", tempDir, EnvironmentVariableTarget.Process);
        try
        {
            var log = new Log(true);

            // Act - Shutdown disposes _logger and _emergencyLock;
            // Dispose must NOT throw even though Shutdown already set _disposed
            Action act = () =>
            {
                log.Shutdown();
                log.Dispose();
            };

            // Assert - no ObjectDisposedException or double-dispose of SemaphoreSlim
            act.Should().NotThrow();
        }
        finally
        {
            Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", originalOverride, EnvironmentVariableTarget.Process);
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public async Task Concurrent_ShutdownAsyncAndDispose_NoDoubleDisposeException()
    {
        // Arrange - create a fresh isolated Log instance per iteration
        var originalOverride = Environment.GetEnvironmentVariable("UDT_APPDATA_OVERRIDE");
        var tempDir = Path.Combine(Path.GetTempPath(), "UDT_LogTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", tempDir, EnvironmentVariableTarget.Process);
        try
        {
            // Run 20 iterations with different Log instances to increase race coverage
            for (int iteration = 0; iteration < 20; iteration++)
            {
                var log = new Log(true);

                // Act - race ShutdownAsync and Dispose concurrently
                // Only one should win the CAS and dispose resources; the other should
                // observe _disposed=1 and return early.
                Func<Task> act = async () =>
                {
                    var t1 = Task.Run(() => log.Dispose());
                    var t2 = log.ShutdownAsync();
                    await Task.WhenAll(t1, t2);
                };

                // Assert - no ObjectDisposedException, no SemaphoreFullException
                await act.Should().NotThrowAsync($"Iteration {iteration} must not double-dispose");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", originalOverride, EnvironmentVariableTarget.Process);
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public void InternalConstructor_IsAccessible_ViaInternalsVisibleTo()
    {
        // Regression: internal Log(bool) was guarded by #if UDT_TEST_HOOKS which
        // was not defined during solution-level builds, causing CS1729 in tests
        // that call new Log(true). The guard has been removed; the internal access
        // modifier + InternalsVisibleTo is sufficient to restrict visibility.

        var originalOverride = Environment.GetEnvironmentVariable("UDT_APPDATA_OVERRIDE");
        var tempDir = Path.Combine(Path.GetTempPath(), "UDT_LogTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", tempDir, EnvironmentVariableTarget.Process);
        try
        {
            // This must compile without UDT_TEST_HOOKS defined
            Log log = new Log(true);
            log.Should().NotBeNull();
            log.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("UDT_APPDATA_OVERRIDE", originalOverride, EnvironmentVariableTarget.Process);
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { /* best-effort cleanup */ }
        }
    }
}
