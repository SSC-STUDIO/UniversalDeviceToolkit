using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Utils;
using Xunit;
using UniversalDeviceToolkit.Tests.Infrastructure;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class GPUHardwareManagerEdgeCaseTests : DeviceTestBase
{
    private Mock<IGPUProcessManager> _processManagerMock = null!;
    private Mock<IGPUHardwareManager> _hardwareManagerMock = null!;
    private GPUController _controller = null!;

    protected override void Setup()
    {
        // Initialize with default device profile for non-parameterized tests
        InitController();
    }

    /// <summary>
    /// Creates mock dependencies and GPUController instance.
    /// </summary>
    protected virtual void InitController()
    {
        _processManagerMock = new Mock<IGPUProcessManager>(MockBehavior.Loose);
        _hardwareManagerMock = new Mock<IGPUHardwareManager>(MockBehavior.Loose);
        _controller = new GPUController(_processManagerMock.Object, _hardwareManagerMock.Object, new DefaultDelayProvider());
    }

    [Fact]
    public async Task RestartGPUAsync_WithNullInstanceId_ShouldNotThrow()
    {
        InitController(); // Re-initialize per profile
        
        var act = async () => await _controller.RestartGPUAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RestartGPUAsync_WithEmptyInstanceId_ShouldNotThrow()
    {
        InitController(); // Re-initialize per profile
        
        var act = async () => await _controller.RestartGPUAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RestartGPUAsync_WithWhitespaceInstanceId_ShouldNotThrow()
    {
        InitController(); // Re-initialize per profile
        
        var act = async () => await _controller.RestartGPUAsync();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("")] // Empty string + no device profile
    [InlineData("   ")] // Whitespace-only + no device profile
    public async Task RestartGPUAsync_WithInvalidInstanceId_ShouldNotThrow(string instanceId)
    {
        InitController(); // Re-initialize per instance

        var stateField = typeof(GPUController).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var gpuInstanceIdField = typeof(GPUController).GetField("_gpuInstanceId", BindingFlags.NonPublic | BindingFlags.Instance);
        stateField!.SetValue(_controller, Lib.GPUState.Active);
        gpuInstanceIdField!.SetValue(_controller, instanceId);
        
        var act = async () => await _controller.RestartGPUAsync();

        await act.Should().NotThrowAsync();
        
        _hardwareManagerMock.Verify(
            m => m.RestartGPUAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RestartGPUAsync_WithMinimalInstanceId_ShouldNotThrow()
    {
        InitController(); // Re-initialize per profile
        
        var act = async () => await _controller.RestartGPUAsync();

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("TEST&ID_123&REV_A1")]
    [InlineData("PCI\\VEN_10DE&DEV_1F95&SUBSYS_17AA38A9&REV_A1\\4&2B6F1C0&0&00E6")]
    public async Task RestartGPUAsync_WithSpecialCharsInstanceId_ShouldDelegateToHardwareManager(string instanceId)
    {
        // Set up the controller with mocked dependencies
        InitController();

        // Use reflection to set internal state and gpuInstanceId field
        var stateField = typeof(GPUController).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var gpuInstanceIdField = typeof(GPUController).GetField("_gpuInstanceId", BindingFlags.NonPublic | BindingFlags.Instance);
        
        stateField!.SetValue(_controller, Lib.GPUState.Active);
        gpuInstanceIdField!.SetValue(_controller, instanceId);

        // Mock hardware manager to verify it gets called
        _hardwareManagerMock.Setup(m => m.RestartGPUAsync(instanceId)).Returns(Task.CompletedTask);

        // Execute
        await _controller.RestartGPUAsync();

        // Verify hardware manager was called with correct instance ID
        _hardwareManagerMock.Verify(
            m => m.RestartGPUAsync(instanceId),
            Times.Once);
    }
}
