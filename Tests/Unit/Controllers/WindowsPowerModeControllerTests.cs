using System;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Settings;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class WindowsPowerModeControllerTests : UnitTestBase
{
    private ApplicationSettings _settings = null!;
    private Mock<IMainThreadDispatcher> _dispatcherMock = null!;
    private WindowsPowerModeController _controller = null!;

    protected override void Setup()
    {
        _settings = new ApplicationSettings();
        _dispatcherMock = new Mock<IMainThreadDispatcher>(MockBehavior.Loose);
        _controller = new WindowsPowerModeController(_settings, _dispatcherMock.Object);
    }

    protected override void Cleanup()
    {
        _controller?.Dispose();
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        _controller.Should().NotBeNull();
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
    public async Task SetPowerModeAsync_WhenMappingModeNotWindowsPowerMode_ShouldReturnEarly()
    {
        _settings.Store.PowerModeMappingMode = PowerModeMappingMode.Disabled;

        await _controller.SetPowerModeAsync(PowerModeState.Quiet);

        _dispatcherMock.Verify(
            d => d.Dispatch(It.IsAny<Action>()),
            Times.Never);
    }

    [Fact]
    public async Task SetPowerModeAsync_WhenMappingModeIsWindowsPowerPlan_ShouldReturnEarly()
    {
        _settings.Store.PowerModeMappingMode = PowerModeMappingMode.WindowsPowerPlan;

        await _controller.SetPowerModeAsync(PowerModeState.Balance);

        _dispatcherMock.Verify(
            d => d.Dispatch(It.IsAny<Action>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_WithValidSettings_ShouldNotThrow()
    {
        var act = () => new WindowsPowerModeController(new ApplicationSettings(), new Mock<IMainThreadDispatcher>().Object);

        act.Should().NotThrow();
    }

    [Fact]
    public void PowerModes_DefaultStore_ShouldContainZeroModes()
    {
        _settings.Store.PowerModes.Should().BeEmpty();
    }

    [Fact]
    public void PowerModeMappingMode_Default_ShouldBeWindowsPowerMode()
    {
        _settings.Store.PowerModeMappingMode.Should().Be(PowerModeMappingMode.WindowsPowerMode);
    }
}
