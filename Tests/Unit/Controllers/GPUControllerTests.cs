using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;
using Moq;
using UniversalDeviceToolkit.Tests.Infrastructure;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GPUControllerTests : UnitTestBase
{
    private Mock<IGPUProcessManager> _processManagerMock = null!;
    private Mock<IGPUHardwareManager> _hardwareManagerMock = null!;
    private GPUController _controller = null!;

    protected override void Setup()
    {
        _processManagerMock = new Mock<IGPUProcessManager>(MockBehavior.Loose);
        _hardwareManagerMock = new Mock<IGPUHardwareManager>(MockBehavior.Loose);
        _controller = new GPUController(_processManagerMock.Object, _hardwareManagerMock.Object, new DefaultDelayProvider());
    }

    protected override void Cleanup()
    {
        _controller?.Dispose();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _controller.Should().NotBeNull();
        _controller.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void IsStarted_WhenNotStarted_ShouldReturnFalse()
    {
        _controller.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenCalled_ShouldSetIsStartedToTrue()
    {
        await _controller.StartAsync(delay: 100, interval: 5000);

        _controller.IsStarted.Should().BeTrue();

        await _controller.StopAsync(waitForFinish: false);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyStarted_ShouldNotStartAgain()
    {
        await _controller.StartAsync(delay: 1000, interval: 5000);
        var firstStartTask = _controller.StartAsync(delay: 1000, interval: 5000);

        firstStartTask.IsCompleted.Should().BeTrue();
        _controller.IsStarted.Should().BeTrue();

        await _controller.StopAsync(waitForFinish: false);
    }

    [Fact]
    public async Task StopAsync_WhenCalled_ShouldSetIsStartedToFalse()
    {
        await _controller.StartAsync(delay: 100, interval: 5000);
        await _controller.StopAsync(waitForFinish: false);

        _controller.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotStarted_ShouldNotThrow()
    {
        var act = async () => await _controller.StopAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetLastKnownStateAsync_WhenNotStarted_ShouldReturnUnknown()
    {
        var state = await _controller.GetLastKnownStateAsync();

        state.Should().Be(GPUState.Unknown);
    }

    [Fact]
    public async Task RestartGPUAsync_WhenStateIsUnknown_ShouldNotCallHardwareManager()
    {
        await _controller.RestartGPUAsync();

        _hardwareManagerMock.Verify(
            m => m.RestartGPUAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task KillGPUProcessesAsync_WhenStateIsUnknown_ShouldNotCallProcessManager()
    {
        await _controller.KillGPUProcessesAsync();

        _processManagerMock.Verify(
            m => m.KillGPUProcessesAsync(It.IsAny<IEnumerable<Process>>()),
            Times.Never);
    }

    [Fact]
    public void Dispose_WhenCalled_ShouldNotThrow()
    {
        var act = () => _controller.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        var act = () =>
        {
            _controller.Dispose();
            _controller.Dispose();
            _controller.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task StartAndStop_MultipleCycles_ShouldWorkCorrectly()
    {
        for (int i = 0; i < 3; i++)
        {
            await _controller.StartAsync(delay: 50, interval: 1000);
            _controller.IsStarted.Should().BeTrue();

            await Task.Delay(100);

            await _controller.StopAsync(waitForFinish: false);
            _controller.IsStarted.Should().BeFalse();
        }
    }

    #region Extended Edge Case Tests

    [Fact]
    public async Task StopAsync_WithWaitForFinish_ShouldComplete()
    {
        // Re-initialize to ensure the test owns the controller lifecycle.
        _processManagerMock = new Mock<IGPUProcessManager>(MockBehavior.Loose);
        _hardwareManagerMock = new Mock<IGPUHardwareManager>(MockBehavior.Loose);
        _controller = new GPUController(_processManagerMock.Object, _hardwareManagerMock.Object, new DefaultDelayProvider());

        await _controller.StartAsync(delay: 50, interval: 5000);
        _controller.IsStarted.Should().BeTrue();

        var act = async () => await _controller.StopAsync(waitForFinish: true);

        await act.Should().NotThrowAsync();
        _controller.IsStarted.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshNowAsync_WhenNotStarted_ShouldReturnUnknownAndNotCrash()
    {
        // Re-initialize to ensure the test owns the controller lifecycle.
        _processManagerMock = new Mock<IGPUProcessManager>(MockBehavior.Loose);
        _hardwareManagerMock = new Mock<IGPUHardwareManager>(MockBehavior.Loose);
        _controller = new GPUController(_processManagerMock.Object, _hardwareManagerMock.Object, new DefaultDelayProvider());

        GPUStatus? result = null;
        var act = async () => { result = await _controller.RefreshNowAsync(); };

        await act.Should().NotThrowAsync();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task RestartGPUAsync_WhenStateIsNotActiveOrInactive_ShouldNotCallHardwareManager()
    {
        var stateField = typeof(GPUController).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        stateField!.SetValue(_controller, GPUState.PoweredOff);

        await _controller.RestartGPUAsync();

        _hardwareManagerMock.Verify(
            m => m.RestartGPUAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task KillGPUProcessesAsync_WhenStateIsNotActive_ShouldNotCallProcessManager()
    {
        var stateField = typeof(GPUController).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        stateField!.SetValue(_controller, GPUState.Inactive);

        await _controller.KillGPUProcessesAsync();

        _processManagerMock.Verify(
            m => m.KillGPUProcessesAsync(It.IsAny<IEnumerable<Process>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetLastKnownStateAsync_AfterConstructor_ShouldReturnUnknown()
    {
        var state = await _controller.GetLastKnownStateAsync();

        state.Should().Be(GPUState.Unknown);
    }

    [Fact]
    public void GPUController_ShouldTreatMissingPnpDeviceIdAsNonFatal()
    {
        var source = ReadGpuControllerSource();
        var isStartedProperty = ExtractProperty(source, "public bool IsStarted");
        var refreshMethod = ExtractMethod(source, "private async Task RefreshStateAsync()");
        var helperMethod = ExtractMethod(source, "private static async Task<string?> TryGetGpuInstanceIdAsync(string? pnpDeviceIdPart)");
        var loopMethod = ExtractMethod(source, "private async Task RefreshLoopAsync(int delay, int interval, CancellationToken token)");

        isStartedProperty.Should().Contain("lock (_startStopLock)");
        isStartedProperty.Should().Contain("return _refreshTask is { IsCompleted: false };");
        refreshMethod.Should().Contain("var gpuInstanceId = await TryGetGpuInstanceIdAsync(pnpDeviceIdPart)");
        refreshMethod.Should().NotContain("throw new InvalidOperationException(\"pnpDeviceIdPart is null or empty\")");
        helperMethod.Should().Contain("string.IsNullOrWhiteSpace(pnpDeviceIdPart)");
        helperMethod.Should().Contain("return null;");
        loopMethod.Should().Contain("catch (Exception ex) when (ex is not OperationCanceledException)");
        loopMethod.Should().NotContain("throw;");
    }

    #endregion

    private static string ExtractProperty(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        var nextMember = source.IndexOf("    /// <summary>", start + signature.Length, StringComparison.Ordinal);
        nextMember.Should().BeGreaterThan(start);

        return source[start..nextMember];
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        var braceStart = source.IndexOf('{', start);
        braceStart.Should().BeGreaterThanOrEqualTo(0);

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[start..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Could not extract method '{signature}'.");
    }

    private static string ReadGpuControllerSource()
    {
        var expectedRelativePath = Path.Combine("Libraries/Device", "Controllers", "GPUController.cs");
        foreach (var candidateRoot in GetRepositoryRootCandidates())
        {
            var path = Path.Combine(candidateRoot, expectedRelativePath);
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new DirectoryNotFoundException($"Could not locate repository file '{expectedRelativePath}'.");
    }

    private static IEnumerable<string> GetRepositoryRootCandidates()
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("UDT_REPOSITORY_ROOT"),
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."))
        };

        foreach (var root in roots.Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            var directory = new DirectoryInfo(root!);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "UniversalDeviceToolkit.sln")))
                    yield return directory.FullName;

                directory = directory.Parent;
            }
        }
    }
}
